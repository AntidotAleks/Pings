using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using cakeslice;
using JetBrains.Annotations;
using Steamworks;
using UnityEngine;
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
            public Transform HitTransform; // Transform of the hit object
            public Vector3 LocalPosition; // Relative position of the ping to the hit object
            public GameObject UIObject;
            public Setup.PingSubObjects SubObjects;
            public float SpawnTime;
            [CanBeNull] public Outline Outline;

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
                var rt = ping.UIObject.transform;
                rt.position = GetPointPosition(ping, out var worldPos, out var direction);
                SetPingShape(ping.SubObjects, direction);

                var distance = Vector3.Distance(Camera.transform.position, worldPos);
                var scale = Mathf.Clamp(1f / distance, 0.1f, 2.5f) * ScaleFactor;
                rt.localScale = Vector3.one * scale;
            }
        }

        #region Ping Position and Shape

        private const int BoundDistance = 170;
        [SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
        private static Vector3 GetPointPosition(PingInstance ping, out Vector3 worldPos, out Vector3 direction)
        {
            worldPos = ping.WorldPosition;
            var pointPos = Camera.WorldToScreenPoint(worldPos);

            // If point is on the screen
            
            var posOnScreen = pointPos;
            posOnScreen.x = Mathf.Clamp(posOnScreen.x, BoundDistance, Screen.width - BoundDistance);
            posOnScreen.y = Mathf.Clamp(posOnScreen.y, BoundDistance, Screen.height - BoundDistance);
            var isOnScreen = pointPos.z > 0 && pointPos.x == posOnScreen.x && pointPos.y == posOnScreen.y;

            ping.SubObjects.textObject.color = new Color(1, 1, 1, isOnScreen ? 1 : 0);
            if (isOnScreen) 
            {
                direction = Vector3.zero;
                return posOnScreen;
            }
            
            if (pointPos.z < 0) // Fix point position if it's behind the camera
            {
                pointPos.x = Screen.width - pointPos.x;
                pointPos.y = Screen.height - pointPos.y;
            }
            
            var screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f);
            pointPos -= screenCenter;

            #region If looking away >90 degrees, move the point to the bottom of the screen

            var angle = Vector3.SignedAngle((worldPos - Camera.transform.position).XZOnly(), Camera.transform.forward.XZOnly(), Vector3.up);
            var delta = Math.Max(Math.Abs(angle) - 90f, 0) / 90; // Value between 90 and 180 degrees away from point to [0, 1]
            delta = Mathf.SmoothStep(0, 1, delta);
            pointPos.Normalize();
            pointPos *= 1-delta;
            pointPos += Quaternion.AngleAxis(angle, Vector3.forward) * new Vector3(0, delta, 0);

            #endregion

            #region Move point to the edge of the screen
            
            var halfWidth = Screen.width / 2f - BoundDistance;
            var halfHeight = Screen.height / 2f - BoundDistance;

            var tx = pointPos.x > 0 ? halfWidth / pointPos.x : -halfWidth / pointPos.x;
            var ty = pointPos.y > 0 ? halfHeight / pointPos.y : -halfHeight / pointPos.y;

            // Use the smallest positive t
            var t = Mathf.Min(
                tx > 0 ? tx : float.MaxValue,
                ty > 0 ? ty : float.MaxValue
            );
            #endregion
            
            direction = pointPos.normalized;
            return screenCenter +  pointPos * t;
        }
        
        private static void SetPingShape(Setup.PingSubObjects subobjects, Vector3 direction)
        {
            var diamond = subobjects.diamondShape;
            var arrow = subobjects.arrowShape;
            var rt = subobjects.rectTransform;

            if (direction == Vector3.zero) // It means the ping is on the screen
            {
                diamond.color = Color.white;
                arrow.color = Color.clear;
                rt.rotation = Quaternion.Euler(Vector3.up);
            }
            else
            {
                diamond.color = Color.clear;
                arrow.color = Color.white;
                var angle = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
                rt.rotation = Quaternion.Euler(0, 0, -angle); // Rotate arrow to point in the direction of the ping
            }
        }

        #endregion
        
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
            var pingObject = Instantiate(_pingPrefab, _canvas.transform);
            var subObjects = pingObject.GetComponent<Setup.PingSubObjects>();
            pingObject.SetActive(true);
            subObjects.textObject.text = pingName;
            
            // Add outline to the hit object or return existing outline on that object. Returns null if transform == null, AKA no outline is needed
            var outline = CreateOutline(transformForOutline);
            ActivePings[steamID] = new PingInstance
            {
                HitTransform = hitTransform,
                LocalPosition = hitTransform ? hitTransform.InverseTransformPoint(worldPos) : worldPos,
                UIObject = pingObject,
                SubObjects = subObjects,
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
                
                outline = Pings.isFancyOutline ? (Outline) 
                    target.gameObject.AddComponent<FancyOutline>() : 
                    target.gameObject.AddComponent<QuickOutline>();
                
                if (outline is QuickOutline qo)
                {
                    qo.OutlineColor = Color.yellow;
                    qo.OutlineWidth = 7f;
                }
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