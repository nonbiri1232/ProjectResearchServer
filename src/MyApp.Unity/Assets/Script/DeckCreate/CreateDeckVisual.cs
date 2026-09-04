using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CreateDeckVisual : MonoBehaviour
{
    [Header("UI Area")]
    [SerializeField] private Transform cardPoolArea;
    [SerializeField] private Transform myDeckArea;
    [SerializeField] private TextMeshProUGUI deckSizeText;

    [Header("Prefab")]
    [SerializeField] private GameObject cardButtonPrefab;

    [Header("Card DBS")]
    public CardConect cardDatabase;

    [Header("Card PopUp")]
    public GameObject cardPopupPanel;
    public TextMeshProUGUI cardPopupText;
    private int nowChangeDeck = 1;

    private readonly string[] idToClassName = new string[]
    {
        "SledOverClock", "IncrementProcess", "ClockDownBot", "ParallelCompilation",
        "PoisonPoint", "UnSafeArea", "Master", "Raid10", "RmRf", "Paging",
        "BackGroundMiner", "SystemFreeze", "CarnelPanicZero", "AllDelete",
        "SafeModeOverdrive","ForcedCrashTest","IllegalResourceSale","ForcedDebugMode",
        "RansomwareInfection","LeechProcess","DDoSArea","MultiEncryptionProtocol",
        "TimedLogicBomb","TrojanHorse","MemoryDumpRestore","CoreDumpProcess",
        "ZombieProcess","DeepArchive","RestoreMeister","FakeHoneypot",
        "PingBot","Firewall","DebugProcess","BackupServer",
        "GarbageShredder","Antivirus","Mainframe","DataFetch",
        "ProcessKill","EmergencyEvasion","Override","CacheClear",
        "Format","ApplyPatch","EmergencyPower"
    };
    private List<int> allAvailableCards = new List<int>
    {
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
        10, 11, 12, 13, 14, 15, 16, 17, 18, 19,
        20, 21, 22, 23, 24, 25, 26, 27, 28, 29,
        30, 31, 32, 33, 34, 35, 36, 37, 38, 39,
        40, 41, 42, 43, 44
    };

    void Start()
    {
        DeckManager.LoadDeck();
        UpdateUI();
    }

    public void SelectChangeDeck(int i)
    {
        Debug.Log($"player{i}に変更しました。");
        nowChangeDeck = i;
        UpdateUI();
    }
    private List<int> ChangeDeck()
    {
        switch (nowChangeDeck)
        {
            case 1:
                return DeckManager.player1Deck;
            case 2:
                return DeckManager.player2Deck;
        }
        return null;
    }
    private void UpdateUI()
    {
        if(nowChangeDeck == 1)
        {
            deckSizeText.text = $"Deck1: {DeckManager.player1Deck.Count} / {DeckManager.MAXDECKNUM}";
        }
        else if(nowChangeDeck == 2)
        {
            deckSizeText.text = $"Deck2: {DeckManager.player2Deck.Count} / {DeckManager.MAXDECKNUM}";
        }
        else
        {
            deckSizeText.text = "";
        }
        foreach(Transform child in cardPoolArea) Destroy(child.gameObject);
        foreach(int cardId in allAvailableCards)
        {
            GameObject cardObj = Instantiate(cardButtonPrefab ,cardPoolArea,false);

            
            TextMeshProUGUI btnText = cardObj.GetComponentInChildren<TextMeshProUGUI>();
            btnText.text = $"Cost:{GetCardCost(cardId)}\n{GetCardName(cardId)}"; 

            EventTrigger trigger = cardObj.GetComponent<EventTrigger>();
            if(trigger == null) trigger = cardObj.AddComponent<EventTrigger>();

            EventTrigger.Entry entryEnter = new EventTrigger.Entry();
            entryEnter.eventID = EventTriggerType.PointerEnter;
            entryEnter.callback.AddListener((data)=>{ShowPopUp(GetCardAbility(cardId));});
            trigger.triggers.Add(entryEnter);

            EventTrigger.Entry entryExit = new EventTrigger.Entry();
            entryExit.eventID = EventTriggerType.PointerExit;
            entryExit.callback.AddListener((data)=>{HidePopUp();});
            trigger.triggers.Add(entryExit);
            
            Button btn = cardObj.GetComponent<Button>();
            btn.onClick.AddListener(() => AddToDeck(cardId));
        }
        foreach(Transform child in myDeckArea) Destroy(child.gameObject);
        foreach(int cardId in ChangeDeck())
        {
            GameObject cardObj = Instantiate(cardButtonPrefab ,myDeckArea,false);

            
            TextMeshProUGUI btnText = cardObj.GetComponentInChildren<TextMeshProUGUI>();
            btnText.text = $"Cost:{GetCardCost(cardId)}\n{GetCardName(cardId)}"; 

            EventTrigger trigger = cardObj.GetComponent<EventTrigger>();
            if(trigger == null) trigger = cardObj.AddComponent<EventTrigger>();

            EventTrigger.Entry entryEnter = new EventTrigger.Entry();
            entryEnter.eventID = EventTriggerType.PointerEnter;
            entryEnter.callback.AddListener((data)=>{ShowPopUp(GetCardAbility(cardId));});
            trigger.triggers.Add(entryEnter);

            EventTrigger.Entry entryExit = new EventTrigger.Entry();
            entryExit.eventID = EventTriggerType.PointerExit;
            entryExit.callback.AddListener((data)=>{HidePopUp();});
            trigger.triggers.Add(entryExit);
            
            Button btn = cardObj.GetComponent<Button>();
            btn.onClick.AddListener(() => RemoveToDeck(cardId));
        }

    }
    private void AddToDeck(int cardId)
    {
        DeckManager.AddDeck(nowChangeDeck,cardId);
        UpdateUI();
    }
    private void RemoveToDeck(int cardId)
    {
        DeckManager.RemoveDeck(nowChangeDeck,cardId);
        UpdateUI();
    }
    private CardSetting GetCardSetting(int cardId)
    {
        if (cardDatabase != null && cardId >= 0 && cardId < idToClassName.Length)
        {
            string targetClassName = idToClassName[cardId];
            
            foreach (CardSetting setting in cardDatabase.cards)
            {
                if (setting.className == targetClassName)
                {
                    return setting;
                }
            }
        }
        return null;
    }
    private string GetCardName(int cardId)
    {
        CardSetting setting = GetCardSetting(cardId);
        return setting != null ? setting.displayName : "Unknown";
    }

    private string GetCardAbility(int cardId)
    {
        CardSetting setting = GetCardSetting(cardId);
        return setting != null ? setting.ability : "なし";
    }

    private int GetCardCost(int cardId)
    {
        CardSetting setting = GetCardSetting(cardId);
        return setting != null ? setting.cost : 0;
    }

    private int GetCardAtk(int cardId)
    {
        CardSetting setting = GetCardSetting(cardId);
        return setting != null ? setting.atk : 0;
    }

    private int GetCardHp(int cardId)
    {
        CardSetting setting = GetCardSetting(cardId);
        return setting != null ? setting.hp : 0;
    }

    public void ShowPopUp(string abilityText)
    {
        if (cardPopupText != null) cardPopupText.text = abilityText;
    }

    public void HidePopUp()
    {
        if (cardPopupText != null) cardPopupText.text = "";
    }
}

