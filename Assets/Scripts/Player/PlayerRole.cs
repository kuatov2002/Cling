using Mirror;
using UnityEngine;
using System;

public class PlayerRole : NetworkBehaviour
{
    [SerializeField] private GameObject sheriffStar;
    public event Action<RoleType> OnRoleChanged;

    // SyncVar с хуком будет вызывать OnRoleUpdated при изменении роли на любом клиенте
    [SyncVar(hook = nameof(OnRoleUpdated))]
    private int _roleInt = 0;

    public RoleType CurrentRole
    {
        get => (RoleType)_roleInt;
        set
        {
            if (!isServer) return;
            
            if (CurrentRole != value)
            {
                _roleInt = (int)value;
                // Вызываем событие только на сервере
                OnRoleChanged?.Invoke(value);
            }
        }
    }

    // Этот метод вызывается при старте объекта на всех клиентах
    public override void OnStartClient()
    {
        // Инициализируем отображение звезды на основе текущей роли
        UpdateSheriffStarVisibility((RoleType)_roleInt);
    }

    // Этот метод вызывается только для объекта локального игрока
    public override void OnStartLocalPlayer()
    {
        // Подписываемся на изменения роли
        OnRoleChanged += HandleRoleChanged;
        
        // Для локального игрока также информируем UI менеджер
        OnRoleChanged += UIManager.Instance.OnRoleChanged;
        
        // Вызываем событие для инициализации UI
        OnRoleChanged?.Invoke(CurrentRole);
    }

    private void HandleRoleChanged(RoleType newRole)
    {
        Debug.Log($"Player role changed to: {newRole}");
        // Здесь можем обновить UI или другие характеристики на основе роли
        // НЕ обновляем здесь звезду! Этот метод вызывается только для локального игрока
    }

    // Этот метод вызывается на всех клиентах при изменении _roleInt
    private void OnRoleUpdated(int oldValue, int newValue)
    {
        RoleType oldRole = (RoleType)oldValue;
        RoleType newRole = (RoleType)newValue;
        
        // Обновляем видимость звезды для всех клиентов
        UpdateSheriffStarVisibility(newRole);
        
        // Если это локальный игрок, также вызываем событие для обновления UI
        if (isLocalPlayer && oldRole != newRole)
        {
            OnRoleChanged?.Invoke(newRole);
        }
    }
    
    // Метод для обновления видимости звезды шерифа
    private void UpdateSheriffStarVisibility(RoleType role)
    {
        if (sheriffStar != null)
        {
            sheriffStar.SetActive(role == RoleType.Sheriff);
        }
    }
    
    // При уничтожении объекта отписываемся от событий
    private void OnDestroy()
    {
        if (isLocalPlayer)
        {
            OnRoleChanged -= HandleRoleChanged;
            
            // Отписываем UI менеджер, если он существует
            if (UIManager.Instance != null)
            {
                OnRoleChanged -= UIManager.Instance.OnRoleChanged;
            }
        }
    }
}
public enum RoleType
{
    None,
    Sheriff,
    Deputy,
    Outlaw,
    Renegade
}