using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// AI対戦用UI。人間側だけが操作し、AI側の手札は枚数のみ表示する。
/// </summary>
public class AIBattleVisual : MonoBehaviour
{
    [Header("System")]
    [SerializeField] private Button endTurnButton;
    [SerializeField] private Button selfGarbageButton;
    [SerializeField] private TextMeshProUGUI systemText;

    [Header("Human UI")]
    [SerializeField] private TextMeshProUGUI humanMemoryText;
    [SerializeField] private Transform humanHandArea;
    [SerializeField] private Transform humanFieldArea;

    [Header("AI UI")]
    [SerializeField] private TextMeshProUGUI aiMemoryText;
    [SerializeField] private Transform aiFieldArea;

    [Header("Panels")]
    [SerializeField] private GameObject selectPanel;
    [SerializeField] private Transform selectArea;
    [SerializeField] private GameObject mariganPanel;
    [SerializeField] private Transform mariganArea;
    [SerializeField] private Transform scopeArea;
    [SerializeField] private GameObject addCostPanel;
    [SerializeField] private Button addCostYesButton;
    [SerializeField] private Button addCostNoButton;
    [SerializeField] private GameObject endGamePanel;
    [SerializeField] private TextMeshProUGUI endText;

    [Header("Card")]
    [SerializeField] private GameObject cardButtonPrefab;
    [SerializeField] private CardConect cardDatabase;
    [SerializeField] private GameObject cardPopupPanel;
    [SerializeField] private TextMeshProUGUI cardPopupText;

    private AIBattleManager manager;
    private Card pendingPlayCard;
    private Card pendingAttacker;
    private List<Card> pendingTargets = new List<Card>();
    private List<Card> mariganCards = new List<Card>();
    private List<Card> garbageCards = new List<Card>();
    private bool pendingAddCost;
    private bool initialized;
    private bool mariganDrawn;

    public void Initialize(AIBattleManager battleManager)
    {
        manager = battleManager;
        initialized = true;

        if (endTurnButton != null)
        {
            endTurnButton.onClick.RemoveListener(OnEndTurnClicked);
            endTurnButton.onClick.AddListener(OnEndTurnClicked);
        }
        if (selfGarbageButton != null)
        {
            selfGarbageButton.onClick.RemoveListener(OpenSelfGarbage);
            selfGarbageButton.onClick.AddListener(OpenSelfGarbage);
        }

        FindAddCostButtonsIfNeeded();
        if (addCostYesButton != null)
        {
            addCostYesButton.onClick.RemoveListener(PlayWithAdditionalCost);
            addCostYesButton.onClick.AddListener(PlayWithAdditionalCost);
        }
        if (addCostNoButton != null)
        {
            addCostNoButton.onClick.RemoveListener(PlayWithoutAdditionalCost);
            addCostNoButton.onClick.AddListener(PlayWithoutAdditionalCost);
        }

        SetActive(selectPanel, false);
        SetActive(addCostPanel, false);
        SetActive(endGamePanel, false);
        Refresh();
    }

    private void FindAddCostButtonsIfNeeded()
    {
        if (addCostPanel == null ||
            (addCostYesButton != null && addCostNoButton != null)) return;

        foreach (Button button in addCostPanel.GetComponentsInChildren<Button>(true))
        {
            if (addCostYesButton == null && button.gameObject.name == "PlayAddCost")
                addCostYesButton = button;
            else if (addCostNoButton == null && button.gameObject.name == "Play")
                addCostNoButton = button;
        }
    }

    public void Refresh()
    {
        if (!initialized || manager == null || manager.Game == null) return;

        Player human = manager.HumanPlayer;
        Player ai = manager.AIPlayer;
        GameManager game = manager.Game;

        if (systemText != null)
        {
            string turn = manager.IsHumanTurn ? "あなたのターン" : "AIのターン";
            systemText.text = $"{turn}\nフェーズ: {game.currentPhase}";
        }

        if (humanMemoryText != null)
        {
            humanMemoryText.text = BuildPlayerStatus(human, false);
        }
        if (aiMemoryText != null)
        {
            aiMemoryText.text = BuildPlayerStatus(ai, true);
        }

        bool canUseMainControls = manager.IsHumanTurn &&
                                  game.currentState == GameState.WaitingForInput &&
                                  game.currentPhase == PhaseState.Main;
        if (endTurnButton != null) endTurnButton.interactable = canUseMainControls;
        if (selfGarbageButton != null)
        {
            selfGarbageButton.interactable = manager.IsHumanTurn &&
                game.currentState == GameState.WaitingForInput &&
                game.currentPhase == PhaseState.Start && game.systemTurn != 1;
        }

        DrawHand();
        DrawField(human.field, humanFieldArea, true);
        DrawField(ai.field, aiFieldArea, false);
        DrawScope();
        DrawMariganIfNeeded();
    }

    public void ShowGameResult(bool humanWon)
    {
        SetActive(endGamePanel, true);
        if (endText != null)
        {
            string mode = manager.LearnFromHuman ? "\nAIへ学習結果を送信しました" : string.Empty;
            endText.text = (humanWon ? "勝利" : "敗北") + mode;
        }
        CloseSelection();
    }

    // EndGamePanelの「再戦」ボタンから呼ぶ。
    public void StartNextBattle()
    {
        CancelSelection();
        mariganCards.Clear();
        mariganDrawn = false;
        SetActive(endGamePanel, false);
        manager.StartNextBattle();
    }

    // 任意の「投了」ボタンから呼ぶ。AI勝利としてEpisodeを終了する。
    public void Surrender()
    {
        manager.SurrenderHuman();
    }

    private string BuildPlayerStatus(Player player, bool hideHand)
    {
        string hand = hideHand ? $"手札: {player.hand.Count}枚" : $"手札: {player.hand.Count}枚";
        return $"Memory: {player.fieldCost} / {player.maxMemory}\n" +
               $"Used: {player.usedMemory} / {player.usableMemory}\n" +
               $"{hand}  Deck: {player.deck.Count}  Garbage: {player.garbage.Count}";
    }

    private void DrawHand()
    {
        ClearChildren(humanHandArea);
        if (humanHandArea == null) return;

        foreach (Card card in manager.HumanPlayer.hand)
        {
            GameObject cardObject = CreateCardButton(card, humanHandArea, true);
            Button button = cardObject.GetComponent<Button>();
            if (button != null)
            {
                Card captured = card;
                button.onClick.AddListener(() => OnHandCardClicked(captured));
                button.interactable = manager.CanBeginPlay(card, out _);
            }
        }
    }

    private void DrawField(List<Card> cards, Transform area, bool isHumanField)
    {
        ClearChildren(area);
        if (area == null) return;

        foreach (Card card in cards)
        {
            GameObject cardObject = CreateCardButton(card, area, false);
            Button button = cardObject.GetComponent<Button>();
            if (button != null)
            {
                Card captured = card;
                button.onClick.AddListener(() =>
                {
                    if (isHumanField) OnAttackerClicked(captured);
                });
                button.interactable = isHumanField && CanAttack(card);
            }
        }
    }

    private void DrawScope()
    {
        ClearChildren(scopeArea);
        if (scopeArea == null || manager.Game.currentScope == null) return;
        CreateCardButton(manager.Game.currentScope, scopeArea, true);
    }

    private void DrawMariganIfNeeded()
    {
        bool needsMarigan = manager.Game.systemTurn == 1 &&
                            manager.Game.currentPhase == PhaseState.Start &&
                            manager.Game.NeedsMarigan(manager.HumanPlayer);
        SetActive(mariganPanel, needsMarigan);
        if (!needsMarigan)
        {
            mariganDrawn = false;
            return;
        }
        if (mariganArea == null || mariganDrawn) return;

        mariganDrawn = true;
        ClearChildren(mariganArea);
        mariganCards.Clear();
        CreateCommandButton(mariganArea, "決定", SubmitMarigan);

        foreach (Card card in manager.HumanPlayer.hand)
        {
            GameObject cardObject = CreateCardButton(card, mariganArea, false);
            Card captured = card;
            Button button = cardObject.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(() =>
                    ToggleCard(captured, mariganCards, cardObject));
            }
        }
    }

    private void SubmitMarigan()
    {
        if (manager.SubmitMarigan(new List<Card>(mariganCards)))
        {
            mariganCards.Clear();
            mariganDrawn = false;
            SetActive(mariganPanel, false);
        }
    }

    private void OpenSelfGarbage()
    {
        if (!manager.IsHumanTurn || manager.CurrentPhase != PhaseState.Start) return;

        garbageCards.Clear();
        OpenSelection();
        ClearChildren(selectArea);
        CreateCommandButton(selectArea, "決定", SubmitSelfGarbage);

        foreach (Card card in manager.HumanPlayer.field)
        {
            GameObject cardObject = CreateCardButton(card, selectArea, false);
            Card captured = card;
            Button button = cardObject.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(() =>
                    ToggleCard(captured, garbageCards, cardObject));
            }
        }
    }

    private void SubmitSelfGarbage()
    {
        if (manager.SubmitSelfGarbage(new List<Card>(garbageCards)))
        {
            garbageCards.Clear();
            CloseSelection();
        }
    }

    private void OnHandCardClicked(Card card)
    {
        if (!manager.CanBeginPlay(card, out string reason))
        {
            Debug.LogWarning($"カードをプレイできません: {reason}");
            return;
        }

        pendingPlayCard = card;
        pendingTargets.Clear();
        pendingAddCost = false;

        Select select = card.select;
        if (select != null && select.isSelectConstructor)
        {
            List<Card> pool = GetTargetPool(select.whereTarget);
            List<Card> validTargets = pool.FindAll(candidate =>
                card.ValidateTargets(
                    manager.HumanPlayer,
                    manager.AIPlayer,
                    new List<Card>() { candidate }));

            if (validTargets.Count == 0)
            {
                AskAddCostOrPlay();
                return;
            }

            DrawPlayTargets(select, validTargets);
            return;
        }

        AskAddCostOrPlay();
    }

    private void DrawPlayTargets(Select select, List<Card> validTargets)
    {
        OpenSelection();
        ClearChildren(selectArea);
        pendingTargets.Clear();
        CreateCommandButton(selectArea, "決定", ConfirmTargetSelection);

        foreach (Card target in validTargets)
        {
            GameObject cardObject = CreateCardButton(target, selectArea, false);
            Card captured = target;
            Button button = cardObject.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(() =>
                {
                    if (pendingTargets.Contains(captured))
                    {
                        pendingTargets.Remove(captured);
                        SetSelected(cardObject, captured, false);
                    }
                    else
                    {
                        pendingTargets.Add(captured);
                        SetSelected(cardObject, captured, true);
                    }

                    int requiredSelections = Mathf.Min(
                        select.numOfSelect, validTargets.Count);
                    if (pendingTargets.Count >= requiredSelections)
                    {
                        CloseSelection();
                        AskAddCostOrPlay();
                    }
                });
            }
        }
    }

    private void ConfirmTargetSelection()
    {
        CloseSelection();
        AskAddCostOrPlay();
    }

    private void AskAddCostOrPlay()
    {
        if (CanAddCost(pendingPlayCard) && addCostPanel != null)
        {
            SetActive(addCostPanel, true);
        }
        else
        {
            ConfirmPlay(false);
        }
    }

    // AddCostPanelの「払う」ボタンから呼ぶ。
    public void PlayWithAdditionalCost()
    {
        ConfirmPlay(true);
    }

    // AddCostPanelの「払わない」ボタンから呼ぶ。
    public void PlayWithoutAdditionalCost()
    {
        ConfirmPlay(false);
    }

    // GameVisual / LocalBattleVisualの既存UI設定との互換用。
    // ボタンでisAdd(bool)を設定した後、PlayAction()を呼ぶ構成でも動作する。
    public void isAdd(bool addCost)
    {
        pendingAddCost = addCost;
    }

    public void PlayAction()
    {
        ConfirmPlay(pendingAddCost);
    }

    // bool引数を直接渡す既存UIにも対応する。
    public void PlayAction(bool addCost)
    {
        ConfirmPlay(addCost);
    }

    private void ConfirmPlay(bool addCost)
    {
        if (pendingPlayCard == null)
        {
            Debug.LogWarning("追加コストプレイ対象のカードが選択されていません。");
            SetActive(addCostPanel, false);
            pendingAddCost = false;
            return;
        }

        SetActive(addCostPanel, false);
        pendingAddCost = addCost;

        List<Card> targets = pendingTargets.Count > 0
            ? new List<Card>(pendingTargets)
            : null;
        manager.PlayCard(pendingPlayCard, targets, pendingAddCost);

        pendingPlayCard = null;
        pendingTargets.Clear();
        pendingAddCost = false;
    }

    private void OnAttackerClicked(Card attacker)
    {
        if (!CanAttack(attacker)) return;
        pendingAttacker = attacker;

        List<Card> objects = manager.AIPlayer.field.FindAll(
            card => card.Type == Card.CardType.Object);
        if (objects.Count == 0)
        {
            // 現行GameManagerでは初ターン中の直接攻撃はImmediateでも不可。
            if (attacker.isFirstTurn)
            {
                pendingAttacker = null;
                return;
            }
            if (!manager.Attack(attacker))
                Debug.LogWarning("直接攻撃に失敗しました。");
            pendingAttacker = null;
            return;
        }

        bool hasProxy = objects.Exists(card => card.isProxy);
        List<Card> validTargets = objects.FindAll(card =>
            !card.isEncrypted && (!hasProxy || card.isProxy));
        if (validTargets.Count == 0) return;

        OpenSelection();
        ClearChildren(selectArea);
        foreach (Card target in validTargets)
        {
            GameObject cardObject = CreateCardButton(target, selectArea, false);
            Card captured = target;
            Button button = cardObject.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(() =>
                {
                    CloseSelection();
                    if (!manager.Attack(pendingAttacker, captured))
                        Debug.LogWarning("攻撃に失敗しました。");
                    pendingAttacker = null;
                });
            }
        }
    }

    private bool CanAttack(Card card)
    {
        return manager != null && manager.IsHumanTurn &&
               manager.CurrentPhase == PhaseState.Main &&
               card != null && card.Type == Card.CardType.Object &&
               card.isCanAttack && (!card.isFirstTurn || card.isImmediate) &&
               card.isAttacked < card.attackTimes;
    }

    private bool CanAddCost(Card card)
    {
        if (card == null || card.Type != Card.CardType.Object) return false;
        Player player = manager.HumanPlayer;
        return player.fieldCost + card.Cost + 1 <= player.maxMemory &&
               player.usedMemory + card.Cost + 1 <= player.usableMemory;
    }

    private List<Card> GetTargetPool(where targetArea)
    {
        switch (targetArea)
        {
            case where.hand:
                return manager.HumanPlayer.hand;
            case where.selfField:
                return manager.HumanPlayer.field;
            case where.enemyField:
                return manager.AIPlayer.field;
            default:
                return new List<Card>();
        }
    }

    private void OnEndTurnClicked()
    {
        if (!manager.EndTurn()) Debug.LogWarning("ターン終了に失敗しました。");
    }

    public void CancelSelection()
    {
        pendingPlayCard = null;
        pendingAttacker = null;
        pendingTargets.Clear();
        garbageCards.Clear();
        CloseSelection();
        SetActive(addCostPanel, false);
    }

    private GameObject CreateCardButton(Card card, Transform parent, bool passive)
    {
        GameObject cardObject = Instantiate(cardButtonPrefab, parent, false);
        TextMeshProUGUI text = cardObject.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            string stats = card.Type == Card.CardType.Object
                ? $"\nATK:{card.Attack} HP:{card.Hp}"
                : string.Empty;
            text.text = $"Cost:{card.Cost}\n{GetCardName(card)}{stats}";
        }

        ApplyCardColor(cardObject, card);
        AddPopupEvents(cardObject, card);

        Button button = cardObject.GetComponent<Button>();
        if (button != null && passive) button.interactable = false;
        return cardObject;
    }

    private void CreateCommandButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
    {
        if (parent == null) return;
        GameObject command = Instantiate(cardButtonPrefab, parent, false);
        TextMeshProUGUI text = command.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null) text.text = label;
        Button button = command.GetComponent<Button>();
        if (button != null) button.onClick.AddListener(action);
    }

    private void AddPopupEvents(GameObject cardObject, Card card)
    {
        EventTrigger trigger = cardObject.GetComponent<EventTrigger>();
        if (trigger == null) trigger = cardObject.AddComponent<EventTrigger>();

        EventTrigger.Entry enter = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerEnter
        };
        enter.callback.AddListener(_ => ShowPopUp(GetCardAbility(card)));
        trigger.triggers.Add(enter);

        EventTrigger.Entry exit = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerExit
        };
        exit.callback.AddListener(_ => HidePopUp());
        trigger.triggers.Add(exit);
    }

    private void ApplyCardColor(GameObject cardObject, Card card)
    {
        Image image = cardObject.GetComponent<Image>();
        if (image == null) return;

        if (card.Type == Card.CardType.Method)
            image.color = new Color(0.65f, 0.82f, 1f, 1f);
        else if (card.Type == Card.CardType.Scope)
            image.color = new Color(1f, 0.68f, 0.68f, 1f);
        else
            image.color = CanAttack(card)
                ? Color.white
                : new Color(0.75f, 0.75f, 0.75f, 1f);
    }

    private void ToggleCard(Card card, List<Card> selected, GameObject cardObject)
    {
        bool isSelected;
        if (selected.Contains(card))
        {
            selected.Remove(card);
            isSelected = false;
        }
        else
        {
            selected.Add(card);
            isSelected = true;
        }
        SetSelected(cardObject, card, isSelected);
    }

    private void SetSelected(GameObject cardObject, Card card, bool selected)
    {
        Image image = cardObject != null ? cardObject.GetComponent<Image>() : null;
        if (image == null) return;

        if (selected)
            image.color = Color.gray;
        else
            ApplyCardColor(cardObject, card);
    }

    private string GetCardName(Card card)
    {
        CardSetting setting = FindSetting(card);
        return setting != null ? setting.displayName : card.GetType().Name;
    }

    private string GetCardAbility(Card card)
    {
        CardSetting setting = FindSetting(card);
        return setting != null ? setting.ability : "なし";
    }

    private CardSetting FindSetting(Card card)
    {
        if (cardDatabase == null || card == null) return null;
        string className = card.GetType().Name;
        return cardDatabase.cards.Find(setting => setting.className == className);
    }

    private void ShowPopUp(string ability)
    {
        if (cardPopupText != null) cardPopupText.text = ability;
        SetActive(cardPopupPanel, true);
    }

    private void HidePopUp()
    {
        SetActive(cardPopupPanel, false);
    }

    private void OpenSelection()
    {
        SetActive(selectPanel, true);
    }

    private void CloseSelection()
    {
        SetActive(selectPanel, false);
        ClearChildren(selectArea);
    }

    private static void ClearChildren(Transform parent)
    {
        if (parent == null) return;
        foreach (Transform child in parent)
        {
            Destroy(child.gameObject);
        }
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null) target.SetActive(active);
    }
}