using System.Collections;
using TMPro;
using UnityEngine;

public class AlertForNickname : MonoBehaviour
{
    [SerializeField] private float showDuration = 2f;
    [SerializeField] private float fadeSpeed = 2f;
    
    private TextMeshProUGUI text;
    private CanvasGroup canvasGroup;
    private Coroutine hideCoroutine;

    private void Awake()
    {
        text = GetComponent<TextMeshProUGUI>();
        canvasGroup = GetComponent<CanvasGroup>();
        
        if (!canvasGroup)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
            
        Hide();
    }

    private void ShowAlert(string message, Color color)
    {
        text.text = message;
        text.color = color;
        
        if (hideCoroutine != null)
            StopCoroutine(hideCoroutine);
            
        gameObject.SetActive(true);
        canvasGroup.alpha = 1f;
        
        hideCoroutine = StartCoroutine(HideAfterDelay());
    }

    public void ShowError(string message)
    {
        ShowAlert(message, Color.red);
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(showDuration);
        
        while (canvasGroup.alpha > 0)
        {
            canvasGroup.alpha -= fadeSpeed * Time.deltaTime;
            yield return null;
        }
        
        Hide();
    }

    private void Hide()
    {
        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }
}