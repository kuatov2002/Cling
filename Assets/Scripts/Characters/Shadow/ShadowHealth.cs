using Mirror;
using UnityEngine;

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
            gameObject.SetActive(false);
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
            gameObject.SetActive(true);
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

    private void OnDestroy()
    {
        if (invisibleMaterial)
        {
            Destroy(invisibleMaterial);
        }
    }
}