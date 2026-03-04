using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class Scoreboard : MonoBehaviour
{
    [SerializeField] private GameObject scoreboardPanel;
    [SerializeField] private Transform redTeamContainer;
    [SerializeField] private Transform blueTeamContainer;
    [SerializeField] private GameObject entryPrefab;
    [SerializeField] private KeyCode scoreboardKey = KeyCode.Tab;

    private readonly List<GameObject> _entries = new();

    private void Update()
    {
        if (Input.GetKeyDown(scoreboardKey))
        {
            ShowScoreboard();
        }

        if (Input.GetKeyUp(scoreboardKey))
        {
            HideScoreboard();
        }
    }

    private void ShowScoreboard()
    {
        if (!scoreboardPanel) return;

        RefreshScoreboard();
        scoreboardPanel.SetActive(true);
    }

    private void HideScoreboard()
    {
        if (scoreboardPanel)
            scoreboardPanel.SetActive(false);
    }

    private void RefreshScoreboard()
    {
        // Clear old entries
        foreach (var entry in _entries)
        {
            if (entry) Destroy(entry);
        }
        _entries.Clear();

        // Find all players
        var allStats = FindObjectsByType<PlayerStats>(FindObjectsSortMode.None);

        // Sort by kills descending
        var sorted = allStats
            .OrderByDescending(s => s.Kills)
            .ThenBy(s => s.Deaths);

        foreach (var stats in sorted)
        {
            var playerState = stats.GetComponent<PlayerState>();
            var playerTeam = stats.GetComponent<PlayerTeam>();
            if (!playerState || !playerTeam) continue;

            Transform container = playerTeam.CurrentTeam == Team.Red ? redTeamContainer : blueTeamContainer;
            if (!container) continue;

            if (!entryPrefab) continue;

            GameObject entry = Instantiate(entryPrefab, container);
            var texts = entry.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length >= 3)
            {
                texts[0].text = playerState.PlayerNickname;
                texts[1].text = stats.Kills.ToString();
                texts[2].text = stats.Deaths.ToString();
            }

            _entries.Add(entry);
        }
    }
}
