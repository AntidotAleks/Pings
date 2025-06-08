using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using I2.Loc;
using Steamworks;
using UnityEngine;
using UnityEngine.Assertions.Must;
using UnityEngine.UI;

namespace pings
{
    public class PingManager : MonoBehaviour
    {
        #region Variables
        private static Canvas _canvas;
        private static GameObject _pingPrefab;
        
        private static string TermPing => Translate("ModPings/Ping");
        
        private static Camera Camera => Camera.main ?? Camera.current;
        private static readonly Dictionary<CSteamID, PingInstance> ActivePings = new Dictionary<CSteamID, PingInstance>();

        private const float ScaleFactor = 10f;
        #endregion
        internal void Update()
        {
            if (!RAPI.IsCurrentSceneGame()) return;

            #region Remove Old And Move Pings
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
            #endregion

            #region Create Ping on Key Press
            if (!Input.GetKeyDown(Pings.PingKey.MainKey) && !Input.GetKeyDown(Pings.PingKey.AltKey)) return;
            if (CanvasHelper.ActiveMenu != MenuType.None) return;
            
            var ray = Camera.ScreenPointToRay(Input.mousePosition);
            if (!CastUtil.PingCast(ray, out var hit)) return;
            
            var p = new PingMessage(hit.point, Pings.SteamID);
            RAPI.SendNetworkMessage(p, Pings.ModChannel); // Send ping to other players
            CreatePing(Pings.SteamID, hit.point, CastUtil.ClosestCollider(hit.point)); 
            // Using the closest collider instead of hit.transform, so pings between players are more consistent
            #endregion
        }

        #region Ping Instance
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
        #endregion

        #region Ping Creation and Removal
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
            
            if (ping.Outline && !GetOutlineOfPingFromActive(ping.HitTransform))
                // Since ping is removed from active, #GetOutlineOfPingFromActive will return true only if the outline is still present on other pings
                DestroyImmediate(ping.Outline); // Need to use DestroyImmediate since right after #CreateOutline will check for outlines
        }

        private static readonly StringBuilder Builder = new StringBuilder();
        // ReSharper disable Unity.PerformanceAnalysis
        private static (string, Transform) GetPingData(Transform transform, Vector3 worldPos)
        {
            var path = Path(transform);
            if (Pings.DebugMode >= 1)
                Debug.Log($"[Pings: Handling] Ping at {worldPos}" + (Pings.DebugMode == 1 ? $" on path {path}":". Ping path and components (from top to root):"));
            
            if (Pings.DebugMode == 2)
            {
                var t = transform;
                while (t)
                {
                    var components = t.GetComponents<Component>();
                    var cs = string.Join(", ", components.Where(c => c && !(c is Transform))
                        .Select(c => c.GetType().Name + (c is MonoBehaviour ? "(+)" : "")));
                    Builder.AppendLine(cs.IsNullOrEmpty() ? $" -       {t.name}" : $" - On {t.name}: {cs}");
                
                    t = t.parent;
                }
                Debug.Log(Builder.Remove(Builder.Length-1, 1).ToString());
                Builder.Clear();
            }
            if (path.StartsWith("ArmatureParent/Armature/Root") && !path.Contains("AI") && !path.Contains("Seagull"))
                return (Translate("ModPings/Animal/Shark"), transform.root); // If it works, it works
            
            if ((path.Contains("Terrain") || path.Contains("BigRock")) && !path.Contains("Shark"))
                return (worldPos.y > -2 ? Translate("ModPings/Landmark/Island") : Translate("ModPings/Landmark/OceanFloor"), null);
                
            string str;
            (transform, str) = GetDataByType(transform);
            if (str != null)
                return (str, transform); // If we found a specific type, return it
            
            if (Pings.DebugMode >= 1)
                Debug.Log("No specific type found for path, using default ping.");
            return (TermPing, transform);
        }
        #endregion

        #region Outlines
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

        #region Ping Data by Type

        #region Dictionary
        public static Dictionary<Type, Func<List<Transform>, int, Component, (Transform, string)>> DataByType = 
            new Dictionary<Type, Func<List<Transform>, int, Component, (Transform, string)>>
        #endregion
        {
            // Raft
            #region Block
            { typeof(Block), (transformsList, i, c) 
                => (transformsList[i], BlockName((Block)c)) },
            #endregion
            // Living entities
            #region AI_StateMachine
            { typeof(AI_StateMachine), (transformsList, i, c) 
                => {
                var t = transformsList[0].root.GetComponent<AI_StateMachine_Shark>()?.trackedRotational?.transform; // Shark outline appears on different transform
                if (!t) t = transformsList[i];
                return (t, AIName(c));
            }},
            #endregion
            #region AI_Sub
            { typeof(AI_Sub), (transformsList, i, c) 
                => (transformsList[i], AIName(c)) },
            #endregion
            #region Seagull
            { typeof(Seagull), (transformsList, i, c) 
                => (transformsList[i], Translate("Item/Seagull")) },
            #endregion
            // Pickups, quest items and interactable
            #region HarvestableTree
            { typeof(HarvestableTree), (transformsList, i, c)
                => {
                var t = transformsList[i];
                var key = KeyString(c.name.Substring("Pickup_Landmark_".Length)); // Remove "Pickup_Landmark_" prefix
                if (TryTranslate("ModPings/Trees/" + key, out var output))
                    return (t, output);
                if (Pings.DebugMode >= 1)
                    Debug.Log($"[Pings: Localization] No translation found for HarvestableTree using key ModPings/Trees/{key}");
                return (t, KeyToCleanString(key));
            }},
            #endregion
            #region PickupItem
            { typeof(PickupItem), (transformsList, i, c) 
                => {
                var t = transformsList[i];
                var item = (PickupItem)c;
                var pickupName = item.PickupName;
                switch (item.pickupItemType)
                {
                    case PickupItemType.QuestItem:
                        pickupName = Translate("ModPings/Substring/QuestItem") + pickupName;
                        break;
                    case PickupItemType.NoteBookNote:
                        var tName = item.name;
                        pickupName = Translate("ModPings/Substring/Note") + CleanString(t.name.Substring(t.name.LastIndexOf('_') + 1));
                        break;
                }
                return (t, pickupName);
            }},
            #endregion
            #region QuestInteractable
            { typeof(QuestInteractable), (transformsList, i, c) 
                => {
                var t = transformsList[i];
                var q = (QuestInteractable)c;
                var req = q.GetRequirementText();
                // return (t, Translate("Game/Requires") + questInteractable.GetRequirementText());
                string key;
                if (TryTranslate("ModPings/QuestInteractable/" +
                                 (key = KeyString(q.name.Replace("Quest", "").Replace("Interactable", ""))), 
                        out var output)) return (t, output);
                if (Pings.DebugMode >= 1)
                    Debug.Log($"[Pings: Localization] No translation found for QuestInteractable using key ModPings/QuestInteractable/{key}");
                return (t, KeyToCleanString(key));
            }},
            #endregion
            // Islands and everything on them
            #region TradingPost
            { typeof(TradingPost), (transformsList, i, c)
                => {
                var t = transformsList[i];
                return (t, Translate("Game/TradingPost"));
            }},
            #endregion
            #region Landmark
            { typeof(Landmark), (transformsList, i, c) 
                => {
                var t = transformsList[Math.Max(i - 4, 0)];
                var baseName = transformsList[i].name;
                var path = Path(transformsList[0]);
                if (path.Contains("/Log"))
                    return (t,Translate("ModPings/Trees/Log")); // I'd move it to localization aliases, but if it works, it works
                
                baseName = baseName.Substring(baseName.IndexOf('_') + 1);
                baseName = Regex.Replace(baseName, @"\d+", ""); // Remove numbers
                if (baseName == "Raft#Floating raft")
                    return (t, Translate("ModPings/AbandonedRaft"));
                
                if (Pings.DebugMode >= 2)
                    Debug.Log($"[Pings: Localization] Landmark \"{baseName}\" is {(LandmarkDictionary.ContainsKey(baseName)?"":"not ")}in the dictionary");
                
                if (!LandmarkDictionary.TryGetValue(baseName, out var landmarkData) &&
                    !baseName.Contains("Small") && !baseName.Contains("Big")) return (null, CleanString(t.name));
                
                if (landmarkData == default) // If Landmark is Small or Big island
                    landmarkData = (baseName.Contains("Small") ? "Small" : "Big", 1);
                    
                if (TryDeepTranslate("ModPings/Landmark/" + landmarkData.Name, transformsList,
                        i - landmarkData.Offset, out var output))
                    return (null, output); // If translation is found
                    
                if (Pings.DebugMode >= 1)
                    Debug.Log($"[Pings: Localization] No translation found for {landmarkData.Name} Landmark using key ModPings/Landmark/{landmarkData.Name}/" +
                              $"{string.Join("/", transformsList.Take(i - landmarkData.Offset + 1).Reverse().Select(tr => KeyString(tr.name)).ToList())} or its parents");

                return (null, CleanString(t.name));
            }}
            #endregion
        };

        #region Landmark Dictionary
        private static readonly Dictionary<string, (string Name, int Offset)> LandmarkDictionary = new Dictionary<string, (string, int)>
        {
            {"Small#", ("Small", 1)},
            {"BalboaIsland", ("Balboa", 2)},
            {"Radar#Big radio tower", ("RadioTower", 1)},
            {"Vasagatan", ("Vasagatan", 1)},
            {"VarunaPoint#", ("VarunaPoint", 1)},
            {"Temperance#", ("Temperance", 1)},
            {"Utopia#", ("Utopia", 1)},
            {"CaravanIsland#RealDeal", ("Caravan", 1)},
            {"Tangaroa#", ("Tangaroa", 1)}
        };
        #endregion

        #region Dictionary Reading Method
        private static (Transform, string) GetDataByType(Transform transform)
        {
            if (!transform) return (null, null);
            var tList = new List<Transform>();
            for (var t = transform; t; t = t.parent)
                tList.Add(t);

            foreach (var type in DataByType.Keys)
            {
                var index = 0;
                foreach (var component in tList.Select(t => t.GetComponent(type)))
                {
                    if (component) 
                        return DataByType[type](tList, index, component);
                    index++;
                }
            }
            
            return (transform, null); 
        }
        #endregion

        #region Localization Helpers
        
        private static string AIName(Component ai)
        {
            var term = ai?.name;
            if (term.IsNullOrEmpty())
                return TermPing; // If name is not found, return default ping name
            
            // ReSharper disable once PossibleNullReferenceException
            var startIndex = term.Contains("Sub_") ? 7 : 3; // If it's a Sub AI, skip "AI_Sub_" prefix, otherwise skip "AI_"
            var endIndex = term.IndexOf("(", StringComparison.Ordinal);
            term = (endIndex < 0 ? term.Substring(startIndex)
                    : term.Substring(startIndex, endIndex - startIndex))
                .Trim();
            switch (term)
            {
                // Items
                case "Boar": return Translate("Item/Boar");
                case "StoneBird": return Translate("Item/Screecher");
                // Figurines
                case "Chicken": return NoDesc(Translate("Block/Figurine_Chicken"));
                case "Llama": return NoDesc(Translate("Block/Figurine_Llama"));
                case "Goat": return NoDesc(Translate("Block/Figurine_Goat"));
                // Custom
                default:
                    return LocalizationManager.TryGetTranslation("ModPings/Animal/"+term, out var translation) ? 
                        translation : // If translation is found, return it
                        CleanString(term); // If translation is not found, return cleaned term
            }
        }
        private static string BlockName(Block block)
        {
            var term = block.buildableItem.settings_Inventory.LocalizationTerm;
            term = Translate(term);
            return term.IsNullOrEmpty() ? TermPing : NoDesc(term);
        }
        
        #endregion
        
        #endregion

        #region Utils

        private static string Translate(string input)
        {
            var output = LocalizationManager.GetTranslation(input);
            return !string.IsNullOrWhiteSpace(output) ? output : 
                LocalizationManager.GetTranslation(LocalizationManager.GetTermData(input)?.Description);
        }

        private static bool TryTranslate(string input, out string output) => !string.IsNullOrEmpty(output = Translate(input));

        private static bool TryDeepTranslate(string input, List<Transform> tList, int start, out string output)
        {
            // Get elements from 0 to start index
            var parts = tList
                .Take(start + 1).Reverse()
                .Select(t => "/"+KeyString(t.name))
                .ToList();
            while (parts.Count > 0)
            {
                if (TryTranslate(input + string.Join("", parts), out output))
                    return true;
                // Remove last part and try again
                parts.RemoveAt(parts.Count - 1);
            }
            output = null;
            return false;
        }

        private static string KeyString(string input)
        {
            input = Regex.Replace(input, @"\d+", ""); // Remove numbers
            input = Regex.Replace(input, @"\(.*?\)", ""); // Remove text with parentheses
            return input.Replace("_", "").Replace(" ", ""); // Remove all spaces and underscores
        }
        
        private static string KeyToCleanString(string input)
        {
            return Regex.Replace(input, "(?<!^)([A-Z])", " $1"); // Insert space before each uppercase letter except the first
        }
        
        private static string CleanString(string input) => KeyToCleanString(KeyString(input));

        internal static string Path(Transform transform)
        {
            if (!transform) return string.Empty;
            return (transform.parent ? Path(transform.parent)+"/" : "") + transform.name;
        }

        private static string NoDesc(string input)
        {
            var descIndex = input.IndexOf("@", StringComparison.Ordinal);
            return descIndex >= 0 ? input.Substring(0, descIndex).Trim() : input.Trim();
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
        public static Transform ClosestCollider(Vector3 worldPos)
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
                var colPath = PingManager.Path(col.transform);
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