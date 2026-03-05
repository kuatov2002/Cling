using Mirror;
using UnityEngine;

/// <summary>
/// Debug network statistics overlay. Toggle with F3.
/// Shows RTT, tick rate, prediction error, interpolation delay, etc.
/// </summary>
public class NetworkStatsUI : MonoBehaviour
{
    private bool _visible;
    private GUIStyle _style;
    private GUIStyle _headerStyle;

    // ── Tracked metrics ───────────────────────────────────────────
    private float _rtt;
    private int _tickRate;
    private float _predictionError;
    private float _interpolationDelay;
    private int _inputQueueSize;
    private int _snapshotBufferCount;
    private float _jitterMs;

    // ── Smoothing ─────────────────────────────────────────────────
    private float _smoothRtt;
    private float _smoothPredError;
    private float _updateTimer;
    private const float UpdateInterval = 0.25f;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F3))
            _visible = !_visible;

        if (!_visible) return;

        _updateTimer += Time.deltaTime;
        if (_updateTimer >= UpdateInterval)
        {
            _updateTimer = 0f;
            GatherMetrics();
        }
    }

    private void GatherMetrics()
    {
        // RTT
        _rtt = (float)(NetworkTime.rtt * 1000.0); // ms
        _smoothRtt = Mathf.Lerp(_smoothRtt, _rtt, 0.3f);

        // Tick
        _tickRate = NetworkTickManager.TickRate;

        // Find local player's PlayerMovement for prediction stats
        if (NetworkClient.localPlayer)
        {
            // We can't easily read private fields, but we can display tick info
            _predictionError = 0f; // would need a public accessor
        }
    }

    private void OnGUI()
    {
        if (!_visible) return;

        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = Color.white }
            };
            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.cyan }
            };
        }

        float x = 10f;
        float y = 10f;
        float lineHeight = 20f;
        float bgWidth = 280f;
        float bgHeight = 200f;

        // Background
        GUI.Box(new Rect(x - 5, y - 5, bgWidth, bgHeight), "");

        GUI.Label(new Rect(x, y, bgWidth, lineHeight), "── Network Stats (F3) ──", _headerStyle);
        y += lineHeight + 4;

        GUI.Label(new Rect(x, y, bgWidth, lineHeight), $"RTT: {_smoothRtt:F1} ms", _style);
        y += lineHeight;

        GUI.Label(new Rect(x, y, bgWidth, lineHeight), $"Tick Rate: {_tickRate} Hz", _style);
        y += lineHeight;

        GUI.Label(new Rect(x, y, bgWidth, lineHeight), $"Current Tick: {NetworkTickManager.Tick}", _style);
        y += lineHeight;

        GUI.Label(new Rect(x, y, bgWidth, lineHeight), $"Tick Alpha: {NetworkTickManager.TickAlpha:F3}", _style);
        y += lineHeight;

        bool isServer = NetworkServer.active;
        bool isClient = NetworkClient.active;
        string role = isServer && isClient ? "Host" : isServer ? "Server" : isClient ? "Client" : "None";
        GUI.Label(new Rect(x, y, bgWidth, lineHeight), $"Role: {role}", _style);
        y += lineHeight;

        if (isClient)
        {
            GUI.Label(new Rect(x, y, bgWidth, lineHeight),
                $"Buffer Time: {NetworkClient.bufferTime * 1000:F1} ms", _style);
            y += lineHeight;
        }

        if (isServer)
        {
            GUI.Label(new Rect(x, y, bgWidth, lineHeight),
                $"Connections: {NetworkServer.connections.Count}", _style);
            y += lineHeight;
        }
    }
}
