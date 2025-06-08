using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
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
            
            yield return Setup.LoadOutlines();
            Networking.OnLoad();
            PingManager.Setup();
            
            Log("Mod Pings is loaded!");
        }

        public void OnModUnload()
        {

            Networking.OnUnload();
            PingManager.Cleanup();
            Setup.UnloadOutlines();
            
            Log("Mod Pings is unloaded.");
        }
        #endregion

        #region Mod activity // PingManager Setup and Cleanup
        private static PingManager _pingManager;
        private static bool _hasPingsMod;
        internal static bool HasPingsMod
        {
            set
            {
                if (_hasPingsMod == value) return;
                _hasPingsMod = value;
                if (value)
                {
                    _pingManager = new GameObject("PingManager").AddComponent<PingManager>();
                    DontDestroyOnLoad(_pingManager.gameObject);
                }
                else
                {
                    PingManager.RemoveAllPings();
                    Destroy(_pingManager);
                }
            }
        }
        #endregion

        #region Networking
        
        public void FixedUpdate() => Networking.CheckMessages();
        public override void WorldEvent_WorldLoaded() => Networking.OnLoad();
        public override void WorldEvent_WorldUnloaded() => Networking.OnUnload();
        
        #endregion

        #region Settings // ExtraSettingsAPI integration
        
        public static Keybind PingKey { get; private set; } = new Keybind("pingKeybind", KeyCode.Mouse2);
        public static float PingDuration { get; private set; } = 10f;
        public static int DebugMode { get; private set; }

        public void ExtraSettingsAPI_Load() { Load_ExtraSettingsAPI_Settings(); }

        public void ExtraSettingsAPI_SettingsClose() { Load_ExtraSettingsAPI_Settings(); }

        private static void Load_ExtraSettingsAPI_Settings()
        {
            PingKey = ExtraSettingsAPI_GetKeybind("pingKeybind");
            PingDuration = ExtraSettingsAPI_GetSliderValue("pingDuration");
            DebugMode = ExtraSettingsAPI_GetComboboxSelectedIndex("debugMode");
        }

        public void ExtraSettingsAPI_Unload()
        {
            PingKey = new Keybind("pingKeybind", KeyCode.Mouse2);
            PingDuration = 10f;
            DebugMode = 0;
        }
        // Overridden by ExtraSettingsAPI
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Keybind ExtraSettingsAPI_GetKeybind(string SettingName) => null;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static float ExtraSettingsAPI_GetSliderValue(string SettingName) => 0;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int ExtraSettingsAPI_GetComboboxSelectedIndex(string SettingName) => -1;
        
        #endregion
    }
}
