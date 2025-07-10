using Mirror;
using UnityEngine;

public class PhoenixHealth : PlayerHealth
{
    [SerializeField, Range(0, 1)] private float reviveHealthPercentage = 0.5f;
    [SerializeField] private GameObject reviveEffectPrefab;
    
    [SyncVar] private bool hasRevived = false;

    [SerializeField] private Material phoenixSkin;
    private Material copyMaterial;
    private Renderer objectRenderer;
    
    private void Start()
    {
        copyMaterial = new Material(phoenixSkin);
        // Получаем рендерер объекта
        objectRenderer = GetComponent<Renderer>();
        
        // Сохраняем оригинальный материал
        if (objectRenderer)
        {
            objectRenderer.material = copyMaterial;
        }
    }
    
    protected override void Die()
    {
        if (!isServer) return;
        
        if (!hasRevived)
        {
            RevivePlayer();
            return;
        }
        
        base.Die();
    }
    
    [Server]
    private void RevivePlayer()
    {
        float reviveHealth = maxHealth * reviveHealthPercentage;
        _currentHealth = reviveHealth;
        
        hasRevived = true;
        
        PlayerState playerState = GetComponent<PlayerState>();
        string playerNickname = playerState?.PlayerNickname ?? "Unknown";
        RpcNotifyPlayerRevive(playerNickname);
        
        RpcPlayReviveEffect();
        
        // Меняем материал только для этого объекта
        RpcChangeToPhoenixSkin();
    }
    
    [ClientRpc]
    private void RpcChangeToPhoenixSkin()
    {
        if (objectRenderer && phoenixSkin)
        {
            // Выключаем эмиссию
            copyMaterial.DisableKeyword("_EMISSION");
            copyMaterial.SetColor("_EmissionColor", Color.black);
        }
    }
    
    [ClientRpc]
    private void RpcNotifyPlayerRevive(string playerNickname)
    {
        if (UIManager.Instance)
        {
            UIManager.Instance.ShowNotification($"{playerNickname} has risen from the ashes!");
        }
    }
    
    [ClientRpc]
    private void RpcPlayReviveEffect()
    {
        if (reviveEffectPrefab)
        {
            Instantiate(reviveEffectPrefab, transform.position, transform.rotation);
        }
    }
}