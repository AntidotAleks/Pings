using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using pings.outlines;
using UnityEngine;

namespace pings
{
    public class PSettings
    {

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
        public static Color PingColor = Color.yellow;
        public static bool HasOwnPingColor { get => _hasOwnPingColor; set { _hasOwnPingColor = value; UpdateMenu(); } }
        /**/ private static bool _hasOwnPingColor = false;
        public static Color OwnPingColor = Color.gray;
        public static float OutlineThicknessMultiplier { get; set; } = 1f;
        public static bool ShowEdgePingAsArrow { get; set; } = true;

        // Debug
        
        public static int DebugMode { get; private set; }

        
        
        
        
        private static void ExtraSettingsAPI_Load() => UpdateMenu();
        private static void ExtraSettingsAPI_Unload() => DebugMode = 0;

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
        
        private static void UpdateMenu()
        {
            var pingText = ExtraSettingsAPI_GetInputValue("colorDefault");
            if(pingText != null) PingColor = pingText.HexToColor() ?? pingText.InnerHex().HexToColor() ?? PingColor;
            var ownText = ExtraSettingsAPI_GetInputValue("colorOwn");
            if(ownText != null) OwnPingColor = ownText.HexToColor() ?? ownText.InnerHex().HexToColor() ?? OwnPingColor;
            ExtraSettingsAPI_CheckSettingVisibility();
        }

        [MethodImpl(MethodImplOptions.NoInlining)] private static void ExtraSettingsAPI_CheckSettingVisibility() { }
        [MethodImpl(MethodImplOptions.NoInlining)] private static string ExtraSettingsAPI_GetInputValue(string settingName) => null;
        [MethodImpl(MethodImplOptions.NoInlining)] private static void ExtraSettingsAPI_SetInputValue(string settingName, string value) { }
        
        private static int Clamp(int val, int min, int max) => val<min?min : val>max?max : val;

        private static char ExtraSettingsAPI_HandleInputValidation (string settingName, string t, int i, char c)
        {
            t = t.OnlyHex();
            if (!Uri.IsHexDigit(c) || t.Length >= 6) return '\0';
            return c;
        }

        private static void ExtraSettingsAPI_InputChanged(string name, ref string text)
        {
            text = text.OnlyHex();
            var fill = 6 - text.Length;
            
            Color? color = text.HexToColor();
            switch (name)
            {
                case "colorDefault": PingColor = color ?? PingColor; break;
                case "colorOwn": OwnPingColor = color ?? OwnPingColor; break;
            }

            text = $"[<color={(name == "colorDefault"?PingColor:OwnPingColor).ColorToHex()}>▐█▌</color>] #{text}";
            if (fill >= 3) text += new string('_', fill - 3) + "\u200B<color=#816f49>___</color>";
            else           text += new string('_', fill);
        }

        private static int ExtraSettingsAPI_InputCaretClamp(string name, string text, int position)
        {
            var left = text.IndexOf("█", StringComparison.Ordinal) + 1;
            left = text.IndexOf("#", left, StringComparison.Ordinal) + 1;
            text = text.OnlyHex();
            return Clamp(position, left, left + text.Length);
        }
    }

    internal static class PUtils
    {
        internal static string OnlyHex(this string text) => string.Concat(Regex.Replace(text, "<.*?>", "").Where(Uri.IsHexDigit));
        internal static string InnerHex(this string text) => string.Concat(text.Substring(text.IndexOf('#', text.IndexOf('>'))).Where(Uri.IsHexDigit));
        
        internal static string ColorToHex(this Color color) => ColorToHex((Color32) color);
        internal static string ColorToHex(this Color32 color) => $"#{color.r:X2}{color.g:X2}{color.b:X2}";
        
        internal static Color32? HexToColor(this string hex)
        {
            if (hex == null) return null;
            hex = hex.OnlyHex();
            switch (hex.Length)
            {
                case 3: hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}FF"; /* #RGB -> #RRGGBBFF */ break;
                // case 4: hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}{hex[3]}{hex[3]}"; /* #RGBA -> #RRGGBBAA */ break; // No transparency support
                case 6: hex += "FF"; /* #RRGGBB -> #RRGGBBFF */ break;
            }
            if (hex.Length != 8 || !uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out uint hexVal)) return null;

            return new Color32(
                (byte)(hexVal >> 24),
                (byte)((hexVal >> 16) & 0xFF),
                (byte)((hexVal >> 8) & 0xFF),
                (byte)(hexVal & 0xFF));
        }
    }
}