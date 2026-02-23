using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using HMLLibrary;
using pings.outlines;
using RaftModLoader;
using UnityEngine;
using UnityEngine.UI;

namespace pings
{
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class Pings : Mod
    {
        // Mod information
        internal static Mod mod;
        internal const int ModChannel = 4571607; // Channel for mod messages
        internal static UserID CurrentUserID => new UserID(RAPI.GetLocalPlayer().steamID);
        private static Camera _camera;
        public static Camera Camera => _camera ? _camera : (_camera = Camera.main ?? Camera.current);

        #region Mod Loading / Unloading

        public IEnumerator Start()
        {
            mod = this;

            MyInput.Keybinds.TryAdd("pings.Pings.pingKeybind", new Keybind("pings.Pings.pingKeybind", KeyCode.Mouse2));
            MyInput.Keybinds.TryAdd("pings.Pings.removeClosestPingKeybind",
                new Keybind("pings.Pings.removeClosestPingKeybind", KeyCode.None));
            MyInput.Keybinds.TryAdd("pings.Pings.clearPingsKeybind",
                new Keybind("pings.Pings.clearPingsKeybind", KeyCode.None));

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


        // Control

        public static string PingKey { get; set; } = "pings.Pings.pingKeybind";
        public static string RemoveClosestPingKey { get; set; } = "pings.Pings.removeClosestPingKeybind";
        public static string RemoveAllPingsKey { get; set; } = "pings.Pings.clearPingsKeybind";
        public static int PingDuration { get => _pingDuration; private set => _pingDuration = PingDurationValues[Clamp(value, 0, PingDurationValues.Length - 1)]; }
        /**/ private static int _pingDuration = 10;
        /**/ public static int[] PingDurationValues { get; } = { 3, 4, 5, 6, 7, 8, 9, 10, 12, 15, 20, 30, 45, 60, int.MaxValue };
        public static int MaxPingsPerPlayer { get => _maxPingsPerPlayer; set => _maxPingsPerPlayer = value <= 10 ? value : int.MaxValue; }
        /**/ private static int _maxPingsPerPlayer = 1;

        // Visual

        public static OutlineStyle Style { get => _style; set { _style = value; UpdateMenu(); OutlineTools.UpdateOutlinesStyle(); } }
        /**/ private static OutlineStyle _style = OutlineStyle.Quick;
        public static bool UniquePingColors { get => _uniquePingColors; set { _uniquePingColors = value; UpdateMenu(); } }
        /**/ private static bool _uniquePingColors = false;
        public static string PingColorHex { get => null; set { PingColor = HexToColor(value) ?? PingColor; if (value != FormatHex(value)) ExtraSettingsAPI_SetInputValue("colorDefault", FormatHex(value)); DisplayColorSettings(); } }
        /**/ public static Color PingColor = Color.yellow;
        public static bool HasOwnPingColor { get => _hasOwnPingColor; set { _hasOwnPingColor = value; UpdateMenu(); } }
        /**/ private static bool _hasOwnPingColor = false;
        public static string OwnPingColorHex { get => null; set { OwnPingColor = HexToColor(value) ?? OwnPingColor; DisplayColorSettings(); } }
        /**/ public static Color OwnPingColor = Color.gray;
        public static float OutlineThicknessMultiplier { get; set; } = 1f;
        public static bool ShowEdgePingAsArrow { get; set; } = true;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ExtraSettingsAPI_SetInputValue(string settingName, string value) { }

        private static string FormatHex(string hex) => "#" + string.Concat(
            Regex.Replace(hex, "<[^>]*>", "")
                .Replace("#", "").Trim().ToUpper().Where("0123456789ABCDEF".Contains).Take(6));

        // Debug
        public static int DebugMode { get; private set; }

        private static void ExtraSettingsAPI_Unload() => DebugMode = 0;
        private static void ExtraSettingsAPI_SettingsOpen() => DisplayColorSettings();

        private static void DisplayColorSettings()
        {
            ExtraSettingsAPI_SetText("colorDefault", $"[<color={ColorToHex(PingColor)}>▐█▌</color>] Ping color");
            ExtraSettingsAPI_SetText("colorOwn", $"[<color={ColorToHex(OwnPingColor)}>▐█▌</color>] Own ping color");
        }
        
        static char ExtraSettingsAPI_InputValidation (string settingName, string t, int i, char c)
        {
            if (i == 0 && c == '#' && !t.Contains('#')) return c;
            var maxLen = t.StartsWith("#") ? 7 : 6;
            if (!Uri.IsHexDigit(c) || t.Length >= maxLen) return '\0';
            var newText = t.Insert(i, c.ToString());
            
            if (settingName == "colorDefault")
                PingColor = HexToColor(newText) ?? PingColor;
            else
                OwnPingColor = HexToColor(newText) ?? OwnPingColor;
            
            DisplayColorSettings();
            return c;
        }

        private static string ExtraSettingsAPI_HandleSliderText(string settingName, float value)
        {
            switch (settingName)
            {
                case "pingDuration":
                    return PingDuration < int.MaxValue ? PingDuration + " seconds" : "Infinite";
                case "maxPingsPerPlayer":
                    return MaxPingsPerPlayer < int.MaxValue ? MaxPingsPerPlayer+"" : "Unlimited";
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
        
        // Overridden by ExtraSettingsAPI
        [MethodImpl(MethodImplOptions.NoInlining)] public static void ExtraSettingsAPI_CheckSettingVisibility() { }
        [MethodImpl(MethodImplOptions.NoInlining)] public static void ExtraSettingsAPI_SetText(string settingName, string text) { }
        public static void UpdateMenu() => ExtraSettingsAPI_CheckSettingVisibility();
        

        private static int Clamp(int val, int min, int max) => val<min?min : val>max?max : val;
        
        #endregion

        #region Utils

        private static Color? HexToColor(string hex)
        {
            if (hex == null) return null;
            hex = hex.Replace("#", "").Trim();
            switch (hex.Length)
            {
                case 3: hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}FF"; /* #RGB -> #RRGGBBFF */ break;
                // case 4: hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}{hex[3]}{hex[3]}"; /* #RGBA -> #RRGGBBAA */ break; // No transparency support
                case 6: hex += "FF"; /* #RRGGBB -> #RRGGBBAA */ break;
            }
            if (hex.Length != 8 || !uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out uint hexVal)) return null;

            return new Color32(
                (byte)(hexVal >> 24),
                (byte)((hexVal >> 16) & 0xFF),
                (byte)((hexVal >> 8) & 0xFF),
                (byte)(hexVal & 0xFF));
        }
        
        private static string ColorToHex(Color32 color) => $"#{color.r:X2}{color.g:X2}{color.b:X2}";

        #endregion
    }

    public enum OutlineStyle
    {
        Disabled,
        Quick,
        Fancy
    }
}
