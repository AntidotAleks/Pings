using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace pings
{
    public static class NameRules
    {
        public static void AddWordRule(params WordRule[] rules)
        {
            WordRules.AddRangeUniqueOnly(rules);
        }
        public static void AddNameRule(params NameRule[] rule)
        {
            GlobalRules.AddRangeUniqueOnly(rule);
        }
        public static void AddWordRuleAt(int index, params WordRule[] rules)
        {
            index = Mathf.Clamp(index, 0, rules.Length - 1);
            WordRules.InsertRange(index, rules);
        }
        public static void AddNameRuleAt(int index, params NameRule[] rule)
        {
            index = Mathf.Clamp(index, 0, rule.Length - 1);
            GlobalRules.InsertRange(index, rule);
        }
        
        
        internal static List<WordRule> WordRules { get; } =
            new List<WordRule>
            {
                new WordRule("ItemCollector", "Hook", 0),
                new WordRule("Pickup_DropBox", "Item", 0),
                new WordRule("QuestItem_DirtPickup", "Dirt", 0),
                new WordRule("QuestItemPickup_Tangaroa_Token", "Token", 0),
                new WordRule("SeagullNest", "Seagull Nest", 0),
                new WordRule("Seagull", -4),
                new WordRule("TradingPost", "Trading Post", -3),
                new WordRule("StoneBird", "Screecher (Stone Bird)", -1), // Birb with name
                new WordRule("SeavineKlump", "Seaweed", -6), // Weird object name
            };
        
        
        internal static List<NameRule> GlobalRules { get; } =
            new List<NameRule>
            {
                new NameRule(
                    transforms => AnyContains(transforms, out _, "PlayerStats", "Network_Player"),
                (transforms, worldPos) =>
                    {
                        AnyContains(transforms, out var index, "PlayerStats", "Network_Player");
                        var name = transforms[index].name;
                        name = name.Substring(name.IndexOf(",", StringComparison.Ordinal) + 1); // Remove "PlayerStats, " or "Network_Player, "
                        return (name.Trim(), null);
                    }
                ),
                
                
                // Quest items
                new NameRule(
                    transforms => NameStartsWith(transforms[0], "QuestItem", "NoteBookPickup"),
                    (transforms, worldPos) =>
                    {
                        var name = transforms[0].name;
                        if (!name.Contains("_"))
                            return (CleanString(name), transforms[0]);
                        var firstIndex = name.IndexOf("_", StringComparison.Ordinal);
                        var secondIndex = name.IndexOf("_", firstIndex + 1, StringComparison.Ordinal) - firstIndex;
                        var thirdIndex = name.LastIndexOf("_", StringComparison.Ordinal) + 1;
                        name = name.Substring(firstIndex, secondIndex) +
                               (name[0] == 'Q' ? " Quest Item: " : " Note Book Page: ") + 
                               name.Substring(thirdIndex);
                        name = name.Replace("TP", "Temperance").Replace("VP", "Varuna Point");
                        return (CleanString(name), transforms[0]);
                    }
                ),
                
                // Terrain
                new NameRule(
                    transforms => NameStartsWith(transforms[0], "Terrain"),
                    (transforms, worldPos) => 
                        (worldPos.y > -2 ? "Island" : "Ocean Floor", null)
                ),
                
                // Hut
                new NameRule(
                    transforms => AnyContains(transforms, out _, "Hut"),
                    (transforms, worldPos) =>
                    {
                        AnyContains(transforms.Reverse().ToArray(), out var index, "Hut");
                        index = transforms.Length - index - 2;
                        if (index < 0 || index >= transforms.Length)
                            return ("Hut", null);
                        var name = transforms[index].name; // Reverse index and -1
                        name = name.Substring(name.IndexOf("_", StringComparison.Ordinal) + 1);
                        name = CleanString(name).Replace("Landmark", "").Trim();
                        name = MoveWordToEnd(name, "Crate");
                        return (MoveWordToStart(name, "Clean"), null);
                    }
                ),
                
                // Islands
                new NameRule(
                    transforms => AnyContains(transforms, out _, "#Landmark_Big#", "#Landmark_Small#"),
                    (transforms, worldPos) =>
                    {
                        AnyContains(transforms, out var index, "#Landmark_");
                        var name = transforms[index-2].name;
                        if (name.Contains("BigRock"))
                            return (worldPos.y > -2 ? "Island" : "Ocean Floor", null);
                        var s = false;
                        if (NameContains(transforms[index - 1], "Big", "Small"))
                        {
                            name = transforms[index - 1].name;
                            if (name[name.Length - 1] == 's')
                                name = name.Substring(0, name.Length - 1);
                            s = true;
                        }
                        name = name.Replace("Pickup", "").Replace("Landmark", "").Replace("OceanBottom", "");
                        name = CleanString(name);
                        return (MoveWordToEnd(name, "Tree", "Flower", "Crate"), (s || name == "Log") ? null : transforms[index-2]);
                    }
                ),
                
                // Raft landmarks
                new NameRule(
                    transforms => transforms.Length >= 2 && NameContains(transforms[transforms.Length-2], "#Landmark_Raft#"),
                    (transforms, worldPos) => 
                        (transforms.Length >= 3 && NameContains(transforms[transforms.Length-3],"CrateRaft")) ? 
                            ("Raft Crate", transforms[transforms.Length-3]) : 
                            ("Forgotten Raft", transforms[transforms.Length-2])
                ),
                
                // Landmark objects
                new NameRule(
                    transforms => AnyContains(transforms, out _, "#Landmark_"),
                    (transforms, worldPos) =>
                    {
                        AnyContains(transforms, out var index, "(Clone)");
                        var name = transforms[index < 0 ? 0 : index].name;
                        if (name.Contains("BigRock"))
                            return (worldPos.y > -2 ? "Island" : "Ocean Floor", null);
                        name = Simplify(transforms, index);
                        name = System.Text.RegularExpressions.Regex.Replace(name, @"\b\w\b\s*", "");
                        name = MoveWordToStart(name, "Small", "Medium", "Big", "Large", "Office", "Good");
                        var show = AnyContains(transforms, out _, "Pickups", "Blueprint", "Crate");
                        return (MoveWordToEnd(name, "Crate", "Blueprint"), show ? transforms[0] : null);

                        string Simplify(Transform[] t, int i)
                        {
                            if (i < 0) i = 0;
                            while (true)
                            {
                                if (i >= transforms.Length) return "Unknown";
                                var n = t[i].name;
                                n = CleanString(n
                                    .Replace("Landmark", "").Replace("Pickup", "").Replace("Land", "")
                                    .Replace("Collider", "").Replace("Collision", "").Replace("Model", "")
                                    .Replace("model", "").Replace("GameObject", "").Replace("Physics", "")
                                    .Replace("AnimatorChild", "").Replace("Mesh", "")
                                    .Replace("Int_", "Interior").Replace("Ext_", "Exterior"));
                                if (n.IsNullOrEmpty())
                                    i += 1;
                                else
                                    return n;
                            }
                        }
                    }
                ),
                
                // Pickup floating
                new NameRule(
                    transforms => NameStartsWith(transforms[0], "Pickup_Floating_"),
                    (transforms, worldPos) =>
                    {
                        var name = transforms[0].name.Substring("Pickup_Floating_".Length);
                        return (CleanString(name), transforms[0]);
                    }
                ),
                
                // AI or Armature
                new NameRule(
                    transforms => AnyContains(transforms, out _,"AI_", "Armature"),
                    (transforms, worldPos) =>
                    {
                        if (!AnyContains(transforms, out var index,"AI_"))
                            return ("Shark", transforms[transforms.Length-1]);
                        
                        var name = transforms[index].name.Substring(3); // Remove "AI_"
                        return (CleanString(name), transforms[index]);
                    }
                ),
                
                // Player's Raft and placeable on raft
                new NameRule(
                    transforms => transforms.Length > 2 && NameStartsWith(transforms[transforms.Length-2], "_SP_Raft"),
                    (transforms, worldPos) =>
                    {
                        if (transforms.Length < 6)
                            return ("Raft", null); // Not enough transforms means not a placeable
                        var t = transforms[transforms.Length-6];
                        if (!NameStartsWith(t, "Placeable_", "MeshPath_"))
                            return ("Raft", null);

                        var name = t.name;
                        const int startIndex = 9; // Skip "Placeable" or "MeshPath_"
                        
                        name = name.Substring(startIndex);
                        name = RemoveAll(name, "Floor", "Stationary", "Two", "CookingStand");
                        name = CleanString(name);
                        return (MoveWordToStart(name, "Wall", "Vertical", "Horizontal", "Advanced", "Wet", "Titanium", "Golden", "Small", "Medium", "Large", "Short", "Long", "Tall", "Round", "Square"), t);
                    }
                ),
                
                // Terrain decorations
                new NameRule(
                    transforms => AnyStartsWith(transforms, out _, "Big", "Small") && 
                                  !AnyStartsWith(transforms, out _, "Pickup_", "Log"),
                    (transforms, worldPos) =>
                    {
                        AnyStartsWith(transforms, out var index, "Big", "Small");
                        var name = transforms[index].name;
                        return (name.Contains("BigRock") ? "Ocean Floor" : CleanString(name), null);
                    }
                ),
            };
        
        
        
        
        
        private static string CleanString(string input)
        {
            input = input.Replace("_", " "); // Replace underscores with spaces
            input = System.Text.RegularExpressions.Regex.Replace(input, "(?<!^)([A-Z])", " $1"); // Insert space before each uppercase letter except the first
            input = System.Text.RegularExpressions.Regex.Replace(input, @"\d+", ""); // Remove numbers
            input = System.Text.RegularExpressions.Regex.Replace(input, @"\(.*?\)", ""); // Remove text with parentheses
            input = System.Text.RegularExpressions.Regex.Replace(input, " +", " "); // Remove extra spaces
            return input.Trim();
        }

        private static bool NameContains(Transform t, params string[] words) => words.Any(word => t.name.Contains(word));
        private static bool NameStartsWith(Transform t, params string[] words) => words.Any(word => t.name.StartsWith(word));
        private static bool NameEndsWith(Transform t, params string[] words) => words.Any(word => t.name.EndsWith(word));
        private static bool NameEquals(Transform t, params string[] words) => words.Any(word => t.name.Equals(word));
        
        internal static bool AnyContains(Transform[] transforms, out int index, params string[] words)
        {
            for (index = 0; index < transforms.Length; index++)
                if (NameContains(transforms[index], words))
                    return true;
            index = -1; // No match found
            return false;
        }
        internal static bool AnyStartsWith(Transform[] transforms, out int index, params string[] words)
        {
            for (index = 0; index < transforms.Length; index++)
                if (NameStartsWith(transforms[index], words))
                    return true;
            index = -1; // No match found
            return false;
        }
        internal static bool AnyEndsWith(Transform[] transforms, out int index, params string[] words)
        {
            for (index = 0; index < transforms.Length; index++)
                if(NameEndsWith(transforms[index], words))
                    return true;
            index = -1; // No match found
            return false;
        }
        internal static bool AnyEquals(Transform[] transforms, out int index, params string[] words)
        {
            for (index = 0; index < transforms.Length; index++)
                if (NameEquals(transforms[index], words))
                    return true;
            index = -1; // No match found
            return false;
        }
        internal static bool AnyRegexMatch(Transform[] transforms, out int index, string pattern)
        {
            for (index = 0; index < transforms.Length; index++)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(transforms[index].name, pattern))
                    return true;
            }
            index = -1; // No match found
            return false;
        }

        private static string MoveWordToEnd(string input, params string[] words)
        {
            foreach (var w in words)
                if (input.StartsWith(w))
                {
                    input = input.Substring(w.Length).Trim() + " " + w;
                    return input;
                }
            return input;
        }
        
        private static string MoveWordToStart(string input, params string[] words)
        {
            foreach (var word in words)
                if (input.EndsWith(word))
                    input = word + " " + input.Substring(0, input.Length - word.Length).Trim();
            return input;
        }
        
        private static string RemoveAll(string input, params string[] words)
        {
            foreach (var word in words)
                input = input.Replace(word, "");
            return input.Trim();
        }
    }
    
    public struct WordRule
    {
        public readonly Func<Transform[], bool> Predicate;
        public readonly (string name, int index) Result;

        public WordRule(string predicateAndResult, int transformIndex)
        {
            Predicate = transforms => NameRules.AnyContains(transforms, out _, predicateAndResult);
            Result = (predicateAndResult, transformIndex);
        }
        public WordRule(string predicate, string result, int transformIndex)
        {
            Predicate = transforms => NameRules.AnyContains(transforms, out _, predicate);
            Result = (result, transformIndex);
        }
    }
    public struct NameRule
    {
        public readonly Func<Transform[], bool> Predicate;
        public readonly Func<Transform[], Vector3, (string, Transform)> Formatter;

        public NameRule(Func<Transform[], bool> predicate, Func<Transform[], Vector3, (string, Transform)> formatter)
        {
            Predicate = predicate;
            Formatter = formatter;
        }
    }
}