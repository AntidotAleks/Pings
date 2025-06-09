using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using I2.Loc;
using UnityEngine;

namespace pings
{
    public static class PingData
    {
        private static string TermPing => Translate("ModPings/Ping"); // Default ping term
        private static readonly StringBuilder Builder = new StringBuilder(); // For debugging output

        public static (string, Transform) GetFrom(Transform transform, Vector3 worldPos)
        {
            var path = transform.Path();

            #region Debugging
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
            #endregion

            #region Return ping data by path
            // Shark ping
            if (path.StartsWith("ArmatureParent/Armature/Root") && !path.Contains("AI") && !path.Contains("Seagull"))
                return (Translate("ModPings/Animal/Shark"), transform.root); // If it works, it works
            
            // Island and underwater ping
            if ((path.Contains("Terrain") || path.Contains("BigRock")) && !path.Contains("Shark"))
                return (worldPos.y > -2 ? Translate("ModPings/Landmark/Island") : Translate("ModPings/Landmark/OceanFloor"), null);
            #endregion

            #region Return ping data by object type
            string str;
            (transform, str) = GetDataByType(transform);
            if (str != null)
                return (str, transform); // If we found a specific type, return it
            
            if (Pings.DebugMode >= 1)
                Debug.Log("No specific type found for path, using default ping.");
            #endregion
            
            return (TermPing, transform);
        }
        
        #region Ping Data by Object Type

        #region Dictionary
        public static Dictionary<Type, Func<List<Transform>, int, Component, (Transform, string)>> DataByType = 
            new Dictionary<Type, Func<List<Transform>, int, Component, (Transform, string)>>
        #endregion
        {
            // Raft
            #region Block
            { typeof(Block), (transformsList, i, c) 
                => (transformsList[i], NameFromBlock((Block)c)) },
            #endregion
            // Living entities
            #region AI_StateMachine
            { typeof(AI_StateMachine), (transformsList, i, c) 
                => {
                var t = transformsList[0].root.GetComponent<AI_StateMachine_Shark>()?.trackedRotational?.transform; // Shark outline appears on different transform
                if (!t) t = transformsList[i];
                return (t, NameFromAI(c));
            }},
            #endregion
            #region AI_Sub
            { typeof(AI_Sub), (transformsList, i, c) 
                => (transformsList[i], NameFromAI(c)) },
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
                        var itemName = t.name.Substring(t.name.LastIndexOf('_') + 1);
                        if (TryTranslate("ModPings/Notes/" + itemName, out var noteOutput))
                            return (t, Translate("ModPings/Substring/Note")+noteOutput);
                        
                        if (Pings.DebugMode >= 1)
                            Debug.Log($"[Pings: Localization] No translation found for Note using key ModPings/Notes/{itemName}");
                        
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
                var landmarkName = transformsList[i].name;
                var path = transformsList[0].Path();
                if (path.Contains("/Log"))
                    return (t,Translate("ModPings/Trees/Log")); // I'd move it to localization aliases, but if it works, it works
                
                landmarkName = landmarkName.Substring(landmarkName.IndexOf('_') + 1);
                landmarkName = Regex.Replace(landmarkName, @"\d+", ""); // Remove numbers

                switch (landmarkName)
                {
                    // Floating Raft
                    case "Raft#Floating raft":
                        return (t, Translate("ModPings/AbandonedRaft"));
                    default:
                        return (null, NameFromLandmark(landmarkName, transformsList, i) ?? CleanString(t.name));
                }
            }}
            #endregion
        };

        #region Landmark Dictionary
        private static readonly Dictionary<string, (string Name, int Offset)> LandmarkDictionary = new Dictionary<string, (string, int)>
        {
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
                foreach (var component in tList.Select(t => t.GetComponent(type))) // Get transform has component of type
                {
                    if (component) // If component exists
                        return DataByType[type](tList, index, component); // Get data using that component
                    index++; // Otherwise, check next
                }
            }
            
            return (transform, null); 
        }
        #endregion

        #region Localization for certain components
        private static string NameFromAI(Component ai)
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
                case "Chicken": return NoDescription(Translate("Block/Figurine_Chicken"));
                case "Llama": return NoDescription(Translate("Block/Figurine_Llama"));
                case "Goat": return NoDescription(Translate("Block/Figurine_Goat"));
                // Custom
                default:
                    return LocalizationManager.TryGetTranslation("ModPings/Animal/"+term, out var translation) ? 
                        translation : // If translation is found, return it
                        CleanString(term); // If translation is not found, return cleaned term
            }
        }
        private static string NameFromBlock(Block block)
        {
            var term = block.buildableItem.settings_Inventory.LocalizationTerm;
            term = Translate(term);
            return term.IsNullOrEmpty() ? TermPing : NoDescription(term);
        }
        private static string NameFromLandmark(string landmarkName, List<Transform> transformsList, int index)
        {
            // If landmark is not in the dictionary and is not Small or Big island
            if (!LandmarkDictionary.TryGetValue(landmarkName, out var landmarkData) &&
                !landmarkName.Contains("Small") && !landmarkName.Contains("Big"))
            {
                if (Pings.DebugMode >= 2)
                    Debug.Log($"[Pings: Localization] Landmark \"{landmarkName}\" is not in the dictionary");
                return null;
            }
                
            if (landmarkData == default) // If Landmark is Small or Big island
                landmarkData = (landmarkName.Contains("Small") ? "Small" : "Big", 1);
                    
            // Try to translate using the landmark name
            if (TryDeepTranslate("ModPings/Landmark/" + landmarkData.Name, transformsList,
                    index - landmarkData.Offset, out var output))
                return output; // If translation is found
                    
            // If no translation is found
            if (Pings.DebugMode >= 1)
                Debug.Log($"[Pings: Localization] No translation found for {landmarkData.Name} Landmark using key ModPings/Landmark/{landmarkData.Name}/" +
                          $"{string.Join("/", transformsList.Take(index - landmarkData.Offset + 1).Reverse().Select(tr => KeyString(tr.name)).ToList())} or its parents");
            return null;
            
        }
        #endregion
        
        #endregion

        #region Translation Methods
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
        #endregion

        #region Utils
        
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

        internal static string Path(this Transform transform)
        {
            if (!transform) return string.Empty;
            return (transform.parent ? transform.parent.Path()+"/" : "") + transform.name;
        }

        private static string NoDescription(string input)
        {
            var descIndex = input.IndexOf("@", StringComparison.Ordinal);
            return descIndex >= 0 ? input.Substring(0, descIndex).Trim() : input.Trim();
        }

        #endregion
    }
}