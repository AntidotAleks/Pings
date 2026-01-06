using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using HMLLibrary;
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
        
        // Materials for outlines
        internal static Material OutlineMaterial, FillMaterial;

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
        public static float PingDuration { get; private set; } = 10f;
        public static int maxPingsPerPlayer = 1;
        // Visual
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
            PingDuration = PingDurationValues[Clamp((int) Math.Round(ExtraSettingsAPI_GetSliderValue("pingDuration")), 0, PingDurationValues.Length - 1)];
            maxPingsPerPlayer = (int) ExtraSettingsAPI_GetSliderValue("maxPingsPerPlayer");
            if (maxPingsPerPlayer == 11) // Unlimited
                maxPingsPerPlayer = int.MaxValue;
            // Visual
            ShowEdgePingAsArrow = ExtraSettingsAPI_GetCheckboxState("showEdgePingAsArrow");
            // Debug
            DebugMode = ExtraSettingsAPI_GetComboboxSelectedIndex("debugMode");
        }

        private static void ExtraSettingsAPI_Unload()
        {
            // Visual
            PingKey = new Keybind("pingKeybind", KeyCode.Mouse2);
            ClearAllPingsKey = new Keybind("clearPingsKeybind", KeyCode.None);
            PingDuration = 10f;
            maxPingsPerPlayer = 1;
            // Control
            ShowEdgePingAsArrow = true;
            // Debug
            DebugMode = 0;
        }
        
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
        
        // Overridden by ExtraSettingsAPI
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Keybind ExtraSettingsAPI_GetKeybind(string SettingName) => null;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static float ExtraSettingsAPI_GetSliderValue(string SettingName) => 0;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int ExtraSettingsAPI_GetComboboxSelectedIndex(string SettingName) => -1;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool ExtraSettingsAPI_GetCheckboxState(string settingName) => false;

        private static int Clamp(int val, int min, int max) => val < min ? min  :  val > max ? max  :  val;
        
        #endregion
    }
}
