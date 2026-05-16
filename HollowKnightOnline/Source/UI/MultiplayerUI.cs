using UnityEngine;
using HollowKnightOnline.Networking;

namespace HollowKnightOnline.UI
{
    /// <summary>
    /// Simple in-game UI for hosting/joining multiplayer sessions
    /// </summary>
    public class MultiplayerUI : MonoBehaviour
    {
        public static MultiplayerUI Instance { get; private set; } = null!;
        
        private bool _showUI = false;
        private Rect _windowRect = new(20, 20, 300, 400);
        
        private string _serverIP = "127.0.0.1";
        private int _serverPort = 7777;
        private string _statusMessage = "Not connected";
        private bool _isConnected = false;
        
        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        
        void OnGUI()
        {
            if (!_showUI) return;
            
            _windowRect = GUILayout.Window(1, _windowRect, DrawWindow, "Hollow Knight Online");
        }
        
        private void DrawWindow(int windowID)
        {
            GUILayout.BeginVertical();
            
            // Status
            GUILayout.Label($"Status: {_statusMessage}");
            GUILayout.Space(10);
            
            if (!_isConnected)
            {
                // Host button
                if (GUILayout.Button("Host Game"))
                {
                    HostGame();
                }
                
                GUILayout.Space(5);
                
                // Server IP input
                GUILayout.Label("Server IP:");
                _serverIP = GUILayout.TextField(_serverIP);
                
                // Server Port input
                GUILayout.Label("Server Port:");
                var portStr = GUILayout.TextField(_serverPort.ToString());
                if (int.TryParse(portStr, out var port))
                {
                    _serverPort = port;
                }
                
                GUILayout.Space(5);
                
                // Connect button
                if (GUILayout.Button("Connect to Server"))
                {
                    ConnectToServer();
                }
                
                GUILayout.Space(10);
                
                // Instructions
                GUILayout.Box("Instructions:\n\n" +
                    "HOST:\n" +
                    "1. Click 'Host Game'\n" +
                    "2. Share your external IP with friends\n" +
                    "3. Use Sakura FRP if behind NAT\n\n" +
                    "CLIENT:\n" +
                    "1. Enter host's IP address\n" +
                    "2. Click 'Connect to Server'");
            }
            else
            {
                // Connected view
                GUILayout.Label("Connected!");
                GUILayout.Space(10);
                
                if (HollowKnightOnlinePlugin.NetworkManager?.IsHost ?? false)
                {
                    GUILayout.Label("You are the HOST");
                    GUILayout.Label($"Players connected: {GetPlayerCount()}");
                }
                else
                {
                    GUILayout.Label($"Connected to: {_serverIP}:{_serverPort}");
                }
                
                GUILayout.Space(10);
                
                if (GUILayout.Button("Disconnect"))
                {
                    Disconnect();
                }
            }
            
            GUILayout.Space(10);
            
            // Toggle key hint
            GUILayout.Label("Press F10 to toggle this UI");
            
            GUILayout.EndVertical();
            
            GUI.DragWindow(new Rect(0, 0, 10000, 500));
        }
        
        private void HostGame()
        {
            try
            {
                HollowKnightOnlinePlugin.NetworkManager?.StartHost();
                _statusMessage = "Hosting game...";
                _isConnected = true;
                
                HollowKnightOnlinePlugin.Instance?.Logger.LogInfo("Started hosting game");
            }
            catch (System.Exception ex)
            {
                _statusMessage = $"Failed to host: {ex.Message}";
                HollowKnightOnlinePlugin.Instance?.Logger.LogError($"Failed to host: {ex}");
            }
        }
        
        private void ConnectToServer()
        {
            try
            {
                HollowKnightOnlinePlugin.NetworkManager?.ConnectToHost(_serverIP, _serverPort);
                _statusMessage = "Connecting...";
                
                HollowKnightOnlinePlugin.Instance?.Logger.LogInfo($"Connecting to {_serverIP}:{_serverPort}");
            }
            catch (System.Exception ex)
            {
                _statusMessage = $"Failed to connect: {ex.Message}";
                HollowKnightOnlinePlugin.Instance?.Logger.LogError($"Failed to connect: {ex}");
            }
        }
        
        private void Disconnect()
        {
            HollowKnightOnlinePlugin.NetworkManager?.Shutdown();
            _statusMessage = "Disconnected";
            _isConnected = false;
            
            HollowKnightOnlinePlugin.SyncManager?.ClearRemotePlayers();
            
            // Clear remote player renders
            var rendererManager = FindObjectOfType<Sync.RemotePlayerManager>();
            rendererManager?.ClearAll();
            
            HollowKnightOnlinePlugin.Instance?.Logger.LogInfo("Disconnected from server");
        }
        
        private int GetPlayerCount()
        {
            // This would need proper implementation based on network manager
            return 1;
        }
        
        void Update()
        {
            // Toggle UI with F10
            if (Input.GetKeyDown(KeyCode.F10))
            {
                _showUI = !_showUI;
            }
            
            // Update connection status
            if (_isConnected && !(HollowKnightOnlinePlugin.NetworkManager?.IsConnected ?? false))
            {
                _statusMessage = "Connection lost";
                _isConnected = false;
            }
        }
    }
}
