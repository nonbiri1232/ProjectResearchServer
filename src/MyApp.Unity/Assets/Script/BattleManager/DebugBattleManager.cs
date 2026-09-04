using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DebugBattleManager : BattleManager
{
    [Header("UI Managers")]
    [SerializeField] private BattleUIManager uiManager;
    [SerializeField] private PlayerInputManager inputManager;
    [SerializeField] private CardConect cardDatabase;
    
    [Header("My Layouts")]
    [SerializeField] private CardLayoutManager p1HandLayout;
    [SerializeField] private CardLayoutManager p1FieldLayout;
    [SerializeField] private CardLayoutManager p1GarbageLayout;
    [SerializeField] private CardLayoutManager p1DeckLayout;
    [SerializeField] private CardLayoutManager p1MariganLayout;

    [Header("Enemy Layouts")]
    [SerializeField] private CardLayoutManager p2HandLayout;
    [SerializeField] private CardLayoutManager p2FieldLayout;
    [SerializeField] private CardLayoutManager p2GarbageLayout;
    [SerializeField] private CardLayoutManager p2DeckLayout;
    private float enemyTurnTimer = 0f;

    private void Start()
    {
        StartDebugBattle();
    }

    private void Update()
    {
        if (gm == null) return;

        // 相手のターンで入力待ち状態になったら、カウントを進める
        if (gm.turn == remotePlayer && gm.currentState == GameState.WaitingForInput)
        {
            enemyTurnTimer += Time.deltaTime;
            
            // 1秒待ってから自動的にパスする（いきなり切り替わると見栄えが悪いため）
            if (enemyTurnTimer > 1.0f)
            {
                enemyTurnTimer = 0f;
                SimulateEnemyTurn();
            }
        }
    }
    private void SimulateEnemyTurn()
    {
        Debug.Log("【Debug】相手のターンを自動スキップします。");

        // 1. 相手がドローした分のカードを画面上に生成（裏向き）
        SyncHandVisuals(remotePlayer, p2HandLayout);

        // 2. 相手が「ターン終了」を宣言したことにして、GameManagerの処理を進める
        gm.ExecuteAction(remotePlayer, localPlayer, new PlayerAction(ActionType.End));

        // 3. 自分のターンに戻り、自分が新しくドローした分のカードを画面に生成
        SyncHandVisuals(localPlayer, p1HandLayout);
        
        uiManager.UpdateUI(gm, localPlayer, remotePlayer);
        Debug.Log("【Debug】自分のターンが開始されました！");
    }

    private void SyncHandVisuals(Player targetPlayer, CardLayoutManager layout)
    {
        foreach (Card c in targetPlayer.hand)
        {
            // まだレイアウト内に生成されていないカードを探す
            CardData checkData = new CardData { uniqueId = c.uniqueId };
            
            if (layout.FindCardObject(checkData) == null)
            {
                // デッキから手札へカードを引く演出
                CardData newData = CreateCardData(c);
                CardLayoutManager deckLayout = (targetPlayer == localPlayer) ? p1DeckLayout : p2DeckLayout;
                
                GameObject deckCardObj = deckLayout.FindCardObject(newData);
                if (deckCardObj != null)
                {
                    layout.ReceiveCard(deckLayout, newData, deckCardObj);
                    
                    // 自分のカードなら能力等をセット
                    if (targetPlayer == localPlayer)
                    {
                        CardView view = deckCardObj.GetComponent<CardView>();
                        view.IsHandCard = true;
                        view.IsMyCard = true;
                        view.Setup(newData);
                    }
                }
            }
        }
    }

    private void StartDebugBattle()
    {
        List<Card> deck1 = new List<Card>();
        List<Card> deck2 = new List<Card>();
        for (int i = 0; i < 40; i++)
        {
            deck1.Add(Card.CreateCardInstance(1));
            deck2.Add(Card.CreateCardInstance(2));
        }

        localPlayer = new Player(deck1);
        remotePlayer = new Player(deck2);

        gm = new GameManager(localPlayer, remotePlayer);

        // 1. デッキのカードをすべて視覚的に生成する
        foreach (Card c in localPlayer.deck) p1DeckLayout.CreateCard(CreateCardData(c));
        foreach (Card c in localPlayer.hand) p1DeckLayout.CreateCard(CreateCardData(c)); // 引く前の手札も一旦デッキに生成
        foreach (Card c in remotePlayer.deck) p2DeckLayout.CreateCard(CreateCardData(c));
        foreach (Card c in remotePlayer.hand) p2DeckLayout.CreateCard(CreateCardData(c));

        // 2. デッキから初手を手札に引く（シームレスな移動）
        foreach (Card c in localPlayer.hand)
        {
            CardData data = CreateCardData(c);
            GameObject obj = p1DeckLayout.FindCardObject(data);
            p1HandLayout.ReceiveCard(p1DeckLayout, data, obj);
            
            CardView view = obj.GetComponent<CardView>();
            view.IsHandCard = true;
            view.IsMyCard = true;
            view.AbilityText = GetAbilityText(c);
        }

        uiManager.UpdateUI(gm, localPlayer, remotePlayer);

        // 3. マリガンフェーズの開始
        if (gm.NeedsMarigan(localPlayer))
        {
            uiManager.ShowMarigan(); // UIの背景とボタンを表示
            
            // 手札からマリガン用レイアウトへ移動
            foreach (Card c in localPlayer.hand)
            {
                CardData data = CreateCardData(c);
                GameObject obj = p1HandLayout.FindCardObject(data);
                p1MariganLayout.ReceiveCard(p1HandLayout, data, obj);
            }
            
            inputManager.StartMariganSelection(); // マウスクラスをマリガン状態へ
        }
    }

    public override void SubmitMarigan(List<CardData> selectedCardsData)
    {
        // 1. 選択されたカードをデッキにシームレスに戻す
        foreach (var data in selectedCardsData)
        {
            GameObject obj = p1MariganLayout.FindCardObject(data);
            if (obj != null) p1DeckLayout.ReceiveCard(p1MariganLayout, data, obj);
        }

        // 2. ロジック上のマリガン処理を実行
        List<Card> cardsToReturn = new List<Card>();
        foreach (var data in selectedCardsData)
        {
            Card c = localPlayer.hand.FirstOrDefault(x => x.uniqueId == data.uniqueId);
            if (c != null) cardsToReturn.Add(c);
        }
        
        bool success = gm.ExecuteAction(localPlayer, remotePlayer, new PlayerAction(ActionType.Marigan, cardsToReturn));
        
        if (success)
        {
            // 相手(AI)もマリガンしたことにしてメインフェーズへ進める
            gm.ExecuteAction(remotePlayer, localPlayer, new PlayerAction(ActionType.Marigan, new List<Card>()));

            // 3. 選択しなかったカードは手札に戻し、新しく引いたカードはデッキから手札へ移動させる
            foreach (Card c in localPlayer.hand)
            {
                CardData data = CreateCardData(c);
                GameObject existingInMarigan = p1MariganLayout.FindCardObject(data);
                
                if (existingInMarigan != null)
                {
                    // 選択しなかった（残した）カード
                    p1HandLayout.ReceiveCard(p1MariganLayout, data, existingInMarigan);
                }
                else
                {
                    // デッキから新しく引いてきたカード！
                    GameObject inDeck = p1DeckLayout.FindCardObject(data);
                    if (inDeck != null)
                    {
                        p1HandLayout.ReceiveCard(p1DeckLayout, data, inDeck);
                        CardView view = inDeck.GetComponent<CardView>();
                        view.IsHandCard = true;
                        view.IsMyCard = true;
                        view.AbilityText = GetAbilityText(c); // 新しいカードにも能力をセット
                    }
                }
            }
            
            uiManager.HideMarigan();
            uiManager.UpdateUI(gm, localPlayer, remotePlayer);
        }
    }

    // ==========================================
    // プレイ・攻撃・終了・補助メソッド
    // ==========================================
    public override void SubmitPlay(CardData sourceData, bool addCost, List<CardData> targetDatas = null)
    {
        Card source = localPlayer.hand.FirstOrDefault(c => c.uniqueId == sourceData.uniqueId);
        if (source == null) return;

        // 正規のルール判定を通す
        PlayerAction action = new PlayerAction(ActionType.Play, source);
        action.isAddCost = addCost;
        bool isSuccess = gm.ExecuteAction(localPlayer, remotePlayer, action);

        if (isSuccess)
        {
            GameObject cardObj = p1HandLayout.FindCardObject(sourceData);
            if (cardObj != null)
            {
                p1FieldLayout.ReceiveCard(p1HandLayout, sourceData, cardObj);
                CardView view = cardObj.GetComponent<CardView>();
                view.IsHandCard = false;
                view.IsFieldCard = true;
                view.Setup(CreateCardData(source));
            }
            uiManager.UpdateUI(gm, localPlayer, remotePlayer);
        }
        else
        {
            Debug.LogWarning($"【Debug】プレイ失敗（コスト不足など）: {source.GetType().Name}");
            // 失敗した場合は手札の定位置にスッと戻る
            p1HandLayout.RefreshCard();
        }
    }

    public override void SubmitAttack(CardData attackerData, CardData? targetData = null) { }
    public override void SubmitEndTurn()
    {
        PlayerAction action = new PlayerAction();
        action.type = ActionType.End;

        gm.ExecuteAction(localPlayer,remotePlayer,action);
    }
    public override void SubmitSelfGarbage(List<CardData> selectedCardsData) { }

    private string GetAbilityText(Card c)
    {
        if (cardDatabase == null) return "能力テキストなし";
        string className = c.GetType().Name;
        CardSetting setting = cardDatabase.cards.FirstOrDefault(s => s.className == className);
        return setting != null ? setting.ability : "能力テキストなし";
    }

    private CardData CreateCardData(Card c)
    {
        return new CardData {
            id = Card.GetCardId(c),
            uniqueId = c.uniqueId,
            cost = c.Cost,
            atk = c.Attack,
            hp = c.Hp,
            canAttackNow = true
        };
    }
}