using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NotificationSystem : MonoBehaviour
{
    [SerializeField] private GameObject notificationPrefab;
    [SerializeField] private Transform notificationParent;
    [SerializeField] private float notificationDuration = 3f;
    [SerializeField] private float fadeOutDuration = 0.5f;
    [SerializeField] private float spacing = 10f;
    
    private Queue<GameObject> _activeNotifications = new Queue<GameObject>();
    private const int MaxNotifications = 5;

    public void ShowNotification(string message)
    {
        if (notificationPrefab == null || notificationParent == null) return;

        GameObject notification = Instantiate(notificationPrefab, notificationParent);
        NotificationUI notificationUI = notification.GetComponent<NotificationUI>();
        
        if (notificationUI != null)
        {
            notificationUI.Setup(message);
        }

        _activeNotifications.Enqueue(notification);
        
        // Remove oldest notification if exceeding max limit
        if (_activeNotifications.Count > MaxNotifications)
        {
            GameObject oldestNotification = _activeNotifications.Dequeue();
            if (oldestNotification != null)
            {
                Destroy(oldestNotification);
            }
        }

        UpdateNotificationPositions();
        StartCoroutine(DestroyNotificationAfterDelay(notification));
    }

    private void UpdateNotificationPositions()
    {
        int index = 0;
        foreach (GameObject notification in _activeNotifications)
        {
            if (notification != null)
            {
                RectTransform rectTransform = notification.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    rectTransform.anchoredPosition = new Vector2(0, -index * spacing);
                }

                index++;
            }
        }
    }

    private IEnumerator DestroyNotificationAfterDelay(GameObject notification)
    {
        yield return new WaitForSeconds(notificationDuration);
        
        if (notification != null)
        {
            NotificationUI notificationUI = notification.GetComponent<NotificationUI>();
            if (notificationUI != null)
            {
                yield return StartCoroutine(notificationUI.FadeOut(fadeOutDuration));
            }
            
            // Remove from queue
            Queue<GameObject> tempQueue = new Queue<GameObject>();
            while (_activeNotifications.Count > 0)
            {
                GameObject current = _activeNotifications.Dequeue();
                if (current != notification)
                {
                    tempQueue.Enqueue(current);
                }
            }

            _activeNotifications = tempQueue;
            
            Destroy(notification);
            UpdateNotificationPositions();
        }
    }
}