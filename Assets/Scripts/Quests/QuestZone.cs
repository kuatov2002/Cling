using System.Linq;
using Mirror;
using UnityEngine;
using Random = UnityEngine.Random;

public class QuestZone : NetworkBehaviour
{
    [Header("Quest Zone Configuration")]
    [SerializeField] private string zoneName = "Quest Giver";
    [SerializeField] private Quest[] availableQuests;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private GameObject visualIndicator;
    
    [Header("Quest Templates")]
    [SerializeField] private QuestTemplate[] questTemplates;
    
    private void Start()
    {
        if (visualIndicator)
            visualIndicator.SetActive(false);
            
        GenerateQuestsFromTemplates();
    }
    
    private void GenerateQuestsFromTemplates()
    {
        if (questTemplates == null || questTemplates.Length == 0) return;
        
        availableQuests = new Quest[questTemplates.Length];
        for (int i = 0; i < questTemplates.Length; i++)
        {
            var template = questTemplates[i];
            var destination = FindDestinationByName(template.destinationName);
            
            if (destination != null)
            {
                availableQuests[i] = new Quest(
                    template.questId,
                    template.questName,
                    template.description,
                    template.reward,
                    destination.transform.position,
                    template.destinationName,
                    template.isRepeatable,
                    template.repeatCooldown
                );
            }
        }
    }
    
    private QuestDestination FindDestinationByName(string name)
    {
        var destinations = FindObjectsByType<QuestDestination>(FindObjectsSortMode.None);
        return destinations.FirstOrDefault(dest => dest.destinationName == name);
    }
    
    private void OnTriggerStay(Collider other)
    {
        var playerIdentity = other.GetComponent<NetworkIdentity>();
        if (playerIdentity && playerIdentity.isLocalPlayer && Input.GetKeyDown(interactKey))
        {
            var playerInput = playerIdentity.GetComponent<PlayerQuest>();
            if (playerInput)
            {
                playerInput.CmdInteractWithQuestZone(netId);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        var inventory = other.GetComponent<PlayerInventory>();
        if (inventory && inventory.isLocalPlayer)
        {
            ShowInteractionPrompt(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var inventory = other.GetComponent<PlayerInventory>();
        if (inventory && inventory.isLocalPlayer)
        {
            ShowInteractionPrompt(false);
        }
    }
    
    private void ShowInteractionPrompt(bool show)
    {
        if (visualIndicator)
            visualIndicator.SetActive(show);

        UIManager.Instance?.UpdateInteractText(show
            ? $"Нажмите {interactKey} чтобы взять квест у {zoneName}"
            : string.Empty);
    }
    
    [Server]
    public void TryGiveQuest(NetworkIdentity playerIdentity)
    {
        if (availableQuests == null || availableQuests.Length == 0) return;
    
        if (playerIdentity == null)
        {
            Debug.LogWarning("Player has no NetworkIdentity");
            return;
        }
    
        Quest questToGive = availableQuests[Random.Range(0, availableQuests.Length)];
    
        if (QuestManager.Instance && QuestManager.Instance.TryGiveQuest(playerIdentity, questToGive))
        {
            Debug.Log($"Quest given to player {playerIdentity.netId}: {questToGive.questName}");
        }
        else
        {
            Debug.Log($"Failed to give quest to player {playerIdentity.netId}");
        }
    }
}