using UnityEngine;

/// <summary>
/// CS-style dynamic crosshair that expands/contracts based on weapon spread.
/// Requires 4 RectTransform lines (up, down, left, right) as children.
/// Updates every frame for smooth visuals. Zero-GC.
/// </summary>
public class DynamicCrosshair : MonoBehaviour
{
    [Header("Crosshair Lines")]
    [Tooltip("4 RectTransforms: [0]=up, [1]=down, [2]=left, [3]=right")]
    [SerializeField] private RectTransform[] lines = new RectTransform[4];

    [Header("Settings")]
    [Tooltip("Base gap in pixels at zero spread.")]
    [SerializeField] private float baseGap = 10f;
    [Tooltip("Pixels added per degree of spread.")]
    [SerializeField] private float spreadMultiplier = 15f;
    [Tooltip("Smoothing speed for visual transitions.")]
    [SerializeField] private float smoothSpeed = 20f;

    [Header("Appearance")]
    [Tooltip("Color of the crosshair lines.")]
    [SerializeField] private Color crosshairColor = Color.white;
    [Tooltip("Length of each crosshair line in pixels.")]
    [SerializeField] private float lineLength = 12f;
    [Tooltip("Thickness of each crosshair line in pixels.")]
    [SerializeField] private float lineThickness = 2f;

    private float _currentGap;
    private float _targetGap;

    private void Awake()
    {
        _currentGap = baseGap;
        _targetGap = baseGap;
    }

    /// <summary>
    /// Set target spread in degrees. Called from PlayerController.Update().
    /// </summary>
    public void SetSpread(float spreadDegrees)
    {
        _targetGap = baseGap + spreadDegrees * spreadMultiplier;
    }

    private void Update()
    {
        // Smooth interpolation
        _currentGap = Mathf.Lerp(_currentGap, _targetGap, Time.deltaTime * smoothSpeed);

        if (lines == null || lines.Length < 4) return;

        // Position the 4 lines around center
        // [0] = up
        if (lines[0])
        {
            lines[0].anchoredPosition = new Vector2(0f, _currentGap);
            lines[0].sizeDelta = new Vector2(lineThickness, lineLength);
        }

        // [1] = down
        if (lines[1])
        {
            lines[1].anchoredPosition = new Vector2(0f, -_currentGap);
            lines[1].sizeDelta = new Vector2(lineThickness, lineLength);
        }

        // [2] = left
        if (lines[2])
        {
            lines[2].anchoredPosition = new Vector2(-_currentGap, 0f);
            lines[2].sizeDelta = new Vector2(lineLength, lineThickness);
        }

        // [3] = right
        if (lines[3])
        {
            lines[3].anchoredPosition = new Vector2(_currentGap, 0f);
            lines[3].sizeDelta = new Vector2(lineLength, lineThickness);
        }
    }
}
