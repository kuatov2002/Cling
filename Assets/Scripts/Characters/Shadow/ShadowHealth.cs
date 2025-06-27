using Mirror;
using UnityEngine;
using UnityEngine.UI;

public class ShadowHealth : PlayerHealth
{
    [SerializeField] private float invisibilityDuration = 5f;
    
    [SyncVar]
    private bool isInvisible = false;
    private MeshRenderer meshRenderer;
    private Material originalMaterial;
    private Material invisibleMaterial;
    
    private void Start()
    {
        InitializeMaterials();
    }
    
    private void InitializeMaterials()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer)
        {
            originalMaterial = meshRenderer.material;
            invisibleMaterial = new Material(originalMaterial);
        }
    }
    
    public override void TakeDamage(float damage)
    {
        base.TakeDamage(damage);
        ActivateInvisibility();
    }
    
    [Server]
    private void ActivateInvisibility()
    {
        isInvisible = true;
        RpcHideShadow();
        Invoke(nameof(DeactivateInvisibility), invisibilityDuration);
    }
    
    [Server]
    private void DeactivateInvisibility()
    {
        isInvisible = false;
        RpcShowShadow();
    }
    
    [ClientRpc]
    private void RpcHideShadow()
    {
        if (!isLocalPlayer && !isServerOnly)
        {
            SetVisibility(false);
        }
        else
        {
            SetMaterialAlpha(0f);
        }
    }
    
    [ClientRpc]
    private void RpcShowShadow()
    {
        if (!isLocalPlayer && !isServerOnly)
        {
            SetVisibility(true);
        }
        else
        {
            SetMaterialAlpha(1f);
        }
    }
    
    private void SetMaterialAlpha(float alpha)
    {
        if (!meshRenderer) return;
        Material targetMaterial = alpha == 0f ? invisibleMaterial : originalMaterial;
        
        if (targetMaterial.HasProperty("_BaseMap"))
        {
            Color baseColor = targetMaterial.color;
            baseColor.a = alpha;
            targetMaterial.color = baseColor;
        }
        
        meshRenderer.material = targetMaterial;
    }
    
    private void SetVisibility(bool visible)
    {
        // Отключение всех Renderer компонентов
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            renderer.enabled = visible;
        }
        
        // Отключение всех UI компонентов
        var graphicComponents = GetComponentsInChildren<Graphic>();
        foreach (var graphic in graphicComponents)
        {
            graphic.enabled = visible;
        }
        
        // Альтернативно, можно отключить Canvas компоненты
        var canvasComponents = GetComponentsInChildren<Canvas>();
        foreach (var canvas in canvasComponents)
        {
            canvas.enabled = visible;
        }
    }
    
    private void OnDestroy()
    {
        if (invisibleMaterial)
        {
            Destroy(invisibleMaterial);
        }
    }
}