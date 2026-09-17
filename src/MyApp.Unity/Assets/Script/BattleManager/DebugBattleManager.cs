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
    [SerializeField] private Transform enemyAttackTarget;
    [SerializeField] private Sprite fallbackCardImage;
    private float enemyTurnTimer = 0f;

    private void Start()
    {
        StartDebugBattle();
    }

    private void Update()
    {
        if (gm == null || isPresenting) return;

        // 2ターン目以降のStartフェーズではセルフガベージの決定が必要。
        // Debug画面には選択UIがないため、未選択（0枚）で確定してMainへ進める。
        if (gm.currentPhase == PhaseState.Start && gm.systemTurn > 1 &&
            gm.currentState == GameState.WaitingForInput)
        {
            Player activePlayer = gm.turn;
            Player waitingPlayer = activePlayer == localPlayer ? remotePlayer : localPlayer;
            if (gm.ExecuteAction(activePlayer, waitingPlayer,
                new PlayerAction(ActionType.SelfGarbage, new List<Card>())))
            {
                SyncBattleVisuals();
            }
            return;
        }

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
        Debug.Log("【Debug】相手のターンです。");

        // 1. 相手がドローした分のカードを画面上に生成（裏向き）
        SyncHandVisuals(remotePlayer, p2HandLayout);
        
        // 2.手持ちのカードにプレイできるものがあればプレイさせる
        foreach(Card c in remotePlayer.hand)
        {
            if(remotePlayer.maxMemory - remotePlayer.fieldCost >= c.Cost && c.Cost <= remotePlayer.usableMemory - remotePlayer.usedMemory)
            {
                Debug.Log("カードをプレイしました。");
                gm.ExecuteAction(remotePlayer, localPlayer, new PlayerAction(ActionType.Play,c));
                SyncBattleVisuals();
                break;
            }
        }

        // 3. 相手が「ターン終了」を宣言したことにして、GameManagerの処理を進める
        gm.ExecuteAction(remotePlayer, localPlayer, new PlayerAction(ActionType.End));

        // 4. 自分のターンに戻り、自分が新しくドローした分のカードを画面に生成
        SyncHandVisuals(localPlayer, p1HandLayout);
        SyncBattleVisuals();
        
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
            // 対象を必要としない1コストObjectで、プレイと攻撃を確認できるデバッグデッキにする。
            deck1.Add(Card.CreateCardInstance(1));
            deck2.Add(Card.CreateCardInstance(1));
        }

        localPlayer = new Player(deck1);
        remotePlayer = new Player(deck2);

        gm = new GameManager(localPlayer, remotePlayer);

        // 1. デッキのカードをすべて視覚的に生成する
        p1DeckLayout.BeginBatchUpdate();
        p2DeckLayout.BeginBatchUpdate();
        foreach (Card c in localPlayer.deck){
            var image = GetCardImage(c);
            Debug.Log($"{image}");
            p1DeckLayout.CreateCard(CreateCardData(c),image);
        }
        foreach (Card c in localPlayer.hand){
            var image = GetCardImage(c);
            Debug.Log($"{image}");
            p1DeckLayout.CreateCard(CreateCardData(c),image);
        } // 引く前の手札も一旦デッキに生成
        foreach (Card c in remotePlayer.deck) {
            var image = GetCardImage(c);
            Debug.Log($"{image}");
            p2DeckLayout.CreateCard(CreateCardData(c),image);
        }
        foreach (Card c in remotePlayer.hand) {
            var image = GetCardImage(c);
            Debug.Log($"{image}");
            p2DeckLayout.CreateCard(CreateCardData(c),image);
        }
        p1DeckLayout.EndBatchUpdate();
        p2DeckLayout.EndBatchUpdate();

        // 2. デッキから初手を手札に引く（シームレスな移動）
        foreach (Card c in localPlayer.hand)
        {
            CardData data = CreateCardData(c);
            GameObject obj = p1DeckLayout.FindCardObject(data);
            p1HandLayout.ReceiveCard(p1DeckLayout, data, obj);
            
            p1HandLayout.UpdateCard(data, true, GetAbilityText(c), true, GetCardImage(c));
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
                p1MariganLayout.UpdateCard(data, true, GetAbilityText(c), true, GetCardImage(c));
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
                        p1HandLayout.UpdateCard(data, true, GetAbilityText(c), true, GetCardImage(c));
                    }
                }
            }
            
            uiManager.HideMarigan();
            SyncBattleVisuals();
        }
    }

    // ==========================================
    // プレイ・攻撃・終了・補助メソッド
    // ==========================================
    public override void SubmitPlay(CardData sourceData, bool addCost, List<CardData> targetDatas = null)
    {
        if (!CanAct()) { p1HandLayout.RefreshCard(); return; }
        Card source = localPlayer.hand.FirstOrDefault(c => c.uniqueId == sourceData.uniqueId);
        if (source == null) { p1HandLayout.RefreshCard(); return; }
        List<Card> targets = null;
        if (targetDatas != null)
        {
            var available = localPlayer.hand.Concat(localPlayer.field).Concat(remotePlayer.field);
            targets = new List<Card>();
            foreach (CardData data in targetDatas)
            {
                Card target = available.FirstOrDefault(c => c.uniqueId == data.uniqueId);
                if (target == null) { p1HandLayout.RefreshCard(); return; }
                targets.Add(target);
            }
        }
        var action = new PlayerAction(ActionType.Play, source) { isAddCost = addCost, targetCard = targets };
        if (!gm.ExecuteAction(localPlayer, remotePlayer, action))
        {
            p1HandLayout.RefreshCard();
            return;
        }
        // The manager owns the command; layouts own movement and effects.
        SyncBattleVisuals();
    }

    public override void SubmitAttack(CardData attackerData, CardData? targetData = null)
    {
        if (!CanAttackTarget(attackerData, targetData)) return;
        Card attacker = localPlayer.field.First(c => c.uniqueId == attackerData.uniqueId);
        Card target = targetData.HasValue
            ? remotePlayer.field.First(c => c.uniqueId == targetData.Value.uniqueId) : null;
        GameObject targetObj = targetData.HasValue ? p2FieldLayout.FindCardObject(targetData.Value) : null;
        Vector3 position = targetObj != null ? targetObj.transform.position :
            (enemyAttackTarget != null ? enemyAttackTarget.position : p2FieldLayout.CenterPosition);
        var action = target == null ? new PlayerAction(ActionType.Attack, attacker) :
            new PlayerAction(ActionType.Attack, attacker, new List<Card> { target });
        if (!gm.ExecuteAction(localPlayer, remotePlayer, action)) return;
        isPresenting = true;
        p1FieldLayout.PlayAttack(attackerData, position, () =>
        {
            isPresenting = false;
            SyncBattleVisuals();
        });
    }

    private void SyncBattleVisuals()
    {
        SyncPlayerVisuals(localPlayer, p1DeckLayout, p1HandLayout, p1FieldLayout, p1GarbageLayout);
        SyncPlayerVisuals(remotePlayer, p2DeckLayout, p2HandLayout, p2FieldLayout, p2GarbageLayout);
        uiManager.UpdateUI(gm, localPlayer, remotePlayer);
    }

    private void SyncPlayerVisuals(Player player, CardLayoutManager deck, CardLayoutManager hand,
        CardLayoutManager field, CardLayoutManager garbage)
    {
        var layouts = new[] { deck, hand, field, garbage };
        SyncZone(player, player.deck, deck, layouts);
        SyncZone(player, player.hand, hand, layouts);
        SyncZone(player, player.field, field, layouts);
        if (gm.currentScope != null && gm.currentScope.player == player)
            SyncZone(player, new List<Card> { gm.currentScope }, field, layouts);
        SyncZone(player, player.garbage, garbage, layouts);
    }

    private void SyncZone(Player owner, List<Card> zone, CardLayoutManager destination,
        CardLayoutManager[] layouts)
    {
        if (destination == null) return;
        foreach (Card card in zone)
        {
            CardData data = CreateCardData(card);
            if (destination.FindCardObject(data) == null)
            {
                CardLayoutManager source = layouts.FirstOrDefault(l => l != null && l.FindCardObject(data) != null);
                if (source != null) destination.ReceiveCard(source, data, source.FindCardObject(data));
                else destination.CreateCard(data);
            }
            bool canShowAbility = !destination.IsFaceDown &&
                ((owner == localPlayer && (destination == p1HandLayout || destination == p1FieldLayout)) ||
                 (owner == remotePlayer && destination == p2FieldLayout));
            destination.UpdateCard(
                data,
                owner == localPlayer,
                canShowAbility ? GetAbilityText(card) : string.Empty,
                canShowAbility,
                destination.IsFaceDown ? null : GetCardImage(card));
        }
    }

    public override void SubmitEndTurn()
    {
        if (!CanAct()) return;
        if (gm.ExecuteAction(localPlayer, remotePlayer, new PlayerAction(ActionType.End)))
            SyncBattleVisuals();
    }
    public override void SubmitSelfGarbage(List<CardData> selectedCardsData)
    {
        if (!CanAct() || gm.currentPhase != PhaseState.Start || gm.systemTurn == 1) return;

        List<Card> selectedCards = new List<Card>();
        if (selectedCardsData != null)
        {
            foreach (CardData data in selectedCardsData)
            {
                Card card = localPlayer.field.FirstOrDefault(c => c.uniqueId == data.uniqueId);
                if (card == null) return;
                selectedCards.Add(card);
            }
        }

        if (gm.ExecuteAction(localPlayer, remotePlayer,
            new PlayerAction(ActionType.SelfGarbage, selectedCards)))
        {
            SyncBattleVisuals();
        }
    }

    private string GetAbilityText(Card c)
    {
        CardSetting setting = GetCardSetting(c);
        return setting != null ? setting.ability : "能力テキストなし";
    }

    private Sprite GetCardImage(Card c)
    {
        CardSetting setting = GetCardSetting(c);
        Sprite sprite = setting != null ? setting.cardImage : null;
        if (sprite == null)
        {
            Debug.LogWarning(setting != null
                ? $"CardImage が未設定です: {setting.className}"
                : $"CardSetting が見つかりませんでした: {c?.GetType().Name}");
        }
        return sprite != null ? sprite : fallbackCardImage;
    }

    private CardSetting GetCardSetting(Card c)
    {
        if (cardDatabase == null || c == null) return null;
        string className = c.GetType().Name;
        return cardDatabase.cards.FirstOrDefault(s => s.className == className);
    }

    private CardData CreateCardData(Card c)
    {
        return Card.PackingCard(c);
    }
}
