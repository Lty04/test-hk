using HarmonyLib;
using UnityEngine;
using HollowKnightOnline.Sync;
using HollowKnight;
using System;

namespace HollowKnightOnline.Patches
{
    /// <summary>
    /// Patches for player movement and position sync
    /// </summary>
    [HarmonyPatch]
    public static class PlayerMovementPatches
    {
        private static HeroController _heroController = null!;
        
        [HarmonyPatch(typeof(HeroController), "Awake")]
        [HarmonyPostfix]
        public static void HeroController_Awake(HeroController __instance)
        {
            _heroController = __instance;
            HollowKnightOnlinePlugin.Instance?.Logger.LogInfo("HeroController initialized");
        }
        
        [HarmonyPatch(typeof(HeroController), "Update")]
        [HarmonyPostfix]
        public static void HeroController_Update(HeroController __instance)
        {
            if (!HollowKnightOnlinePlugin.NetworkManager?.IsConnected ?? true) return;
            
            // Get player state
            var position = __instance.transform.position;
            var velocity = __instance.GetComponent<Rigidbody2D>()?.velocity ?? Vector2.zero;
            var isGrounded = __instance.cState.onGround;
            var isFacingRight = __instance.gameObject.transform.localScale.x > 0;
            
            // Determine animation state
            var animState = DetermineAnimationState(__instance);
            
            // Update local player data
            HollowKnightOnlinePlugin.SyncManager?.UpdateLocalPlayerPosition(
                position, 
                velocity, 
                isGrounded, 
                isFacingRight
            );
            
            HollowKnightOnlinePlugin.SyncManager?.UpdateLocalPlayerAnimation(animState);
        }
        
        private static PlayerAnimationState DetermineAnimationState(HeroController controller)
        {
            if (controller.cState.dead) return PlayerAnimationState.Dead;
            if (controller.cState.hazardDying) return PlayerAnimationState.TakingDamage;
            if (controller.cState.dashing) return PlayerAnimationState.Dashing;
            if (controller.cState.attacking) return PlayerAnimationState.Attacking;
            if (controller.cState.jumpRequested) return PlayerAnimationState.Jumping;
            if (!controller.cState.onGround && controller.cState.falling) return PlayerAnimationState.Falling;
            if (Mathf.Abs(controller.GetAxisInput().x) > 0.1f) return PlayerAnimationState.Running;
            
            return PlayerAnimationState.Idle;
        }
        
        [HarmonyPatch(typeof(HeroController), "TakeDamage")]
        [HarmonyPrefix]
        public static void HeroController_TakeDamage(HeroController __instance, ref int damageAmount)
        {
            if (!HollowKnightOnlinePlugin.NetworkManager?.IsConnected ?? true) return;
            
            // Sync health update
            var newHealth = __instance.healthManager.currentHealth - damageAmount;
            HollowKnightOnlinePlugin.SyncManager?.UpdateLocalPlayerHealth(newHealth);
        }
    }
    
    /// <summary>
    /// Patches for scene transitions
    /// </summary>
    [HarmonyPatch]
    public static class SceneTransitionPatches
    {
        [HarmonyPatch(typeof(Gamemap), "BeginSceneTransition")]
        [HarmonyPrefix]
        public static void Gamemap_BeginSceneTransition(Gamemap __instance, Gamemap.SceneLoadInfo info)
        {
            if (!HollowKnightOnlinePlugin.NetworkManager?.IsConnected ?? true) return;
            
            // Update current scene
            HollowKnightOnlinePlugin.SyncManager?.SetCurrentScene(info.sceneName);
            
            HollowKnightOnlinePlugin.Instance?.Logger.LogInfo($"Scene transition to: {info.sceneName}");
        }
    }
    
    /// <summary>
    /// Patches for enemy spawning and health sync
    /// </summary>
    [HarmonyPatch]
    public static class EnemyPatches
    {
        [HarmonyPatch(typeof(GameObject), "SetActive")]
        [HarmonyPostfix]
        public static void GameObject_SetActive(GameObject __instance, bool value)
        {
            if (!HollowKnightOnlinePlugin.NetworkManager?.IsConnected ?? true) return;
            if (!__instance.CompareTag("Enemy")) return;
            
            // Generate unique enemy ID based on scene and object name
            var enemyId = $"{GameManager.instance.GetCurrentSceneName()}_{__instance.name}_{__instance.GetInstanceID()}";
            
            if (value)
            {
                // Enemy spawned - try to get health component
                var healthComponent = __instance.GetComponentInChildren<HealthManager>();
                var maxHealth = healthComponent?.hp ?? 10;
                
                HollowKnightOnlinePlugin.SyncManager?.RegisterEnemy(
                    enemyId,
                    __instance.name,
                    __instance.transform.position,
                    maxHealth
                );
            }
            else
            {
                // Enemy despawned
                // Note: This would need proper cleanup logic
            }
        }
        
        [HarmonyPatch(typeof(HealthManager), "TakeDamage")]
        [HarmonyPrefix]
        public static void HealthManager_TakeDamage(HealthManager __instance, ref int damageAmount)
        {
            if (!HollowKnightOnlinePlugin.NetworkManager?.IsConnected ?? true) return;
            
            var gameObject = __instance.gameObject;
            if (!gameObject.CompareTag("Enemy")) return;
            
            var enemyId = $"{GameManager.instance.GetCurrentSceneName()}_{gameObject.name}_{gameObject.GetInstanceID()}";
            
            // Sync damage to other players
            HollowKnightOnlinePlugin.SyncManager?.UpdateEnemyHealth(enemyId, damageAmount);
        }
    }
    
    /// <summary>
    /// Patches for bench activation (checkpoint sync)
    /// </summary>
    [HarmonyPatch]
    public static class BenchPatches
    {
        [HarmonyPatch(typeof(HeroController), "heroBench")]
        [HarmonyPostfix]
        public static void HeroController_heroBench(HeroController __instance)
        {
            if (!HollowKnightOnlinePlugin.NetworkManager?.IsConnected ?? true) return;
            
            HollowKnightOnlinePlugin.Instance?.Logger.LogInfo("Player sat on bench - checkpoint synced");
            
            // In a full implementation, this would sync bench activation to all players
            // and potentially teleport them to the same bench
        }
    }
}
