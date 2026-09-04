using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class BattleUIManager : MonoBehaviour
{
    [Header("System")]
    public BattleManager battleManager;
    public PlayerInputManager inputManager;
    public Button endTurnButton;
    public TextMeshProUGUI systemText;

    [Header("self UI")]
    public TextMeshProUGUI selfDataText; 
    public TextMeshProUGUI selfusedMemory; //１ターンですでに使ったメモリ
    public TextMeshProUGUI selfusableMemory; //1ターン中に使用可能なメモリ
    public TextMeshProUGUI selffieldMemory; //使用済みメモリ
    public TextMeshProUGUI selfmaxMemory; //最大メモリ

    [Header("Enemy UI")]
    public TextMeshProUGUI enemyDataText; 
    public TextMeshProUGUI enemyusedMemory; //１ターンですでに使ったメモリ
    public TextMeshProUGUI enemyusableMemory; //1ターン中に使用可能なメモリ
    public TextMeshProUGUI enemyfieldMemory; //使用済みメモリ
    public TextMeshProUGUI enemymaxMemory; //最大メモリ
    [Header("DrawField")] 
    public GameObject witchPlay;
    public GameObject SelectCard;
    public GameObject MariganField;
    public Button mariganConfirmButton;
    [Header("Card DBS")]
    public CardConect cardDatabase;
    [Header("Card PopUp")]
    public GameObject cardPopupPanel;
    public TextMeshProUGUI cardPopupText;
    [Header("End Game")]
    public GameObject endGame;
    public TextMeshProUGUI endText;
    private void Start()
    {
        if (mariganConfirmButton != null) 
        {
            mariganConfirmButton.onClick.AddListener(() => DecideMarigan());
        }
        if(endTurnButton != null)
        {
            endTurnButton.onClick.AddListener(()=>OnEndTurnClicked());
        }
    }
    private void GameEnd(bool win)
    {
        endGame.SetActive(true);
        if(win)
        {
            endText.text = "勝利";
        }
        else
        {
            endText.text = "敗北";
        }
    }
    private void DecideMarigan()
    {
        mariganConfirmButton.gameObject.SetActive(false);
        inputManager.ConfirmMarigan();
    }
    
    public void ShowMarigan()
    {
        if (MariganField != null) MariganField.SetActive(true);
        if (mariganConfirmButton != null) mariganConfirmButton.gameObject.SetActive(true);
    }

    public void HideMarigan()
    {
        if (MariganField != null) MariganField.SetActive(false);
        if (mariganConfirmButton != null) mariganConfirmButton.gameObject.SetActive(false);
    }
    public void OnEndTurnClicked()
    {
        battleManager.SubmitEndTurn();
    }
    public void UpdateUI(GameManager gm,Player self,Player enemy)
    {
        systemText.text = (gm.turn == self ? "Turn: You" : "Turn:") + " " + $" Phase: {gm.currentPhase}";
        selfDataText.text = $"Hand:{self.hand.Count} Deck:{self.deck.Count} Garbage:{self.garbage.Count}";
        enemyDataText.text = $"Hand:{enemy.hand.Count} Deck:{enemy.deck.Count} Garbage:{enemy.garbage.Count}";
        selffieldMemory.text = $"{self.fieldCost}";
        selfmaxMemory.text = $"{self.maxMemory}";
        selfusableMemory.text = $"{self.usableMemory}";
        selfusedMemory.text = $"{self.usedMemory}";
        enemyfieldMemory.text = $"{enemy.fieldCost}";
        enemymaxMemory.text = $"{enemy.maxMemory}";
        enemyusableMemory.text = $"{enemy.usableMemory}";
        enemyusedMemory.text = $"{enemy.usedMemory}";
    }
    public void ShowPopUp(string abilityText)
    {
        cardPopupText.text = abilityText;
        cardPopupPanel.SetActive(true);
    }
    public void HidePopUp()
    {
        cardPopupPanel.SetActive(false);
    }
}