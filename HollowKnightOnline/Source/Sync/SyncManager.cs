using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;
using HollowKnightOnline.Networking;
using LiteNetLib;
using LiteNetLib.Utils;

namespace HollowKnightOnline.Sync
{
    public class PlayerData
    {
        public string PlayerId { get; set; } = Guid.NewGuid().ToString();
        public Vector2 Position { get; set; }
        public Vector2 Velocity { get; set; }
        public int Health { get; set; } = 5;
        public int Soul { get; set; } = 0;
        public string CurrentScene { get; set; } = "";
        public bool IsGrounded { get; set; }
        public bool IsFacingRight { get; set; } = true;
        public PlayerAnimationState AnimationState { get; set; } = PlayerAnimationState.Idle;
    }

    public enum PlayerAnimationState
    {
        Idle,
        Running,
        Jumping,
        Falling,
        Attacking,
        Dashing,
        TakingDamage,
        Dead
    }

    public class EnemyData
    {
        public string EnemyId { get; set; } = "";
        public string PrefabName { get; set; } = "";
        public Vector2 Position { get; set; }
        public int MaxHealth { get; set; }
        public int CurrentHealth { get; set; }
        public bool IsDead { get; set; }
        public float AttackCooldown { get; set; }
        public Vector2 TargetPosition { get; set; }
    }

    public class SyncManager
    {
        private ManualLogSource _logger;
        private PlayerData _localPlayerData = new();
        private Dictionary<string, PlayerData> _remotePlayers = new();
        private Dictionary<string, EnemyData> _enemies = new();
        
        // Sync intervals (in seconds)
        private const float POSITION_SYNC_INTERVAL = 0.033f; // ~30fps
        private const float ENEMY_HEALTH_SYNC_INTERVAL = 0.1f;
        
        private float _lastPositionSyncTime;
        private float _lastEnemyHealthSyncTime;
        
        public PlayerData LocalPlayer => _localPlayerData;
        public IReadOnlyDictionary<string, PlayerData> RemotePlayers => _remotePlayers;
        public IReadOnlyDictionary<string, EnemyData> Enemies => _enemies;
        
        public SyncManager(ManualLogSource logger)
        {
            _logger = logger;
            
            // Subscribe to network events
            if (HollowKnightOnlinePlugin.NetworkManager != null)
            {
                HollowKnightOnlinePlugin.NetworkManager.OnDataReceived += HandleNetworkData;
            }
        }
        
        public void Update()
        {
            float currentTime = Time.time;
            
            // Sync player position
            if (currentTime - _lastPositionSyncTime >= POSITION_SYNC_INTERVAL)
            {
                SyncPlayerPosition();
                _lastPositionSyncTime = currentTime;
            }
            
            // Sync enemy health
            if (currentTime - _lastEnemyHealthSyncTime >= ENEMY_HEALTH_SYNC_INTERVAL)
            {
                SyncEnemyHealth();
                _lastEnemyHealthSyncTime = currentTime;
            }
        }
        
        private void SyncPlayerPosition()
        {
            var writer = NetDataWriter.Get();
            
            // Write player data
            writer.Put(_localPlayerData.PlayerId);
            writer.Put(_localPlayerData.Position.x);
            writer.Put(_localPlayerData.Position.y);
            writer.Put(_localPlayerData.Velocity.x);
            writer.Put(_localPlayerData.Velocity.y);
            writer.Put(_localPlayerData.IsGrounded);
            writer.Put(_localPlayerData.IsFacingRight);
            writer.Put((byte)_localPlayerData.AnimationState);
            writer.Put(_localPlayerData.CurrentScene ?? "");
            
            HollowKnightOnlinePlugin.NetworkManager?.SendToAll(
                PacketType.PlayerPosition, 
                writer, 
                DeliveryMethod.UnreliableSequenced
            );
        }
        
        private void SyncEnemyHealth()
        {
            foreach (var enemy in _enemies.Values)
            {
                var writer = NetDataWriter.Get();
                
                writer.Put(enemy.EnemyId);
                writer.Put(enemy.CurrentHealth);
                writer.Put(enemy.MaxHealth);
                writer.Put(enemy.IsDead);
                writer.Put(enemy.Position.x);
                writer.Put(enemy.Position.y);
                
                HollowKnightOnlinePlugin.NetworkManager?.SendToAll(
                    PacketType.EnemyHealthUpdate,
                    writer,
                    DeliveryMethod.ReliableOrdered
                );
            }
        }
        
        private void HandleNetworkData(NetPeer peer, NetDataReader reader)
        {
            try
            {
                var packetType = (PacketType)reader.GetByte();
                
                switch (packetType)
                {
                    case PacketType.PlayerPosition:
                        HandlePlayerPositionUpdate(reader);
                        break;
                    case PacketType.EnemyHealthUpdate:
                        HandleEnemyHealthUpdate(reader);
                        break;
                    case PacketType.EnemySpawn:
                        HandleEnemySpawn(reader);
                        break;
                    case PacketType.EnemyDespawn:
                        HandleEnemyDespawn(reader);
                        break;
                    default:
                        _logger.LogWarning($"Unknown packet type: {packetType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error handling network data: {ex.Message}");
            }
        }
        
        private void HandlePlayerPositionUpdate(NetDataReader reader)
        {
            var playerId = reader.GetString();
            var posX = reader.GetFloat();
            var posY = reader.GetFloat();
            var velX = reader.GetFloat();
            var velY = reader.GetFloat();
            var isGrounded = reader.GetBool();
            var isFacingRight = reader.GetBool();
            var animationState = (PlayerAnimationState)reader.GetByte();
            var scene = reader.GetString();
            
            if (!_remotePlayers.ContainsKey(playerId))
            {
                _remotePlayers[playerId] = new PlayerData { PlayerId = playerId };
            }
            
            var playerData = _remotePlayers[playerId];
            playerData.Position = new Vector2(posX, posY);
            playerData.Velocity = new Vector2(velX, velY);
            playerData.IsGrounded = isGrounded;
            playerData.IsFacingRight = isFacingRight;
            playerData.AnimationState = animationState;
            playerData.CurrentScene = scene;
        }
        
        private void HandleEnemyHealthUpdate(NetDataReader reader)
        {
            var enemyId = reader.GetString();
            var currentHealth = reader.GetInt();
            var maxHealth = reader.GetInt();
            var isDead = reader.GetBool();
            var posX = reader.GetFloat();
            var posY = reader.GetFloat();
            
            if (!_enemies.ContainsKey(enemyId))
            {
                _enemies[enemyId] = new EnemyData { EnemyId = enemyId };
            }
            
            var enemy = _enemies[enemyId];
            enemy.CurrentHealth = currentHealth;
            enemy.MaxHealth = maxHealth;
            enemy.IsDead = isDead;
            enemy.Position = new Vector2(posX, posY);
            
            _logger.LogDebug($"Enemy {enemyId} health updated: {currentHealth}/{maxHealth}");
        }
        
        private void HandleEnemySpawn(NetDataReader reader)
        {
            var enemyId = reader.GetString();
            var prefabName = reader.GetString();
            var posX = reader.GetFloat();
            var posY = reader.GetFloat();
            var maxHealth = reader.GetInt();
            
            var enemy = new EnemyData
            {
                EnemyId = enemyId,
                PrefabName = prefabName,
                Position = new Vector2(posX, posY),
                MaxHealth = maxHealth,
                CurrentHealth = maxHealth,
                IsDead = false
            };
            
            _enemies[enemyId] = enemy;
            _logger.LogInfo($"Enemy spawned: {prefabName} at ({posX}, {posY})");
        }
        
        private void HandleEnemyDespawn(NetDataReader reader)
        {
            var enemyId = reader.GetString();
            
            if (_enemies.Remove(enemyId))
            {
                _logger.LogInfo($"Enemy despawned: {enemyId}");
            }
        }
        
        public void UpdateLocalPlayerPosition(Vector2 position, Vector2 velocity, bool isGrounded, bool isFacingRight)
        {
            _localPlayerData.Position = position;
            _localPlayerData.Velocity = velocity;
            _localPlayerData.IsGrounded = isGrounded;
            _localPlayerData.IsFacingRight = isFacingRight;
        }
        
        public void UpdateLocalPlayerAnimation(PlayerAnimationState state)
        {
            _localPlayerData.AnimationState = state;
        }
        
        public void UpdateLocalPlayerHealth(int health)
        {
            _localPlayerData.Health = health;
        }
        
        public void SetCurrentScene(string sceneName)
        {
            _localPlayerData.CurrentScene = sceneName;
        }
        
        public void RegisterEnemy(string enemyId, string prefabName, Vector2 position, int maxHealth)
        {
            var enemy = new EnemyData
            {
                EnemyId = enemyId,
                PrefabName = prefabName,
                Position = position,
                MaxHealth = maxHealth,
                CurrentHealth = maxHealth,
                IsDead = false
            };
            
            _enemies[enemyId] = enemy;
            
            // Notify other players
            var writer = NetDataWriter.Get();
            writer.Put(enemyId);
            writer.Put(prefabName);
            writer.Put(position.x);
            writer.Put(position.y);
            writer.Put(maxHealth);
            
            HollowKnightOnlinePlugin.NetworkManager?.SendToAll(
                PacketType.EnemySpawn,
                writer,
                DeliveryMethod.ReliableOrdered
            );
        }
        
        public void UpdateEnemyHealth(string enemyId, int damage)
        {
            if (_enemies.TryGetValue(enemyId, out var enemy))
            {
                enemy.CurrentHealth = Math.Max(0, enemy.CurrentHealth - damage);
                
                if (enemy.CurrentHealth <= 0)
                {
                    enemy.IsDead = true;
                    
                    // Notify despawn
                    var writer = NetDataWriter.Get();
                    writer.Put(enemyId);
                    HollowKnightOnlinePlugin.NetworkManager?.SendToAll(
                        PacketType.EnemyDespawn,
                        writer,
                        DeliveryMethod.ReliableOrdered
                    );
                }
            }
        }
        
        public void ClearAllEnemies()
        {
            _enemies.Clear();
        }
        
        public void ClearRemotePlayers()
        {
            _remotePlayers.Clear();
        }
    }
}
