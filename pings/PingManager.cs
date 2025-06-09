using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Steamworks;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.Object;

namespace pings
{
    public static class PingManager
    {
        #region Variables
        private static Canvas _canvas;
        private static GameObject _pingPrefab;
        
        private static Camera Camera => Camera.main ?? Camera.current;
        private static readonly Dictionary<CSteamID, PingInstance> ActivePings = new Dictionary<CSteamID, PingInstance>();

        private const float ScaleFactor = 10f;
        #endregion
        
        #region Ping Instance
        private class PingInstance
        {
            public Transform HitTransform; // Transform of the object hit by the ping
            public Vector3 LocalPosition; // Relative position of the ping in the hit object's local space
            public GameObject UIObject; // Visual representation of the ping in the UI
            public float SpawnTime; // Time when the ping was created, used for expiration
            [CanBeNull] public Outline Outline; // Outline for the hit object

            public Vector3 WorldPosition => HitTransform
                ? HitTransform.TransformPoint(LocalPosition)
                : LocalPosition;
        }
        #endregion
        
        #region Pings Update
        internal static void UpdatePings()
        {
            if (!Pings.HasPingsMod || !RAPI.IsCurrentSceneGame()) return; // Only in game

            RemoveOldPings();
            UpdatePingPositions();
            CreatePingIfKeyPressed();
        }

        private static void RemoveOldPings()
        {
            for (var i = ActivePings.Count - 1; i >= 0; i--)
            {
                var ping = ActivePings.ElementAt(i);
                if (Time.time > ping.Value.SpawnTime + Pings.PingDuration)
                    RemovePing(ping.Key);
            }
        }

        private static void UpdatePingPositions()
        {
            foreach (var (_, ping) in ActivePings)
            {
                var worldPos = ping.WorldPosition;
                var screenPos = Camera.WorldToScreenPoint(worldPos);
                if (screenPos.z < 0) screenPos *= -1;

                var rt = ping.UIObject.transform;
                rt.position = screenPos;

                var distance = Vector3.Distance(Camera.transform.position, worldPos);
                var scale = Mathf.Clamp(1f / distance, 0.1f, 2.5f) * ScaleFactor;
                rt.localScale = Vector3.one * scale;
            }
        }

        private static void CreatePingIfKeyPressed()
        {
            if (!Input.GetKeyDown(Pings.PingKey.MainKey) && !Input.GetKeyDown(Pings.PingKey.AltKey)) return; // On key press only
            if (CanvasHelper.ActiveMenu != MenuType.None) return; // If any menu is open, ignore
            
            var ray = Camera.ScreenPointToRay(Input.mousePosition);
            if (!CastUtil.PingCast(ray, out var hit)) return; // If nothing hit, ignore
            
            var worldPos = hit.point;
            var p = new PingMessage(worldPos, Pings.SteamID);
            RAPI.SendNetworkMessage(p, Pings.ModChannel); // Send ping to other players
            CreatePing(Pings.SteamID, worldPos, CastUtil.ClosestTransform(worldPos)); 
        }
        #endregion

        #region Ping Creation and Removal
        internal static void CreatePing(CSteamID steamID, Vector3 worldPos, Transform hitTransform)
        {
            if (!hitTransform) return;
            RemovePing(steamID); // Remove existing ping for this player, if any
            
            // Get ping data (name and transform)
            var (pingName, transformForOutline) = PingData.GetFrom(hitTransform, worldPos);

            // Create ping
            var pingUI = Instantiate(_pingPrefab, _canvas.transform);
            pingUI.SetActive(true);
            pingUI.GetComponentInChildren<Text>().text = pingName;
            
            // Add outline to the hit object or return existing outline on that object. Returns null if transform == null, AKA no outline is needed
            var outline = CreateOutline(transformForOutline);
            ActivePings[steamID] = new PingInstance
            {
                HitTransform = /*pingTransform ?? */hitTransform,
                LocalPosition = (/*pingTransform ?? */hitTransform) ? (/*pingTransform ?? */hitTransform).InverseTransformPoint(worldPos) : worldPos,
                UIObject = pingUI,
                SpawnTime = Time.time,
                Outline = outline
            };
        }
        
        private static void RemovePing(CSteamID steamID)
        {
            if (!ActivePings.Remove(steamID, out var ping)) return; // If ping doesn't exist, do nothing
            Destroy(ping.UIObject);
            
            if (ping.Outline && !GetOutlineOfPingFromActive(ping.HitTransform))
                // Since ping is removed from active, #GetOutlineOfPingFromActive will return true only if the outline is still present on other pings
                DestroyImmediate(ping.Outline); // Need to use DestroyImmediate since right after that #CreateOutline will check for outlines
        }
        #endregion

        #region Outlines
        [CanBeNull]
        private static Outline CreateOutline(Transform target)
        {
            if (!target) return null;

            try {
                var outline = GetOutlineOfPingFromActive(target);
                if (outline)
                    return outline; // If outline already exists for this object, return it
            
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
        
        private static Outline GetOutlineOfPingFromActive(Transform pingTransform)
        {
            return ActivePings.FirstOrDefault(pair => pair.Value.HitTransform == pingTransform).Value?.Outline;
        }
        #endregion

        #region Setup and Cleanup
        public static void Setup()
        {
            _canvas = pings.Setup.CreateCanvas();
            _pingPrefab = pings.Setup.CreatePingPrefab();
            pings.Setup.LoadLocalizations();
        }
        
        public static void Cleanup()
        {
            RemoveAllPings();
            if (_canvas) Destroy(_canvas.gameObject);
            if (_pingPrefab) Destroy(_pingPrefab);
        }
        
        public static void RemoveAllPings()
        {
            while (ActivePings.Count > 0)
                RemovePing(ActivePings.First().Key);
        }
        #endregion
    }

    public static class CastUtil
    {
        #region Cone Cast For Pings
        private const int Mask = ~((1 << 1) // Transparent FX
                                   | (1 << 4) // Water
                                   | (1 << 5) // UI
                                   | (1 << 9) // Raft Collision
                                   | (1 << 19) // Remote Player
                                   | (1 << 20) // Local Player
                                   | (1 << 21) // Particles
                                   | (1 << 24)); // Hand Camera

        /// <summary>
        /// Performs a cone cast by doubling radius and distance from start on each iteration, up to 7 times.
        /// </summary>
        /// <param name="ray">Ray</param>
        /// <param name="hit">Hit info (if any)</param>
        /// <returns>True if hit, false otherwise</returns>
        public static bool PingCast(Ray ray, out RaycastHit hit)
        {
            var radius = 0.001f; // Starting radius
            var distanceFromOrigin = 0.15f; // Starting distance
        
            for (var i = 0; i < 7; i++)
            {
                if (Physics.SphereCast(ray.origin + ray.direction * distanceFromOrigin, radius, ray.direction, 
                        out hit, 280f, Mask))
                {
                    if (Pings.DebugMode >= 2)
                        Debug.Log($"[Pings: Raycast] Hit layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)} (#{hit.collider.gameObject.layer})");
                    return true;
                }
                
                radius *= 2f;
                radius += 0.06f; 
                distanceFromOrigin *= 2f;
                distanceFromOrigin += 1f;
            }

            hit = default;
            return false;
        }
        #endregion

        #region Closest Collider at Hit Point
        private static readonly Collider[] Colliders = new Collider[128];
        public static Transform ClosestTransform(Vector3 worldPos)
        {
            const float radius = 0.05f;
            const float maxRadius = 10f; // Maximum search radius
            var s = 1;
            var amount = 0;
            Backsie:
            while (s*radius <= maxRadius) // Limit search radius
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
                var colPath = col.transform.Path();
                if (colPath.Contains("FoundationCollider") && colPath.Contains("_SP_Raft")) continue; // Skip invisible foundation colliders
                var dist = Vector3.Distance(worldPos, col.transform.position);
                if (!(dist < minDist) || col.transform.name.Contains("Player")) continue;
                minDist = dist;
                closest = col.transform;
            }

            if (closest || !(s * radius <= maxRadius)) return closest;
            s *= 2; // Increase the search radius
            goto Backsie; // Try again with a larger radius

        }
        #endregion
    }
}