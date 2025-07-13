using Mirror;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class QuestDestination : MonoBehaviour
{
    [Header("Destination Configuration")]
    public string destinationName = "Checkpoint";
    [SerializeField] private GameObject visualMarker;
    [SerializeField] private bool showMarkerOnlyWhenQuestActive = true;

    private Collider _collider;

    private void Start()
    {
        if (visualMarker && !showMarkerOnlyWhenQuestActive)
            visualMarker.SetActive(true);

        _collider = GetComponent<Collider>();
        _collider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        var playerIdentity = other.GetComponent<NetworkIdentity>();
        if (playerIdentity && playerIdentity.isServer)
        {
            // Передаем имя точки назначения в QuestManager
            QuestManager.Instance?.CheckQuestCompletion(playerIdentity, destinationName);
        }
        
        // Визуальная обратная связь на клиенте
        if (playerIdentity && playerIdentity.isLocalPlayer)
        {
            ShowArrivalFeedback();
        }
    }
    
    private void ShowArrivalFeedback()
    {
        // Можно добавить визуальный или звуковой эффект
        if (visualMarker)
        {
            // Простая анимация мигания маркера
            StartCoroutine(FlashMarker());
        }
    }
    
    private System.Collections.IEnumerator FlashMarker()
    {
        if (!visualMarker) yield break;
        
        for (int i = 0; i < 3; i++)
        {
            visualMarker.SetActive(false);
            yield return new WaitForSeconds(0.1f);
            visualMarker.SetActive(true);
            yield return new WaitForSeconds(0.1f);
        }
    }
    
    // Метод для обновления видимости маркера в зависимости от активных квестов
    public void UpdateMarkerVisibility(bool hasActiveQuest)
    {
        if (visualMarker && showMarkerOnlyWhenQuestActive)
        {
            visualMarker.SetActive(hasActiveQuest);
        }
    }
}