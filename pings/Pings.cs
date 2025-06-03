using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using HMLLibrary;
using Steamworks;
using UnityEngine;

namespace pings
{
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class Pings : Mod
    {
        // Mod information
        internal const int ModChannel = 4571607; // Channel for mod messages
        internal static CSteamID SteamID => RAPI.GetLocalPlayer().steamID;
        
        // Materials for outlines
        private AssetBundle _asset;
        internal static Material OutlineMaterial, FillMaterial;

        #region Mod Loading / Unloading
        public IEnumerator Start()
        {
            #region AssetBundle Loading
            var request = AssetBundle.LoadFromMemoryAsync(GetEmbeddedFileBytes("misc/outline.assets"));
            yield return request;
            _asset = request.assetBundle;
            OutlineMaterial = _asset.LoadAsset<Material>("OutlineMask");
            FillMaterial = _asset.LoadAsset<Material>("OutlineFill");
            #endregion
            
            Log("Mod pings is loaded :)");
            
            Networking.OnLoad();
            PingManager.Setup();
            
        }

        public void OnModUnload()
        {
            #region AssetBundle Unloading
            _asset?.Unload(true);
            Destroy(OutlineMaterial);
            Destroy(FillMaterial);
            #endregion

            Networking.OnUnload();
            PingManager.Cleanup();
            
            Log("Mod Pings is unloaded :3");
        }
        #endregion

        #region Mod activity // PingManager Setup and Cleanup
        private static PingManager _pingManager;
        private static bool _hasPingsMod;
        internal static bool HasPingsMod
        {
            set
            {
                if (_hasPingsMod == value) return;
                _hasPingsMod = value;
                if (value)
                {
                    _pingManager = new GameObject("PingManager").AddComponent<PingManager>();
                    DontDestroyOnLoad(_pingManager.gameObject);
                }
                else
                {
                    PingManager.RemoveAllPings();
                    Destroy(_pingManager);
                }
            }
        }
        #endregion

        #region Networking
        
        public void FixedUpdate() => Networking.CheckMessages();
        public override void WorldEvent_WorldLoaded() => Networking.OnLoad();
        public override void WorldEvent_WorldUnloaded() => Networking.OnUnload();
        
        #endregion

        #region Settings // ExtraSettingsAPI integration
        
        public static Keybind PingKey { get; private set; } = new Keybind("pingKeybind", KeyCode.Mouse2);
        public static float PingDuration { get; private set; } = 10f; 
        public static bool DebugMode { get; private set; }

        public void ExtraSettingsAPI_Load() { Load_ExtraSettingsAPI_Settings(); }

        public void ExtraSettingsAPI_SettingsClose() { Load_ExtraSettingsAPI_Settings(); }

        private void Load_ExtraSettingsAPI_Settings()
        {
            PingKey = ExtraSettingsAPI_GetKeybind("pingKeybind");
            PingDuration = ExtraSettingsAPI_GetSliderValue("pingDuration");
            DebugMode = ExtraSettingsAPI_GetCheckboxState("debugMode");
        }

        public void ExtraSettingsAPI_Unload()
        {
            PingKey = new Keybind("pingKeybind", KeyCode.Mouse2);
            PingDuration = 10f;
            DebugMode = false;
        }
        // Overridden by ExtraSettingsAPI
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Keybind ExtraSettingsAPI_GetKeybind(string SettingName) => null;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static float ExtraSettingsAPI_GetSliderValue(string SettingName) => 0;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool ExtraSettingsAPI_GetCheckboxState(string SettingName) => false;
        
        #endregion
    }

    public static class CastUtil
    {
        #region Cone Cast For Pings
        /// <summary>
        /// Performs a cone cast by doubling radius and distance from start on each iteration, up to 7 times.
        /// </summary>
        /// <param name="ray">Ray</param>
        /// <param name="hit">Hit info (if any)</param>
        /// <returns>True if hit, false otherwise</returns>
        public static bool PingCast(Ray ray, out RaycastHit hit)
        {
            var radius = 0.1f; // Starting radius
            var distanceFromOrigin = 0.2f; // Starting distance
        
            for (var i = 0; i < 7; i++)
            {
                if (Physics.SphereCast(ray.origin + ray.direction * distanceFromOrigin, radius, ray.direction, out hit, 280f))
                    return true;

                radius *= 2f;
                distanceFromOrigin *= 2f;
                distanceFromOrigin += 1f;
            }

            hit = default;
            return false;
        }
        #endregion

        #region Closest Collider at Hit Point
        private static readonly Collider[] Colliders = new Collider[128]; // I got 8 colliders in a single ping at most, so 128 should be more than enough
        public static Transform ClosestCollider(Vector3 worldPos)
        {
            const float radius = 0.1f;
            var s = 1;
            var amount = 0;
            while (s <= 128) // Limit search radius to 12.8 meters
            {
                amount = Physics.OverlapSphereNonAlloc(worldPos, radius * s, Colliders);
                if (amount > 0) break; // Found at least one collider
                s *= 2; // Increase the search radius
            }

            Transform closest = null;
            var minDist = float.MaxValue;

            for (var i = 0; i < amount; i++)
            {
                var col = Colliders[i];
                var dist = Vector3.Distance(worldPos, col.transform.position);
                if (!(dist < minDist) || col.transform.name.Contains("Player")) continue;
                minDist = dist;
                closest = col.transform;
            }
            
            return closest;
        }
        #endregion
    }
}
