using System;
using System.Collections.Generic;
using System.Linq;
using Steamworks;
using UnityEngine;
using UnityEngine.UI;

namespace pings
{
    public class PingManager : MonoBehaviour
    {
        private static Canvas _canvas;
        private static GameObject _pingPrefab;
        private static Camera Camera => Camera.main ?? Camera.current;
        private static readonly Dictionary<CSteamID, PingInstance> ActivePings = new Dictionary<CSteamID, PingInstance>();

        private const float ScaleFactor = 10f;
        internal void Update()
        {
            if (!RAPI.IsCurrentSceneGame()) return;

            var toRemove = new List<CSteamID>();
            foreach (var (steamID, ping) in ActivePings)
            {
                if (Time.time > ping.SpawnTime + Pings.PingDuration)
                {
                    toRemove.Add(steamID);
                    continue;
                }
            
                var worldPos = ping.WorldPosition;
                var screenPos = Camera.WorldToScreenPoint(worldPos);
                if (screenPos.z < 0) screenPos *= -1;

                var rt = ping.UIObject.transform;
                rt.position = screenPos;

                var distance = Vector3.Distance(Camera.transform.position, worldPos);
                var scale = Mathf.Clamp(1f / distance, 0.1f, 2.5f) * ScaleFactor;
                rt.localScale = Vector3.one * scale;
            }
            foreach (var steamID in toRemove)
                RemovePing(steamID);
            
            if (CanvasHelper.ActiveMenu != MenuType.None) return;

            if (!Input.GetKeyDown(Pings.PingKey.MainKey) && !Input.GetKeyDown(Pings.PingKey.AltKey)) return;
            
            var ray = Camera.ScreenPointToRay(Input.mousePosition);
            if (!CastUtil.PingCast(ray, out var hit)) return;
            
            var p = new PingMessage(hit.point, Pings.SteamID);
            RAPI.SendNetworkMessage(p, Pings.ModChannel); // Send ping to other players
            CreatePing(Pings.SteamID, hit.point, CastUtil.ClosestCollider(hit.point)); // Using the closest collider instead of hit.transform, so pings between players are more consistent
        }

        internal static void CreatePing(CSteamID steamID, Vector3 worldPos, Transform hitTransform)
        {
            if (!hitTransform) return;
            RemovePing(steamID);
            
            // Speedtest
            var (pingName, pingTransform) = GetPingData(hitTransform, worldPos);

            // Create ping
            var pingUI = Instantiate(_pingPrefab, _canvas.transform);
            pingUI.SetActive(true);
            pingUI.GetComponentInChildren<Text>().text = pingName;
            
            // Add outline to the hit object
            var outline = CreateOutline(pingTransform);
            ActivePings[steamID] = new PingInstance
            {
                HitTransform = pingTransform ?? hitTransform,
                LocalPosition = (pingTransform ?? hitTransform) ? (pingTransform ?? hitTransform).InverseTransformPoint(worldPos) : worldPos,
                UIObject = pingUI,
                SpawnTime = Time.time,
                Outline = outline
            };
        }

        private static void RemovePing(CSteamID steamID)
        {
            if (!ActivePings.Remove(steamID, out var ping)) return; // If ping doesn't exist, do nothing
            Destroy(ping.UIObject);
            
            if (ping.Outline && !TargetOutline(ping.HitTransform))
                // Since ping is removed from active, #DoesTargetHaveOutline will return true only if the outline is still present on other pings
                DestroyImmediate(ping.Outline); // Need to use DestroyImmediate since right after #CreateOutline will check for outlines
            
        }

        private static (string, Transform) GetPingData(Transform transform, Vector3 worldPos)
        {
            var tList = new List<Transform>();
            for (var t = transform; t; t = t.parent)
                tList.Add(t);
            var transforms = tList.ToArray();

            if (Pings.DebugMode)
                Debug.Log($"[Pings: Handling] Creating ping with {transforms.Length} transforms: {string.Join(" / ", transforms.Select(t => t.name))}");

            foreach (var rule in NameRules.WordRules.Where(rule => rule.Predicate(transforms)))
                return (rule.Result.name, transforms[ rule.Result.index + (rule.Result.index < 0 ? transforms.Length : 0) ]);
            
            foreach (var rule in NameRules.GlobalRules.Where(rule => rule.Predicate(transforms)))
                return rule.Formatter(transforms, worldPos);
            
            if (Pings.DebugMode)
                Debug.LogWarning("[Pings: Handling] No rules matched");
            return ("Ping", transform);
        }
        
        private static Outline CreateOutline(Transform target)
        {
            if (!target) return null;

            try {
                var outline = TargetOutline(target);
                if (outline)
                    return outline;
            
                outline = target.gameObject.AddComponent<Outline>();
                outline.OutlineColor = Color.yellow;
                outline.OutlineWidth = 7f;
                outline.enabled = true;
            
                return outline;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Pings: Handling] Error creating outline for {target.name}: {e.Message}");
                return null;
            }
        }
        
        private static Outline TargetOutline(Transform target)
        {
            return ActivePings.FirstOrDefault(pair => pair.Value.HitTransform == target).Value?.Outline;
        }

        private class PingInstance
        {
            public Transform HitTransform;
            public Vector3 LocalPosition;
            public GameObject UIObject;
            public float SpawnTime;
            public Outline Outline;

            public Vector3 WorldPosition => HitTransform
                ? HitTransform.TransformPoint(LocalPosition)
                : LocalPosition;
        }
        
        public static void Setup()
        {
            _canvas = pings.Setup.CreateCanvas();
            _pingPrefab = pings.Setup.CreatePingPrefab();
        }
        
        public static void RemoveAllPings()
        {
            while (ActivePings.Count > 0)
                RemovePing(ActivePings.First().Key);
        }
        
        public static void Cleanup()
        {
            RemoveAllPings();
            if (_canvas) Destroy(_canvas.gameObject);
            if (_pingPrefab) Destroy(_pingPrefab);
        }
    }
}