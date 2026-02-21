using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using HMLLibrary;
using pings.outlines;
using RaftModLoader;
using Steamworks;
using UnityEngine;

namespace pings
{
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class Pings : Mod
    {
        // Mod information
        internal static Mod mod;
        internal const int ModChannel = 4571607; // Channel for mod messages
        internal static CSteamID SteamID => RAPI.GetLocalPlayer().steamID;
        private static Camera _camera;
        public static Camera Camera => _camera ? _camera : (_camera = Camera.main ?? Camera.current);

        #region Mod Loading / Unloading
        public IEnumerator Start()
        {
            mod = this;
            
            yield return Setup.LoadAssets();
            Networking.OnLoad();
            PingManager.OnLoad();
            
            Log("Mod Pings is loaded!");
        }

        public void OnModUnload()
        {

            Networking.OnUnload();
            PingManager.OnUnload();
            Setup.UnloadAssets();
            
            Log("Mod Pings is unloaded.");
        }
        #endregion

        #region Translation Commands
        [ConsoleCommand(name: "PingsTranslateSearch", docs: "Searches for terms in the translation file.")]
        public static string TranslateSearch(string[] args)
        {
            if (args.Length == 0)
                TranslationCheck.TermTreeList();
            else
                TranslationCheck.TermSearch(args[0]);
            return null;
        }
        
        [ConsoleCommand(name: "PingsTranslate", docs: "Translates a term from the translation file.")]
        public static string Translate(string[] args)
        {
            if (args.Length == 0)
                return "Usage: translate <term>";
            TranslationCheck.Translate(args[0]);
            return null;
        }
        #endregion
        
        #region Is mod enabled
        private static bool _hasPingsMod;
        internal static bool HasPingsMod
        {
            get => _hasPingsMod;
            set
            {
                if (_hasPingsMod == value) return;
                _hasPingsMod = value;

                if (!value) PingManager.RemoveAllPings();
            }
        }

        #endregion

        #region PingManager and Networking
        public void Update() => PingManager.Update();
        public void FixedUpdate() => Networking.CheckMessages();
        public override void WorldEvent_WorldLoaded() => Networking.OnLoad();
        public override void WorldEvent_WorldUnloaded() => Networking.OnUnload();
        
        #endregion

        #region Settings // ExtraSettingsAPI integration
        
        public static float[] PingDurationValues { get; } = { 3,4,5,6,7,8,9,10,12,15,20,30,45,60,float.PositiveInfinity};
        
        // Control
        public static Keybind PingKey { get; private set; } = new Keybind("pingKeybind", KeyCode.Mouse2);
        public static Keybind ClearAllPingsKey { get; private set; } = new Keybind("clearPingsKeybind", KeyCode.None);
        public static Keybind RemoveClosestPingKey { get; private set; } = new Keybind("removeClosestPingKeybind", KeyCode.None);
        
        public static float PingDuration { get; private set; } = 10f;
        public static int maxPingsPerPlayer = 1;
        // Visual
        public static OutlineStyle Style = OutlineStyle.Quick;
        public static bool UniquePingColors;
        public static Color PingColor = Color.yellow;
        public static bool HasOwnPingColor;
        public static Color OwnPingColor = Color.gray;

        public static float OutlineThicknessMultiplier = 1f;
        
        public static bool ShowEdgePingAsArrow = true;
        // Debug
        public static int DebugMode { get; private set; }
        

        public void ExtraSettingsAPI_Load() => Load_ExtraSettingsAPI_Settings(); 
        public void ExtraSettingsAPI_SettingsClose() => Load_ExtraSettingsAPI_Settings();
        private static void Load_ExtraSettingsAPI_Settings()
        {
            // Control
            PingKey = ExtraSettingsAPI_GetKeybind("pingKeybind");
            ClearAllPingsKey = ExtraSettingsAPI_GetKeybind("clearPingsKeybind");
            RemoveClosestPingKey = ExtraSettingsAPI_GetKeybind("removeClosestPingKeybind");
            PingDuration = PingDurationValues[Clamp((int) Math.Round(ExtraSettingsAPI_GetSliderValue("pingDuration")), 0, PingDurationValues.Length - 1)];
            maxPingsPerPlayer = (int) ExtraSettingsAPI_GetSliderValue("maxPingsPerPlayer");
            if (maxPingsPerPlayer == 11) // Unlimited
                maxPingsPerPlayer = int.MaxValue;
            // Visual
            ShowEdgePingAsArrow = ExtraSettingsAPI_GetCheckboxState("showEdgePingAsArrow");
            
            var prevStyle = Style;
            Style = (OutlineStyle) ExtraSettingsAPI_GetComboboxSelectedIndex("outlineStyle");
            if (prevStyle != Style) OutlineTools.UpdateOutlinesStyle();
            
            UniquePingColors = ExtraSettingsAPI_GetCheckboxState("uniquePingColors");
            PingColor = (Style == OutlineStyle.Quick || !UniquePingColors) ? HexToColor(ExtraSettingsAPI_GetInputValue("colorDefault")) : Color.white;
            HasOwnPingColor = ExtraSettingsAPI_GetCheckboxState("separateOwnPingColor");
            OwnPingColor = HasOwnPingColor ? HexToColor(ExtraSettingsAPI_GetInputValue("colorOwn")) : Color.white;
            OutlineThicknessMultiplier = ExtraSettingsAPI_GetSliderValue("outlineThickness");
            
            // Debug
            DebugMode = ExtraSettingsAPI_GetComboboxSelectedIndex("debugMode");
        }

        private static void ExtraSettingsAPI_Unload() { DebugMode = 0; }
        
        private static string ExtraSettingsAPI_HandleSliderText(string settingName, float value)
        {
            switch (settingName)
            {
                case "pingDuration":
                    int index = Clamp(
                        (int) Math.Round(ExtraSettingsAPI_GetSliderValue("pingDuration")), 
                        0, PingDurationValues.Length-1);
                    if (index == PingDurationValues.Length-1)
                        return "Infinite";
                    return PingDurationValues[index] + " seconds";
                case "maxPingsPerPlayer":
                    int amount = (int)Math.Round(ExtraSettingsAPI_GetSliderValue("maxPingsPerPlayer"));
                    return amount == 11 ? "Unlimited" : amount.ToString();
                default:
                    return "idk tbh";
            }
        }
        
        private static bool ExtraSettingsAPI_HandleSettingVisible(string settingName, bool isInWorld)
        {
            if (Style == OutlineStyle.Disabled) return false;
            switch (settingName)
            {
                case "outlineThickness":
                    return true;
                case "uniquePingColors":
                case "separateOwnPingColor":
                    return Style != OutlineStyle.Quick;
                case "colorDefault":
                    return Style == OutlineStyle.Quick || !UniquePingColors;
                case "colorOwn":
                    return HasOwnPingColor && Style != OutlineStyle.Quick;
                default:
                    return false;
            }
        }
        
        private static void ExtraSettingsAPI_ButtonPress(string settingName)
        {
            Debug.Log($"Button \"{settingName}\" was clicked");
        }
        
        // Overridden by ExtraSettingsAPI
        [MethodImpl(MethodImplOptions.NoInlining)] public static Keybind ExtraSettingsAPI_GetKeybind(string SettingName) => null;
        [MethodImpl(MethodImplOptions.NoInlining)] public static float ExtraSettingsAPI_GetSliderValue(string SettingName) => 0;
        [MethodImpl(MethodImplOptions.NoInlining)] public static int ExtraSettingsAPI_GetComboboxSelectedIndex(string SettingName) => -1;
        [MethodImpl(MethodImplOptions.NoInlining)] public static bool ExtraSettingsAPI_GetCheckboxState(string settingName) => false;
        [MethodImpl(MethodImplOptions.NoInlining)] public static string ExtraSettingsAPI_GetInputValue(string settingName) => null;
        // [MethodImpl(MethodImplOptions.NoInlining)] public static void ExtraSettingsAPI_CheckSettingVisibility() { }
        
        // Update settings menu
        private static readonly Settings settings = ComponentManager<Settings>.Value;
        public static string UpdateMenu
        { get => null; set {
                // ExtraSettingsAPI_CheckSettingVisibility();
                if (!settings || !settings.IsOpen) return;
                settings.Close();
                settings.Open();
        } }
        

        private static int Clamp(int val, int min, int max) => val<min?min : val>max?max : val;
        
        #endregion

        #region Utils
        
        private static Color HexToColor(string hex)
        {
            Debug.Log("Parsing color from hex: " + hex);
            hex = hex.Replace("#", "").Trim();
            switch (hex.Length)
            {
                case 3: hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}FF"; /* #RGB -> #RRGGBBFF */ break;
                case 4: hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}{hex[3]}{hex[3]}"; /* #RGBA -> #RRGGBBAA */ break;
                case 6: hex += "FF"; /* #RRGGBB -> #RRGGBBAA */ break;
            }
            if (hex.Length != 8 || !uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out uint hexVal)) return Color.white;

            Color color = new Color32(
                (byte)(hexVal >> 24),
                (byte)((hexVal >> 16) & 0xFF),
                (byte)((hexVal >> 8) & 0xFF),
                (byte)(hexVal & 0xFF));
            Debug.Log($"Color: R:{color.r} G:{color.g} B:{color.b} A:{color.a}");
            return color;
        }

        #endregion
    }

    public enum OutlineStyle
    {
        Disabled,
        Quick,
        Fancy
    }
}
