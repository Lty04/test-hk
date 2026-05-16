using System;
using BepInEx.Logging;
using LiteNetLib;
using LiteNetLib.Utils;

namespace HollowKnightOnline.Networking
{
    public enum PacketType : byte
    {
        // Connection
        ConnectRequest = 1,
        ConnectResponse,
        Disconnect,
        
        // Player Sync
        PlayerPosition = 10,
        PlayerAnimation,
        PlayerAction,
        
        // Enemy Sync
        EnemySpawn = 20,
        EnemyDespawn,
        EnemyHealthUpdate,
        EnemyPositionUpdate,
        
        // World State
        SceneChange = 30,
        BenchActivate,
    }

    public class NetworkManager : IDisposable
    {
        private EventBasedNetListener _listener = null!;
        private NetPeerConfiguration _config = null!;
        private NetManager _netManager = null!;
        private ManualLogSource _logger;
        
        public bool IsHost { get; private set; }
        public bool IsConnected => _netManager.IsRunning;
        public NetPeer? ConnectedPeer { get; private set; }
        
        // Events
        public event Action<NetPeer>? OnPeerConnected;
        public event Action<NetPeer, DisconnectReason>? OnPeerDisconnected;
        public event Action<NetPeer, NetDataReader>? OnDataReceived;
        
        public NetworkManager(ManualLogSource logger)
        {
            _logger = logger;
            Initialize();
        }
        
        private void Initialize()
        {
            _listener = new EventBasedNetListener();
            
            // Setup listener events
            _listener.PeerConnectedEvent += peer =>
            {
                _logger.LogInfo($"Peer connected: {peer.EndPoint}");
                OnPeerConnected?.Invoke(peer);
            };
            
            _listener.PeerDisconnectedEvent += (peer, disconnectInfo) =>
            {
                _logger.LogWarning($"Peer disconnected: {peer.EndPoint}, Reason: {disconnectInfo.Reason}");
                OnPeerDisconnected?.Invoke(peer, disconnectInfo.Reason);
            };
            
            _listener.NetworkReceiveEvent += (peer, reader, deliveryMethod) =>
            {
                OnDataReceived?.Invoke(peer, reader);
                reader.Recycle();
            };
            
            _listener.ConnectionSucceedEvent += peer =>
            {
                _logger.LogInfo($"Connection succeeded to: {peer.EndPoint}");
                ConnectedPeer = peer;
            };
            
            _listener.ConnectionFailedEvent += (endpoint, error) =>
            {
                _logger.LogError($"Connection failed to: {endpoint}, Error: {error}");
            };
            
            // Configure network
            _config = new NetPeerConfiguration("HollowKnightOnline")
            {
                Port = 7777,
                MaxConnections = 4, // Host + 3 players like Elden Ring seamless
                EnableMessageType = NetIncomingMessageType.ConnectionApproval,
                SimulationMaxPacketSize = 4096,
                ReceiveBufferSize = 1024 * 1024,
                SendBufferSize = 1024 * 1024,
            };
            
            // Enable latency simulation for testing (optional)
            // _config.SimulateLatency = true;
            // _config.SimulationMinLatency = 50;
            // _config.SimulationMaxLatency = 150;
            
            _netManager = new NetManager(_listener, _config);
        }
        
        public void StartHost()
        {
            if (_netManager.Start())
            {
                IsHost = true;
                _logger.LogInfo($"Started host on port {_config.Port}");
            }
            else
            {
                _logger.LogError("Failed to start host");
            }
        }
        
        public void ConnectToHost(string ip, int port = 7777)
        {
            IsHost = false;
            _logger.LogInfo($"Connecting to {ip}:{port}...");
            _netManager.Connect(ip, port);
        }
        
        public void PollEvents()
        {
            _netManager.PollEvents();
        }
        
        public void SendToAll(PacketType packetType, NetDataWriter writer, DeliveryMethod deliveryMethod = DeliveryMethod.ReliableOrdered)
        {
            if (!IsConnected) return;
            
            writer.Put((byte)packetType);
            
            if (IsHost)
            {
                _netManager.SendToAll(writer, deliveryMethod);
            }
            else
            {
                ConnectedPeer?.Send(writer, deliveryMethod);
            }
            
            writer.Recycle();
        }
        
        public void SendToPeer(NetPeer peer, PacketType packetType, NetDataWriter writer, DeliveryMethod deliveryMethod = DeliveryMethod.ReliableOrdered)
        {
            if (!IsConnected) return;
            
            writer.Put((byte)packetType);
            peer.Send(writer, deliveryMethod);
            writer.Recycle();
        }
        
        public void Shutdown()
        {
            _netManager.Stop();
            _logger.LogInfo("Network manager shutdown");
        }
        
        public void Dispose()
        {
            Shutdown();
            _netManager.Dispose();
        }
    }
}
