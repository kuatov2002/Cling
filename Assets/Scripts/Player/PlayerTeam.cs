using System;
using Mirror;
using UnityEngine;

public enum Team
{
    None,
    Red,
    Blue
}

public class PlayerTeam : NetworkBehaviour
{
    public event Action<Team> OnTeamChanged;

    [SyncVar(hook = nameof(OnTeamUpdated))]
    private Team _currentTeam = Team.None;

    public Team CurrentTeam
    {
        get => _currentTeam;
        set
        {
            if (!isServer || _currentTeam == value) return;
            _currentTeam = value;
            OnTeamChanged?.Invoke(value);
        }
    }

    private void OnTeamUpdated(Team oldTeam, Team newTeam)
    {
        UpdateTeamVisuals(newTeam);
        if (isLocalPlayer && oldTeam != newTeam)
            OnTeamChanged?.Invoke(newTeam);
    }

    private void UpdateTeamVisuals(Team team)
    {
        PlayerVisual visual = GetComponent<PlayerVisual>();
        if (visual != null)
            visual.SetTeamColor(team);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        UpdateTeamVisuals(_currentTeam);
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        OnTeamChanged?.Invoke(_currentTeam);
    }
}
