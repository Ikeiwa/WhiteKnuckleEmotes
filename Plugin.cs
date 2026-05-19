using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using WKLib.API.UI;

namespace WhiteKnuckleEmotes
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("com.monksilly.WKLib")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }
        public static ManualLogSource Log { get; private set; }

        public static readonly List<ConfigEntry<KeyCode>> EmoteKeysLeft = new();
        public static readonly List<ConfigEntry<KeyCode>> EmoteKeysRight = new();

        public const int MaxEmotes = 10;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            for (int i = 0; i < MaxEmotes; i++)
            {
                EmoteKeysLeft.Add(Config.Bind("General.KeyBinds", "EmoteKeyLeft" + i, KeyCode.None));
                EmoteKeysRight.Add(Config.Bind("General.KeyBinds", "EmoteKeyRight" + i, KeyCode.None));
            }

            // Plugin startup logic
            var harmony = new Harmony(PluginInfo.PLUGIN_GUID + ".patch");
            harmony.PatchAll();

            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }
    }
}