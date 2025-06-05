using System;
using System.Collections.Generic;
using System.Linq;
using I2.Loc;
using Steamworks;
using UnityEngine;
using UnityEngine.UI;

namespace pings
{
    public class PingManager : MonoBehaviour
    {
        private static Canvas _canvas;
        private static GameObject _pingPrefab;
        
        private static LanguageSourceData _lang;
        private static string TermPing => _lang.GetTranslation("pings/ping");
        
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
            var path = Path(transform);
            if (Pings.DebugMode)
                Debug.Log($"[Pings: Handling] Ping at {worldPos} on path {path}");
            
            if (path.Contains("Armature") && !path.Contains("AI") && !path.Contains("Seagull"))
                return ("Bruce the Shark", transform.root); // If it works, it works
            
            if ((path.Contains("Terrain") || path.Contains("BigRock")) && !path.Contains("Shark"))
                return (worldPos.y > -2 ? "Island" : "Ocean Floor", null);
                
            string str;
            (transform, str) = GetDataByType(transform);
            if (str != null)
                return (str, transform); // If we found a specific type, return it
            
            if (Pings.DebugMode)
                Debug.Log(" - No specific type found for path, using default ping.");
            return (TermPing, transform);
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
            _lang = pings.Setup.LoadLocalizations();
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

        private static string Path(Transform transform)
        {
            if (!transform) return string.Empty;
            return (transform.parent ? Path(transform.parent)+"/" : "") + transform.name;
        }

        // TODO: QuestInteractable 
        public static Dictionary<Type, Func<List<Transform>, int, Component, (Transform, string)>> DataByType = 
                   new Dictionary<Type, Func<List<Transform>, int, Component, (Transform, string)>>
        {
            { typeof(Block), (transformsList, i, c) 
                => (transformsList[i], BlockName((Block)c)) },
            
            { typeof(PickupItem), (transformsList, i, c) 
                => {
                var t = transformsList[i];
                var item = (PickupItem)c;
                var pickupName = item.PickupName;
                switch (item.pickupItemType)
                {
                    case PickupItemType.QuestItem:
                        pickupName = "Quest Item: " + pickupName;
                        break;
                    case PickupItemType.NoteBookNote:
                        pickupName = "Note: " + CleanString(t.name.Substring(t.name.LastIndexOf('_') + 1));
                        break;
                }
                return (t, pickupName);
            }},
            
            { typeof(AI_StateMachine), (transformsList, i, c) 
                => {
                    var t = transformsList[0].root.GetComponent<AI_StateMachine_Shark>()?.trackedRotational?.transform; // Shark outline appears on different transform
                    if (!t) t = transformsList[i];
                    return (t, AIName(c));
                }},
            
            { typeof(AI_Sub), (transformsList, i, c) 
                => (transformsList[i], AIName(c)) },
            
            { typeof(Seagull), (transformsList, i, c) 
                => (transformsList[i], _lang.GetTranslation("Item/Seagull")) },
            
            { typeof(Landmark), (transformsList, i, c) 
                => {
                    var tLandmark = transformsList[Math.Max(i - 4, 0)]; // Min(index + 4, tList.Count - 1) if tList is reversed
                    var name = CleanString(tLandmark.name).Replace("Character Unlock ", "Character Unlock: ");
                    return (name.Contains("Character Unlock") ? tLandmark : null, name);
                }}
        };
        
        private static (Transform, string) GetDataByType(Transform transform)
        {
            if (!transform) return (null, null);
            var tList = new List<Transform>();
            for (var t = transform; t; t = t.parent)
                tList.Add(t);
            // tList.Reverse();

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
        
        private static string AIName(Component ai)
        {
            var term = ai?.name;
            if (term.IsNullOrEmpty())
                return TermPing; // If name is not found, return default ping name
            
            var startIndex = term.Contains("Sub_") ? 7 : 3; // If it's a Sub AI, skip "AI_Sub_" prefix, otherwise skip "AI_"
            var endIndex = term.IndexOf("(", StringComparison.Ordinal);
            term = (endIndex < 0 ? term.Substring(startIndex)
                    : term.Substring(startIndex, endIndex - startIndex))
                .Trim();
            switch (term)
            {
                case "Shark":
                    return "Bruce the Shark";
                case "StoneBird":
                    return "Screecher";
                case "StoneBird_Caravan":
                    return "White Screecher";
                case "Boar":
                    return "Warthog";
                default:
                    return CleanString(term);
            }
        }
        private static string BlockName(Block block)
        {
            var term = block.buildableItem.settings_Inventory.LocalizationTerm;
            term = _lang.GetTranslation(term);
            if (term.IsNullOrEmpty())
                return TermPing; // If translation is not found, return default ping name
                    
            var descIndex = term.IndexOf("@", StringComparison.Ordinal);
            if (descIndex >= 0)
                term = term.Substring(0, descIndex).Trim(); // Remove description part if exists
            
            return term;
        }
        private static string CleanString(string input)
        {
            input = input.Replace("_", " "); // Replace underscores with spaces
            input = System.Text.RegularExpressions.Regex.Replace(input, "(?<!^)([A-Z])", " $1"); // Insert space before each uppercase letter except the first
            input = System.Text.RegularExpressions.Regex.Replace(input, @"\d+", ""); // Remove numbers
            input = System.Text.RegularExpressions.Regex.Replace(input, @"\(.*?\)", ""); // Remove text with parentheses
            input = System.Text.RegularExpressions.Regex.Replace(input, " +", " "); // Remove extra spaces
            return input.Trim();
        }
    }
}