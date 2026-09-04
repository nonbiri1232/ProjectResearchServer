using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class LocalBattleVisual : MonoBehaviour
{
    [Header("System")]
    public Button endTurnButton;
    
    public Button selfGarbageButton;
    public TextMeshProUGUI systemText;

    [Header("Self UI")]
    public TextMeshProUGUI p1MemoryText;
    public Transform p1HandArea;
    public Transform p1FieldArea;

    [Header("Enemy UI")]
    public TextMeshProUGUI p2MemoryText;
    public Transform p2FieldArea;

    [Header("Prefabs")]
    public GameObject cardButtonPrefab;
    [Header("DrawField")] 
    public GameObject witchPlay;
    public GameObject witchPlaySelect;
    public GameObject SelectCard;
    public GameObject MariganField;
    public GameObject MariganFieldPlayer1;
    public GameObject ScopeArea;
    [Header("Card DBS")]
    public CardConect cardDatabase;
    [Header("Card PopUp")]
    public GameObject cardPopupPanel;
    public TextMeshProUGUI cardPopupText;
    [Header("LocalBattleManager")]
    public LocalBattleManager battleManager;
    [Header("End Game")]
    public GameObject endGame;
    public TextMeshProUGUI endText;
    //保持データ
    private List<Card> selfHand;
    private List<Card> selfField;
    private List<Card> enemyField;
    private int[] selfMemory = new int[7];
    /*
    0:手札枚数
    1:墓場枚数
    2:フィールドのメモリ
    3:フィールドで使ったメモリ
    4:使えるメモリ
    5:使ったメモリ
    6:デッキ枚数*/
    private int[] enemyMemory = new int[7];//上と同様
    private bool isMyTurn;
    private Card Scope;
    private bool isMarigan;
    private Card selectedPlayCard;
    private int selectedPlayIndex;
    private Card selectedAttackCard;
    private int selectedAttackIndex;
    List<Card> selectList = new List<Card>();
    List<int> selectIndexList = new List<int>();
    private bool isCostAdd;
    public void isAdd(bool cost)
    {
        isCostAdd = cost;
    }
    public void OpenSelectCard()
    {
        SelectCard.SetActive(true);
    }
    void Start()
    {
        isMarigan = false;
        endTurnButton.onClick.AddListener(OnEndTurnClicked);
        selfGarbageButton.onClick.AddListener(OnSelfGarbageClicked);
        MariganField.SetActive(true);
        endGame.SetActive(false);
    }
    private void ApplyCardColor(GameObject cardObject, Card card)
    {
        Image image = cardObject.GetComponent<Image>();
        if(image == null || card == null)return;

        if(card.Type == Card.CardType.Method)
            image.color = new Color(0.65f, 0.82f, 1f, 1f);
        else if(card.Type == Card.CardType.Scope)
            image.color = new Color(1f, 0.68f, 0.68f, 1f);
        else
            image.color = card.isCanAttack
                ? Color.white
                : new Color(0.75f, 0.75f, 0.75f, 1f);
    }

    public void EndGame(bool iswin)
    {
        endGame.SetActive(true);
        if (iswin)
        {
            endText.text = "勝利";
        }
        else
        {
            endText.text = "敗北";
        }
    }
    public void DrawGame()
    {
        endGame.SetActive(true);
        endText.text = "引き分け";
    }
    public void OnSelfGarbageClicked()
    {
        if(battleManager.CurrentPhase != PhaseState.Start) return;
        if(!battleManager.IsMyTurn) return;
        if(selfField.Count == 0)
        {
            DecideSelfGarbage();
            return;
        }
        SelectCard.SetActive(true);
        DrawSelectCardSelfGarbage();
    }
    private void DrawSelectCardSelfGarbage()
    {
        Transform selectArea = SelectCard.GetComponent<Transform>();
        foreach(Transform child in selectArea)
        {
            Destroy(child.gameObject);
        }
        List<Card> field = selfField;

        GameObject decide = Instantiate(cardButtonPrefab, selectArea);

        TextMeshProUGUI decideText = decide.GetComponentInChildren<TextMeshProUGUI>();
        
        decideText.text = $"Decide"; 

        Button decidebtn = decide.GetComponent<Button>();

        decidebtn.onClick.AddListener(()=>DecideSelfGarbage());
        int index = 0;
        foreach(Card c in field)
        {
            GameObject cardObj = Instantiate(cardButtonPrefab, selectArea);

            TextMeshProUGUI btnText = cardObj.GetComponentInChildren<TextMeshProUGUI>();
            
            btnText.text = $"Cost:{c.Cost}\n{GetCardName(c)}\nATK:{c.Attack} HP:{c.Hp}"; 
            ApplyCardColor(cardObj, c);

            Button btn = cardObj.GetComponent<Button>();
            int targetIndex = index;
            btn.onClick.AddListener(()=>AddSelfGarbage(c,targetIndex,cardObj));
            index++;
        }
    }
    List<Card> selfGarbageList = new List<Card>();
    List<int> selfGarbageIndexList = new List<int>();
    private void AddSelfGarbage(Card c,int index,GameObject obj)
    {
        Image img = obj.GetComponent<Image>();
        if (selfGarbageList.Contains(c))
        {
            selfGarbageList.Remove(c);        
            selfGarbageIndexList.Remove(index);
            ApplyCardColor(obj, c);
        }
        else
        {
            selfGarbageList.Add(c); 
            selfGarbageIndexList.Add(index);
            img.color = Color.gray;
        }
    }
    private void DecideSelfGarbage()
    {
        SelectCard.SetActive(false);
        battleManager.GarbageActionRpc(selfGarbageIndexList.ToArray());
        selfGarbageList.Clear();
        selfGarbageIndexList.Clear();
    }

    private void OnEndTurnClicked()
    {
        Debug.Log("ターン終了ボタンが押されました！");
        if (battleManager == null) {
            Debug.LogError("LocalBattleManagerがアサインされていません！");
            return;
        }
        battleManager.TurnEndRpc();
    }
    public void CancelPlay()
    {
        selectedPlayCard = null;

        UpdateUI();
    }
    public void UpdateUI()
    {
        if (!isMarigan)
        {
            DrawMarigan();
            isMarigan = true;
        }
        systemText.text = (battleManager.IsMyTurn ? "MyTurn" : "EnemyTurn") + " " + $" Phase: {battleManager.CurrentPhase}";
        p1MemoryText.text = $"field/maxMemory: {selfMemory[3]} / {selfMemory[2]} \nused/usable: {selfMemory[5]} / {selfMemory[4]} \nhandNum {selfMemory[0]} \ndeckNum {selfMemory[6]}\n garbageNum {selfMemory[1]}";
        p2MemoryText.text = $"field/maxMemory: {enemyMemory[3]} / {enemyMemory[2]} \nused/usable: {enemyMemory[5]} / {enemyMemory[4]} \nhandNum {enemyMemory[0]} \ndeckNum {enemyMemory[6]}\n garbageNum {enemyMemory[1]}";


        endTurnButton.interactable = battleManager.IsMyTurn;

        systemText.text = (battleManager.IsMyTurn ? "自分のターン" : "相手のターン") +$"\n現在のフェイズ: {battleManager.CurrentPhase}";
        DrawField();
        DrawHand();
        DrawScope();
        Debug.Log("画面を更新しました");
    }
    public void SetupInitialBoard(CardData[] selfHand,CardData[] selfField,CardData[] enemyField,int[] selfMemory,int[] enemyMemory,int currentScope)
    {
        this.selfHand = Player.ChangeCard(selfHand);
        this.selfField = Player.ChangeCard(selfField);
        this.enemyField = Player.ChangeCard(enemyField);
        this.selfMemory = selfMemory;
        this.enemyMemory = enemyMemory;
        if(currentScope != -1)
            Scope = Card.CreateCardInstance(currentScope);
        UpdateUI();
    }
    //マリガン用関数
    private void DrawMarigan()
    {
        Transform trf1 = MariganFieldPlayer1.GetComponent<Transform>();
        foreach(Transform t in trf1)
        {
            Destroy(t.gameObject);
        }
        //player1のマリガン決定ボタン表示
        GameObject decide1 = Instantiate(cardButtonPrefab,trf1);

        TextMeshProUGUI btnText1 = decide1.GetComponentInChildren<TextMeshProUGUI>();
            
        btnText1.text = $"Decide"; 

        Button btn1 = decide1.GetComponent<Button>();

        btn1.onClick.AddListener(()=>DecideMarigan(decide1));

        foreach(Card c in selfHand)
        {
            GameObject cardObj = Instantiate(cardButtonPrefab,trf1);

            TextMeshProUGUI btnText = cardObj.GetComponentInChildren<TextMeshProUGUI>();
            
            btnText.text = $"Cost:{c.Cost}\n{GetCardName(c)}\nATK:{c.Attack} HP:{c.Hp}"; 
            ApplyCardColor(cardObj, c);

            Button btn = cardObj.GetComponent<Button>();

            btn.onClick.AddListener(()=>AddMarigan(Card.GetCardId(c),cardObj));
        }
    }
    private void DecideMarigan(GameObject bt)
    {
        bt.SetActive(false);
        battleManager.DecideMariganRpc(marigan.ToArray());
    }
    List<int> marigan = new List<int>();
    private void AddMarigan(int c,GameObject obj)
    {
    
        Image img = obj.GetComponent<Image>();
        if (marigan.Contains(c))
        {
            marigan.Remove(c);
            ApplyCardColor(obj, Card.CreateCardInstance(c));
        }
        else
        {
            marigan.Add(c); 
            img.color = Color.gray;
        }
    }
    public void EndMarigan()
    {
        MariganField.SetActive(false);
    }
    private void DrawField()
    {
        int i = 0;
        //自陣のフィールド
        foreach(Transform child in p1FieldArea)
        {
            Destroy(child.gameObject);
        }
        foreach(Card c in selfField)
        {
            GameObject cardObj = Instantiate(cardButtonPrefab, p1FieldArea);

            TextMeshProUGUI btnText = cardObj.GetComponentInChildren<TextMeshProUGUI>();
            
            btnText.text = $"Cost:{c.Cost}\n{GetCardName(c)}\nATK:{c.Attack} HP:{c.Hp}"; 
            ApplyCardColor(cardObj, c);

            EventTrigger trigger = cardObj.GetComponent<EventTrigger>();
            if(trigger == null) trigger = cardObj.AddComponent<EventTrigger>();

            EventTrigger.Entry entryEnter = new EventTrigger.Entry();
            entryEnter.eventID = EventTriggerType.PointerEnter;
            entryEnter.callback.AddListener((data)=>{ShowPopUp(GetCardAbility(c));});
            trigger.triggers.Add(entryEnter);

            EventTrigger.Entry entryExit = new EventTrigger.Entry();
            entryExit.eventID = EventTriggerType.PointerExit;
            entryExit.callback.AddListener((data)=>{HidePopUp();});
            trigger.triggers.Add(entryExit);

            Button btn = cardObj.GetComponent<Button>();
            int fieldIndex = i;
            btn.onClick.AddListener(()=>OnClickCardField(c,fieldIndex));
            i++;
        }
        //敵陣のフィールド
        foreach(Transform child in p2FieldArea)
        {
            Destroy(child.gameObject);
        }
        foreach(Card c in enemyField)
        {
            GameObject cardObj = Instantiate(cardButtonPrefab, p2FieldArea);

            TextMeshProUGUI btnText = cardObj.GetComponentInChildren<TextMeshProUGUI>();
            
            btnText.text = $"Cost:{c.Cost}\n{GetCardName(c)}\nATK:{c.Attack} HP:{c.Hp}"; 
            ApplyCardColor(cardObj, c);

            EventTrigger trigger = cardObj.GetComponent<EventTrigger>();
            if(trigger == null) trigger = cardObj.AddComponent<EventTrigger>();

            EventTrigger.Entry entryEnter = new EventTrigger.Entry();
            entryEnter.eventID = EventTriggerType.PointerEnter;
            entryEnter.callback.AddListener((data)=>{ShowPopUp(GetCardAbility(c));});
            trigger.triggers.Add(entryEnter);

            EventTrigger.Entry entryExit = new EventTrigger.Entry();
            entryExit.eventID = EventTriggerType.PointerExit;
            entryExit.callback.AddListener((data)=>{HidePopUp();});
            trigger.triggers.Add(entryExit);
        }
    }
    private void OnClickCardField(Card c, int fieldIndex)
    {
        if(battleManager.CurrentPhase != PhaseState.Main) return;
        selectedAttackCard = c;
        selectedAttackIndex = fieldIndex;
        if(battleManager.IsMyTurn)
        {
            battleManager.AskCanAttackRpc(fieldIndex);
            
        }
    }
    public void OpenAttackSelectUI(int fieldIndex)
    {
        /*if(selectedAttackCard != selfField[fieldIndex])
        {
            Debug.Log("インデックスと選択されたカードがずれています。");
            return;
        }*/
        SelectCard.SetActive(true);
        if(enemyField.Count > 0)
        {
            DrawSelectCard();
        }
        else
        {
            DirectAttack();
        }
    }
    private void DrawSelectCard()
    {
        int index = 0;
        Transform selectArea = SelectCard.GetComponent<Transform>();
        foreach(Transform child in selectArea)
        {
            Destroy(child.gameObject);
        }
        foreach(Card c in enemyField)
        {
            GameObject cardObj = Instantiate(cardButtonPrefab, selectArea);

            TextMeshProUGUI btnText = cardObj.GetComponentInChildren<TextMeshProUGUI>();
            
            btnText.text = $"Cost:{c.Cost}\n{GetCardName(c)}\nATK:{c.Attack} HP:{c.Hp}"; 
            ApplyCardColor(cardObj, c);

            Button btn = cardObj.GetComponent<Button>();
            int targetIndex = index;
            btn.onClick.AddListener(()=>AttackAction(targetIndex));
            index++;
        }
    }
    
    private void DirectAttack()
    {
        Transform selectArea = SelectCard.GetComponent<Transform>();
        foreach(Transform child in selectArea)
        {
            Destroy(child.gameObject);
        }
        GameObject cardObj = Instantiate(cardButtonPrefab, selectArea);

        TextMeshProUGUI btnText = cardObj.GetComponentInChildren<TextMeshProUGUI>();
        
        btnText.text = $"DirectAttack"; 

        Button btn = cardObj.GetComponent<Button>();

        btn.onClick.AddListener(()=>AttackAction(-1));
    }

    private void AttackAction(int targetIndex)
    {
        SelectCard.SetActive(false);
        battleManager.AttackActionRpc(selectedAttackIndex,targetIndex);
    }
    private void DrawHand()
    {
        foreach(Transform child in p1HandArea)
        {
            Destroy(child.gameObject);
        }
        int index = 0;
        foreach(Card c in selfHand)
        {
            GameObject cardObj = Instantiate(cardButtonPrefab, p1HandArea);

            TextMeshProUGUI btnText = cardObj.GetComponentInChildren<TextMeshProUGUI>();
            
            btnText.text = $"Cost:{c.Cost}\n{GetCardName(c)}\nATK:{c.Attack} HP:{c.Hp}"; 
            ApplyCardColor(cardObj, c);

            EventTrigger trigger = cardObj.GetComponent<EventTrigger>();
            if(trigger == null) trigger = cardObj.AddComponent<EventTrigger>();

            EventTrigger.Entry entryEnter = new EventTrigger.Entry();
            entryEnter.eventID = EventTriggerType.PointerEnter;
            entryEnter.callback.AddListener((data)=>{ShowPopUp(GetCardAbility(c));});
            trigger.triggers.Add(entryEnter);

            EventTrigger.Entry entryExit = new EventTrigger.Entry();
            entryExit.eventID = EventTriggerType.PointerExit;
            entryExit.callback.AddListener((data)=>{HidePopUp();});
            trigger.triggers.Add(entryExit);

            Button btn = cardObj.GetComponent<Button>();
            int handIndex = index;
            btn.onClick.AddListener(()=>OnClickCardHand(c,handIndex,c.select));
            index++;
        }
    }
    private void OnClickCardHand(Card c,int handIndex,Select select)
    {
        if(battleManager.CurrentPhase != PhaseState.Main) return;
        if(!battleManager.IsMyTurn) return;

        int remainMaxMem = selfMemory[2] - selfMemory[3];
        int remainUsableMem = selfMemory[4] - selfMemory[5];
        if(c.Cost > remainMaxMem || c.Cost > remainUsableMem)
        {
            Debug.LogWarning("コストが足りません！");
            return;
        }
        selectedPlayCard = c;
        selectedPlayIndex = handIndex;

        bool canAddCost = (c.Cost + 1 <= remainMaxMem) && (c.Cost + 1 <= remainUsableMem);
        if(c.Type == Card.CardType.Object)
        {
            Debug.Log($"オブジェクトがプレイされました。");
            if(select.whereTarget == where.hand || (select.isSelectConstructor && IsSelf(select).Count > 0)){
                DrawSelectCard(select);
                if(canAddCost)
                {
                    witchPlaySelect.SetActive(true); // +1コスト払うか聞くUI
                }
                else
                {
                    SelectCard.SetActive(true); // 通常プレイでターゲット選択UIへ
                    isCostAdd = false;
                }
            }
            else // ターゲット選択が不要な場合
            {    
                if(canAddCost)
                {
                    witchPlay.SetActive(true); // +1コスト払うか聞くUI
                }
                else
                {
                    // ターゲット不要・追加コストなしなら、そのままRPCでホストへ送信！
                    battleManager.PlayActionRpc(false, handIndex);
                }
            }
        }
        else if(c.Type == Card.CardType.Method)
        {
            Debug.Log($"メソッドがプレイされました。");
            if(select.whereTarget == where.hand || (select.isSelectConstructor && IsSelf(select).Count > 0))
            {
                SelectCard.SetActive(true);
                DrawSelectCard(select);
            }
            else
            {
                // ターゲット不要なら、そのままRPCでホストへ送信！
                battleManager.PlayActionRpc(false, handIndex);
            }
        }
        else if(c.Type == Card.CardType.Scope)
        {
            Debug.Log($"スコープがプレイされました。");
            battleManager.PlayActionRpc(false, handIndex);
        }
        
    }
    public void PlayAction()
    {
        battleManager.PlayActionRpc(isCostAdd, selectedPlayIndex);
    }
    private void DrawSelectCard(Select select)
    {
        Transform selectArea = SelectCard.GetComponent<Transform>();
        foreach(Transform child in selectArea)
        {
            Destroy(child.gameObject);
        }
        List<Card> field;
        if (select.whereTarget == where.selfField)
        {
            
            field = selfField;
        }
        else if(select.whereTarget == where.enemyField)
        {
            field = enemyField;
        }
        else
        {
            field = selfHand;
        }
        int index = 0;
        foreach(Card c in field)
        {
            GameObject cardObj = Instantiate(cardButtonPrefab, selectArea);

            TextMeshProUGUI btnText = cardObj.GetComponentInChildren<TextMeshProUGUI>();
            
            btnText.text = $"Cost:{c.Cost}\n{GetCardName(c)}\nATK:{c.Attack} HP:{c.Hp}"; 
            ApplyCardColor(cardObj, c);

            Button btn = cardObj.GetComponent<Button>();

            int fieldIndex = index;
            btn.onClick.AddListener(()=>OnclickSelect(select,c,fieldIndex,cardObj));
            index++;
        }
    }
    int selectNum = 0;
    private void OnclickSelect(Select select,Card c,int fieldIndex,GameObject obj)
    {
        Image img = obj.GetComponent<Image>();
        if (selectList.Contains(c))
        {
            selectNum--;
            selectList.Remove(c);
            selectIndexList.Remove(fieldIndex);       
            ApplyCardColor(obj, c);
        }
        else
        {
            selectNum++;
            selectList.Add(c); 
            selectIndexList.Add(fieldIndex);
            img.color = Color.gray;
        }
        List<Card> area = new List<Card>();
        if(select.whereTarget == where.enemyField)
        {
            area = enemyField;
        }
        else if(select.whereTarget == where.hand){
            area = selfHand;
        }
        else if (select.whereTarget == where.selfField)
        {
            area = selfField;
        }
        if(selectNum >= select.numOfSelect || selectNum >= area.Count)
        {
            selectNum = 0;
            int[] finalTargets = selectIndexList.ToArray();
            selectList.Clear();
            selectIndexList.Clear();      
            SelectCard.SetActive(false);
            battleManager.PlayActionSelectRpc(isCostAdd,selectedPlayIndex,finalTargets,select.whereTarget);            
        }
    }
    private void DrawScope()
    {
        if(Scope == null)
        {
            return;
        }
        Transform trs = ScopeArea.GetComponent<Transform>();
        foreach (Transform child in trs)
        {
            Destroy(child.gameObject);
        }
        Card c = Scope;
        GameObject cardObj = Instantiate(cardButtonPrefab, trs);
        TextMeshProUGUI btnText = cardObj.GetComponentInChildren<TextMeshProUGUI>();
            
        btnText.text = $"Cost:{c.Cost}\n{GetCardName(c)}"; 
        ApplyCardColor(cardObj, c);

        EventTrigger trigger = cardObj.GetComponent<EventTrigger>();
        if(trigger == null) trigger = cardObj.AddComponent<EventTrigger>();

        EventTrigger.Entry entryEnter = new EventTrigger.Entry();
        entryEnter.eventID = EventTriggerType.PointerEnter;
        entryEnter.callback.AddListener((data)=>{ShowPopUp(GetCardAbility(c));});
        trigger.triggers.Add(entryEnter);

        EventTrigger.Entry entryExit = new EventTrigger.Entry();
        entryExit.eventID = EventTriggerType.PointerExit;
        entryExit.callback.AddListener((data)=>{HidePopUp();});
        trigger.triggers.Add(entryExit);
    }
    private int[] transCardId(List<Card> cards)
    {
        int[] c = new int[cards.Count];
        for(int i = 0;i < cards.Count; i++)
        {
            c[i] = Card.GetCardId(cards[i]);
        }
        return c;   
    } 
    private List<Card> IsSelf(Select select)
    {
        if(select.whereTarget == where.selfField)return selfField;
        else return enemyField;
    }    
    
    private string GetCardName(Card c)
    {
        string className = c.GetType().Name;
        string displayName = className;
        if(cardDatabase != null)
        {
            foreach(CardSetting name in cardDatabase.cards)
            {
                if(name.className == className)
                {
                    displayName = name.displayName;
                    break;
                }
            }
        }
        return displayName;
    }
    private string GetCardAbility(Card c)
    {
        string className = c.GetType().Name;
        string ability = "なし";
        if(cardDatabase != null)
        {
            foreach(CardSetting name in cardDatabase.cards)
            {
                if(name.className == className)
                {
                    ability = name.ability;
                    break;
                }
            }
        }
        return ability;
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
