using System;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }
    [SerializeField] private List<PlayerRoleMapping> playerRoleMappings = new List<PlayerRoleMapping>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }


    public void OnRoleChanged(RoleType newRole)
    {
        foreach (var roleMapping in playerRoleMappings)
        {
            roleMapping.gameObject.SetActive(roleMapping.playerRole == newRole);
        }
    }
}

[Serializable]
public class PlayerRoleMapping
{
    public RoleType playerRole;
    public GameObject gameObject; 
}
