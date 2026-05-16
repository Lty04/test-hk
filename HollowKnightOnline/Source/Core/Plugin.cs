using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using HollowKnightOnline.Core;
using HollowKnightOnline.Networking;
using UnityEngine;

namespace HollowKnightOnline
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class HollowKnightOnlinePlugin : BaseUnityPlugin
    {
        public static HollowKnightOnlinePlugin Instance { get; private set; } = null!;
        
        public static NetworkManager NetworkManager { get; private set; } = null!;
        public static SyncManager SyncManager { get; private set; } = null!;
        
        private ManualLogSource _logSource = null!;
        private Harmony _harmony = null!;

        private void Awake()
        {
            Instance = this;
            _logSource = Logger;
            
            _logSource.LogInfo($"Loading {PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION}");
            
            // Initialize Harmony patches
            _harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            _harmony.PatchAll();
            
            // Initialize network manager
            NetworkManager = new NetworkManager(_logSource);
            
            // Initialize sync manager
            SyncManager = new SyncManager(_logSource);
            
            _logSource.LogInfo($"{PluginInfo.PLUGIN_NAME} loaded successfully!");
        }

        private void Update()
        {
            // Poll network events
            NetworkManager?.PollEvents();
            
            // Update sync states
            SyncManager?.Update();
        }

        private void OnDestroy()
        {
            NetworkManager?.Shutdown();
            _harmony?.UnpatchSelf();
        }
    }
}
