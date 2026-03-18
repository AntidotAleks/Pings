using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using HarmonyLib;
using HMLLibrary;
using pings.outlines;
using RaftModLoader;
using Steamworks;
using UnityEngine;
using UnityEngine.UI;

namespace pings
{
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class Pings : Mod
    {
        // Mod information
        public static Mod mod;
        private static Harmony _harmony;
        PSettings ExtraSettingsAPI_Settings = new PSettings();
        internal const int ModChannel = 4571607; // Channel for mod messages
        public static Network_UserId CurrentUserID => RAPI.GetLocalPlayer().steamID;
        private static Camera _camera;
        public static Camera Camera => _camera ? _camera : (_camera = Camera.main ?? Camera.current);

        #region Mod Loading / Unloading

        public IEnumerator Start()
        {
            mod = this;
            (_harmony = new Harmony("me.antidotaleks.pings")).PatchAll();
            
            SubscribeToNetworkChannel(slug);

            MyInput.Keybinds.TryAdd("pings.Pings.pingKeybind", new Keybind("pings.Pings.pingKeybind", KeyCode.Mouse2));
            MyInput.Keybinds.TryAdd("pings.Pings.removeClosestPingKeybind", new Keybind("pings.Pings.removeClosestPingKeybind", KeyCode.None));
            MyInput.Keybinds.TryAdd("pings.Pings.clearPingsKeybind", new Keybind("pings.Pings.clearPingsKeybind", KeyCode.None));

            yield return Setup.LoadAssets();
            Networking.OnLoad();
            PingManager.OnLoad();

            Log("Mod Pings is loaded!");
        }

        public void OnModUnload()
        {
            _harmony.UnpatchAll("me.antidotaleks.pings");
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
        public override bool OnNetworkMessage(object message, Network_UserId from, string modslug)
        {
            Log($"{message.GetType()} from {RAPI.GetUsernameFromUserID(from)}, slug {modslug} comparing with {slug}");
            if (modslug != slug) return false;
            Networking.CheckMessages(message);
            return true;
        }

        public override void WorldEvent_WorldLoaded() => Networking.OnLoad();
        public override void WorldEvent_WorldUnloaded() => Networking.OnUnload();

        #endregion

        private static string Prefix(string sub) => mod.GetModInfo().name + (sub != null ? ": "+sub : "");
        
        public static void Log(object message, int minLevel = 0, string sub = null)
        { if(PSettings.DebugMode >= minLevel) Debug.Log($"[{Prefix(sub)}] {message.ToString().Replace("<", "<\u200B")}"); }
        
        public static void LogWarning(object message, int minLevel = 0, string sub = null)
        { if(PSettings.DebugMode >= minLevel) Debug.LogWarning($"[{Prefix(sub)}] {message}"); }

        public static void LogError(object message, int minLevel = 0, string sub = null)
        { if (PSettings.DebugMode >= minLevel) Debug.LogError($"[{Prefix(sub)}] {message}"); }
    }

    public enum OutlineStyle
    {
        Disabled,
        Quick,
        Fancy
    }
}
