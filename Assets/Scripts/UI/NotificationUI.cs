using System.Collections;
using TMPro;
using UnityEngine;

public class NotificationUI : MonoBehaviour
{
    private TextMeshProUGUI _messageText;
    private CanvasGroup _canvasGroup;

    private void Awake()
    {
        _messageText = GetComponent<TextMeshProUGUI>();
        _canvasGroup = GetComponent<CanvasGroup>();
    }

    public void Setup(string message)
    {
        if (_messageText != null)
        {
            _messageText.text = message;
        }

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
        }
    }

    public IEnumerator FadeOut(float duration)
    {
        if (_canvasGroup == null) yield break;

        float startAlpha = _canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = elapsed / duration;
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, normalizedTime);
            yield return null;
        }

        _canvasGroup.alpha = 0f;
    }
}