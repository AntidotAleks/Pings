using System;
using System.Linq;
using JetBrains.Annotations;
using pings.outlines.fancy;
using pings.outlines.quick;
using Sirenix.Utilities;
using Steamworks;
using UltimateWater;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace pings.outlines
{
    public abstract class Outline : MonoBehaviour
    {
        public abstract void SetColor(Color color);
    }

    public static class QuickOutlineBundle
    { public static Material OutlineMaterial, FillMaterial; }

    public static class FancyOutlineBundle
    { public static Shader BufferShader; public static ComputeShader OutlineShader; }

    public static class OutlineTools
    {
        [CanBeNull]
        public static Outline CreateOutline(Transform target, CSteamID steamID)
        {
            if (!target || Pings.Style == OutlineStyle.Disabled) return null;

            try {
                var outline = GetOrCreateOutlineComponent(target.gameObject);
                outline.SetColor(GetColor(steamID));
                outline.enabled = true;
            
                if(Pings.DebugMode == 2) Debug.Log($"[Pings: Outline] Using outline (ID: {outline.GetInstanceID()})");
                
                return outline;
            }
            catch { return null; }
        }

        public static void RemoveOutline(PingManager.PingInstance ping)
        {
            var outline = ping.Outline;
            ping.Outline = null;
            if (!outline || OutlineIsUsed(outline)) return;
            if (Pings.DebugMode == 2) Debug.Log($"[Pings: Outline] Removing outline (ID: {outline.GetInstanceID()})");
            Object.Destroy(outline);
        }

        public static void UpdateOutlinesStyle()
        {
            _lastCamera?.gameObject.GetComponent<FancyOutlineOnCamera>().Destroy();
            _lastCamera = null;
            
            var pingsWithOutlines = 
                from list in PingManager.ActivePings.Values 
                from ping in list 
                where ping.Outline 
                select ping;
            
            foreach (var ping in pingsWithOutlines)
                RemoveOutline(ping);
            
            // foreach (var ping in pingsWithOutlines)
            // {
            //     var transform = ping.Outline?.transform;
            //     ping.Outline = CreateOutline(transform, ping.SteamID);
            // }
        }
        
        
        private static Camera _lastCamera;
        private static Outline GetOrCreateOutlineComponent(GameObject go)
        {
            if (go.TryGetComponent<Outline>(out var outline)) return outline;

            switch (Pings.Style)
            {
                case OutlineStyle.Quick: return go.AddComponent<QuickOutline>();
                
                case OutlineStyle.Fancy: 
                    if (Pings.Camera != _lastCamera && Pings.Camera)
                    {
                        _lastCamera?.gameObject.GetComponents<FancyOutlineOnCamera>()?.ForEach(Object.DestroyImmediate);
                        _lastCamera = Pings.Camera;
                        _lastCamera.gameObject.GetComponents<FancyOutlineOnCamera>()?.ForEach(Object.DestroyImmediate);
                        if (Pings.DebugMode == 2) Debug.Log($"[Pings: Outline] Updating camera reference for Fancy Outline (ID: {_lastCamera.GetInstanceID()})");
                        Pings.Camera.gameObject.AddComponent<FancyOutlineOnCamera>();
                    }
                    return go.AddComponent<FancyOutline>();
                
                default: return null;
            }
        }

        private static bool OutlineIsUsed(Outline outline) =>
            outline && (
                from queue in PingManager.ActivePings.Values
                from ping in queue 
                where ping.Outline == outline 
                select ping.Outline
            ).FirstOrDefault();


        private static Color GetColor(CSteamID steamID)
        {
            // return Color.HSVToRGB(Random.value, .7f+Random.value*.2f, .8f+Random.value*.2f); // Test
            bool isSelf = steamID == Pings.SteamID;
            
            switch (Pings.Style)
            {
                case OutlineStyle.Quick:
                    return Pings.PingColor;
                case OutlineStyle.Fancy:
                    if (isSelf && Pings.HasOwnPingColor)
                        return Pings.OwnPingColor;
                    return Pings.UniquePingColors ? 
                        Color.HSVToRGB((steamID.m_SteamID % 1000) / 1000f, 0.8f, 1) 
                        : Pings.PingColor;

                default: throw new ArgumentOutOfRangeException();
            }
        }
    }
}