using UnityEngine;
using System.Collections.Generic;
using HollowKnightOnline.Sync;

namespace HollowKnightOnline.Sync
{
    /// <summary>
    /// Renders remote players in the game world
    /// Attach this to a GameObject to represent a remote player
    /// </summary>
    public class RemotePlayerRenderer : MonoBehaviour
    {
        public string PlayerId { get; set; } = "";
        public PlayerData? PlayerData { get; set; }
        
        private SpriteRenderer _spriteRenderer = null!;
        private Animator? _animator;
        private bool _isFacingRight = true;
        
        // Smooth interpolation
        private Vector2 _targetPosition;
        private Vector2 _currentVelocity;
        private float _interpolationSpeed = 10f;
        
        void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _animator = GetComponent<Animator>();
        }
        
        void Update()
        {
            if (PlayerData == null) return;
            
            // Smoothly interpolate position
            _targetPosition = PlayerData.Position;
            transform.position = Vector2.SmoothDamp(
                transform.position, 
                _targetPosition, 
                ref _currentVelocity, 
                1f / _interpolationSpeed
            );
            
            // Update facing direction
            if (PlayerData.IsFacingRight != _isFacingRight)
            {
                _isFacingRight = PlayerData.IsFacingRight;
                var scale = transform.localScale;
                scale.x = _isFacingRight ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
                transform.localScale = scale;
            }
            
            // Update animation based on state
            UpdateAnimation();
        }
        
        private void UpdateAnimation()
        {
            if (_animator == null) return;
            
            switch (PlayerData?.AnimationState)
            {
                case PlayerAnimationState.Idle:
                    _animator.SetBool("IsRunning", false);
                    _animator.SetBool("IsJumping", false);
                    _animator.SetBool("IsAttacking", false);
                    break;
                case PlayerAnimationState.Running:
                    _animator.SetBool("IsRunning", true);
                    _animator.SetBool("IsJumping", false);
                    _animator.SetBool("IsAttacking", false);
                    break;
                case PlayerAnimationState.Jumping:
                case PlayerAnimationState.Falling:
                    _animator.SetBool("IsRunning", false);
                    _animator.SetBool("IsJumping", true);
                    _animator.SetBool("IsAttacking", false);
                    break;
                case PlayerAnimationState.Attacking:
                    _animator.SetBool("IsRunning", false);
                    _animator.SetBool("IsJumping", false);
                    _animator.SetBool("IsAttacking", true);
                    break;
                case PlayerAnimationState.Dashing:
                    // Handle dash animation
                    break;
                case PlayerAnimationState.TakingDamage:
                    // Handle damage animation
                    break;
                case PlayerAnimationState.Dead:
                    // Handle death animation
                    break;
            }
        }
        
        public void SetPlayerData(PlayerData data)
        {
            PlayerData = data;
            PlayerId = data.PlayerId;
        }
    }
    
    /// <summary>
    /// Manager for remote player rendering
    /// Creates and updates remote player GameObjects
    /// </summary>
    public class RemotePlayerManager : MonoBehaviour
    {
        public static RemotePlayerManager Instance { get; private set; } = null!;
        
        [SerializeField] private GameObject remotePlayerPrefab = null!;
        
        private Dictionary<string, RemotePlayerRenderer> _remotePlayers = new();
        private Dictionary<string, float> _lastUpdateTime = new();
        
        private const float PLAYER_TIMEOUT = 5f; // Remove player if no update for 5 seconds
        
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
        
        void Update()
        {
            foreach (var kvp in HollowKnightOnlinePlugin.SyncManager?.RemotePlayers ?? new Dictionary<string, PlayerData>())
            {
                var playerId = kvp.Key;
                var playerData = kvp.Value;
                
                // Skip local player
                if (playerId == HollowKnightOnlinePlugin.SyncManager?.LocalPlayer.PlayerId) continue;
                
                // Create or update remote player
                if (!_remotePlayers.ContainsKey(playerId))
                {
                    CreateRemotePlayer(playerData);
                }
                else
                {
                    UpdateRemotePlayer(playerData);
                }
                
                _lastUpdateTime[playerId] = Time.time;
            }
            
            // Remove timed out players
            RemoveTimedOutPlayers();
        }
        
        private void CreateRemotePlayer(PlayerData playerData)
        {
            if (remotePlayerPrefab == null)
            {
                HollowKnightOnlinePlugin.Instance?.Logger.LogWarning("Remote player prefab not assigned!");
                return;
            }
            
            var go = Instantiate(remotePlayerPrefab, playerData.Position, Quaternion.identity);
            var renderer = go.GetComponent<RemotePlayerRenderer>();
            
            if (renderer != null)
            {
                renderer.SetPlayerData(playerData);
                _remotePlayers[playerData.PlayerId] = renderer;
                
                HollowKnightOnlinePlugin.Instance?.Logger.LogInfo($"Created remote player: {playerData.PlayerId}");
            }
        }
        
        private void UpdateRemotePlayer(PlayerData playerData)
        {
            if (_remotePlayers.TryGetValue(playerData.PlayerId, out var renderer))
            {
                renderer.SetPlayerData(playerData);
            }
        }
        
        private void RemoveTimedOutPlayers()
        {
            var currentTime = Time.time;
            var playersToRemove = new List<string>();
            
            foreach (var kvp in _lastUpdateTime)
            {
                if (currentTime - kvp.Value > PLAYER_TIMEOUT)
                {
                    playersToRemove.Add(kvp.Key);
                }
            }
            
            foreach (var playerId in playersToRemove)
            {
                if (_remotePlayers.TryGetValue(playerId, out var renderer))
                {
                    if (renderer != null)
                    {
                        Destroy(renderer.gameObject);
                    }
                    _remotePlayers.Remove(playerId);
                    _lastUpdateTime.Remove(playerId);
                    
                    HollowKnightOnlinePlugin.Instance?.Logger.LogInfo($"Removed timed out player: {playerId}");
                }
            }
        }
        
        public void ClearAll()
        {
            foreach (var renderer in _remotePlayers.Values)
            {
                if (renderer != null)
                {
                    Destroy(renderer.gameObject);
                }
            }
            
            _remotePlayers.Clear();
            _lastUpdateTime.Clear();
        }
    }
}
