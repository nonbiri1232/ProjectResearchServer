using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public struct CardData : INetworkSerializable
{
    public int uniqueId;
    public int id;
    public int type; //1.Object 2.Method 3.Scope
    public int cost;
    public int atk;
    public int hp;
    public bool canAttackNow;
    public bool isProxy;

    // 通信で送るためのパッキング処理
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref uniqueId);
        serializer.SerializeValue(ref id);
        serializer.SerializeValue(ref type);
        serializer.SerializeValue(ref cost);
        serializer.SerializeValue(ref atk);
        serializer.SerializeValue(ref hp);
        serializer.SerializeValue(ref canAttackNow);
        serializer.SerializeValue(ref isProxy);
    }
}
public class LocalBattleManager : BattleManager
{
    [Header("描画クラスへの参照")]
    [SerializeField] private LocalBattleVisual visualManager;

    [Header("新描画システム（段階移行用）")]
    [SerializeField] private PlayerInputManager inputManager;
    [SerializeField] private CardLayoutManager p1HandLayout;
    [SerializeField] private CardLayoutManager p1FieldLayout;
    [SerializeField] private CardLayoutManager p2HandLayout;
    [SerializeField] private CardLayoutManager p2FieldLayout;
    [SerializeField] private CardConect cardDatabase;

    private Dictionary<ulong, List<Card>> receivedDecks = new Dictionary<ulong, List<Card>>();
    private Player host;
    private Player client;
    private Player first;
    private ulong remoteClientId = ulong.MaxValue;
    private CardData[] cachedSelfHand = new CardData[0];
    private CardData[] cachedSelfField = new CardData[0];
    private CardData[] cachedEnemyField = new CardData[0];

    public NetworkVariable<ulong> currentTurnPlayerId = new NetworkVariable<ulong>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<PhaseState> currentPhaseState = new NetworkVariable<PhaseState>(
        PhaseState.Start, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public bool IsMyTurn => IsSpawned && NetworkManager != null &&
                            currentTurnPlayerId.Value == NetworkManager.LocalClientId;
    public override PhaseState CurrentPhase => currentPhaseState.Value;

    private void Awake()
    {
        WireNewBattleView();
    }

    private void WireNewBattleView()
    {
        if (cardDatabase == null && visualManager != null)
            cardDatabase = visualManager.cardDatabase;

        if (inputManager == null)
            inputManager = FindAnyObjectByType<PlayerInputManager>();
        if (inputManager == null) return;

        inputManager.battleManager = this;
        p1HandLayout = p1HandLayout != null ? p1HandLayout : inputManager.p1HandLayout;
        p1FieldLayout = p1FieldLayout != null ? p1FieldLayout : inputManager.p1FieldLayout;
        p2FieldLayout = p2FieldLayout != null ? p2FieldLayout : inputManager.p2FieldLayout;
        inputManager.p1HandLayout = p1HandLayout;
        inputManager.p1FieldLayout = p1FieldLayout;
        inputManager.p2FieldLayout = p2FieldLayout;

        if (cardDatabase == null && inputManager.uiManager != null)
            cardDatabase = inputManager.uiManager.cardDatabase;
    }

    public override void OnNetworkSpawn()
    {
        isDidMariganHost = false;
        isDidMariganClient = false;
        base.OnNetworkSpawn();
        
        SubmitDeckServerRpc(DeckManager.player1Deck.ToArray());

        // ターンが変わった時のUI更新
        currentTurnPlayerId.OnValueChanged += (oldId, newId) =>
        {
            if (visualManager != null) visualManager.UpdateUI();
        };
        
        //フェイズが変わった時も自動で画面を更新する
        currentPhaseState.OnValueChanged += (oldState, newState) =>
        {
            if (visualManager != null) visualManager.UpdateUI();
        };

        //通信状況を監視
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
    }
    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
        }
        base.OnNetworkDespawn();
    }
    private void OnClientDisconnect(ulong disconnectedClientId)
    {
        Debug.Log($"プレイヤー {disconnectedClientId} との通信が切断されました。");

        // もしすでに「HPが0になってゲームが正常終了」している後なら、何もしない
        if (gm != null && gm.currentState == GameState.Finished) return;

        if (IsServer)
        {
            // 【自分がホストの場合】
            if (disconnectedClientId != NetworkManager.ServerClientId)
            {
                Debug.Log("対戦相手（クライアント）が切断しました。あなたの不戦勝です！");
                
                // 通信が切れているのでRPCは使わず、直接自分の画面に勝利を出す
                if (visualManager != null) visualManager.EndGame(true); 
                
                // 自分も通信を綺麗に閉じておく
                NetworkManager.Singleton.Shutdown(); 
            }
        }
        else
        {
            // 【自分がクライアントの場合】
            // ホスト（サーバー）が落ちると、クライアントは強制的にここが呼ばれます
            Debug.Log("対戦相手（ホスト）が切断しました。あなたの不戦勝です！");
            
            if (visualManager != null) visualManager.EndGame(true);
            
            NetworkManager.Singleton.Shutdown();
        }
    }

    private void Update()
    {
        if (!IsServer || gm == null) return;

        // ホスト（サーバー）だけがターンを監視して同期変数に書き込む
        ulong turnId = (gm.turn == host) ? NetworkManager.ServerClientId : GetClientId();
        
        if (currentTurnPlayerId.Value != turnId)
        {
            currentTurnPlayerId.Value = turnId;
        }
        if (currentPhaseState.Value != gm.currentPhase)
        {
            currentPhaseState.Value = gm.currentPhase;
        }
    }

    public override bool CanAct()
    {
        return !isPresenting && IsMyTurn;
    }

    public override int RequiresTargetCount(CardData cardData)
    {
        Card card = Card.CreateCardInstance(cardData);
        return card != null && card.select != null && card.select.isSelectConstructor
            ? card.select.numOfSelect : 0;
    }

    public override bool TryGetPlayTargets(CardData cardData, out where targetArea,
        out List<int> targetUniqueIds)
    {
        targetArea = where.None;
        targetUniqueIds = new List<int>();
        Card source = Card.CreateCardInstance(cardData);
        if (source == null || source.select == null || !source.select.isSelectConstructor)
            return false;

        targetArea = source.select.whereTarget;
        CardData[] candidates;
        switch (targetArea)
        {
            case where.hand: candidates = cachedSelfHand; break;
            case where.selfField: candidates = cachedSelfField; break;
            case where.enemyField: candidates = cachedEnemyField; break;
            default: return false;
        }

        targetUniqueIds = candidates
            .Where(c => c.uniqueId != cardData.uniqueId)
            .Select(c => c.uniqueId)
            .Distinct()
            .ToList();
        return targetUniqueIds.Count >= source.select.numOfSelect;
    }

    public override bool CanAttackTarget(CardData attackerData, CardData? targetData = null)
    {
        CardData? attacker = cachedSelfField
            .Where(c => c.uniqueId == attackerData.uniqueId)
            .Cast<CardData?>()
            .FirstOrDefault();
        if (!CanAct() || CurrentPhase != PhaseState.Main || !attacker.HasValue ||
            !attacker.Value.canAttackNow)
            return false;

        CardData[] objects = cachedEnemyField.Where(c => c.type == 1).ToArray();
        if (!targetData.HasValue) return objects.Length == 0;
        CardData? target = objects.Where(c => c.uniqueId == targetData.Value.uniqueId)
            .Cast<CardData?>().FirstOrDefault();
        return target.HasValue && (!objects.Any(c => c.isProxy) || target.Value.isProxy);
    }

    public override void SubmitPlay(CardData sourceData, bool addCost,
        List<CardData> targetDatas = null)
    {
        if (!CanAct()) return;
        int[] targetIds = targetDatas == null
            ? new int[0] : targetDatas.Select(c => c.uniqueId).ToArray();
        SubmitPlayByIdServerRpc(sourceData.uniqueId, addCost, targetIds);
    }

    public override void SubmitAttack(CardData attackerData, CardData? targetData = null)
    {
        if (!CanAttackTarget(attackerData, targetData)) return;
        SubmitAttackByIdServerRpc(attackerData.uniqueId,
            targetData.HasValue ? targetData.Value.uniqueId : -1);
    }

    public override void SubmitEndTurn()
    {
        if (CanAct()) TurnEndRpc();
    }

    public override void SubmitMarigan(List<CardData> selectedCardsData)
    {
        int[] selectedCardTypeIds = selectedCardsData == null
            ? new int[0] : selectedCardsData.Select(c => c.id).ToArray();
        DecideMariganRpc(selectedCardTypeIds);
    }

    public override void SubmitSelfGarbage(List<CardData> selectedCardsData)
    {
        if (!CanAct()) return;
        int[] selectedIds = selectedCardsData == null
            ? new int[0] : selectedCardsData.Select(c => c.uniqueId).ToArray();
        SubmitSelfGarbageByIdServerRpc(selectedIds);
    }
    public void PackageData(Player pl)
    {
        CardData[] selfHand = transCardData(pl.hand);
        CardData[] selfField = transCardData(pl.field);
        int[] selfMemory = new int[]{
            pl.hand.Count,
            pl.garbage.Count,
            pl.maxMemory,
            pl.fieldCost,
            pl.usableMemory,
            pl.usedMemory,
            pl.deck.Count};
        CardData[] enemyField = transCardData(GetEnemyPlayer(pl).field);
        int[] enemyMemory = new int[]{
            GetEnemyPlayer(pl).hand.Count,
            GetEnemyPlayer(pl).garbage.Count,
            GetEnemyPlayer(pl).maxMemory,
            GetEnemyPlayer(pl).fieldCost,
            GetEnemyPlayer(pl).usableMemory,
            GetEnemyPlayer(pl).usedMemory,
            GetEnemyPlayer(pl).deck.Count
        };
        int scope = -1;
        if (gm.currentScope != null)
        {
            scope = Card.GetCardId(gm.currentScope);
        }
        ulong target;
        if (pl == host)
        {
            target = NetworkManager.ServerClientId;
        }
        else
        {
            target = GetClientId();
        }
        CardData[] hostHand = transCardData(host.hand);

        if (IsServer) 
        {
            Debug.Log($"[調査3] 画面に描画されるホストの手札ID配列: {string.Join(", ", hostHand)}");
        }
        SendBoardDataToClient(target,selfHand,selfField,enemyField,selfMemory,enemyMemory,scope);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SubmitDeckServerRpc(int[] deckData ,RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[調査2] プレイヤー {senderId} から受信したデッキ: {string.Join(", ", deckData)}");
        List<Card> deck = Player.ChangeCard(deckData);

        receivedDecks[senderId] = deck;
        Debug.Log($"プレイヤー {senderId} のデッキを受信しました！ (現在の受信数: {receivedDecks.Count} / 2)");

        if (receivedDecks.Count >= 2)
        {
            Debug.Log("【通信ログ】両プレイヤーのデッキが揃いました！バトルの準備を開始します！");
            
            GameStart();
        }
    }
    //ゲーム開始用
    private void GameStart()
    {
        ulong hostId = NetworkManager.ServerClientId;
        ulong clientId = 0;
        foreach (ulong id in receivedDecks.Keys)
        {
            if (id != hostId)
            {
                clientId = id;
                break;
            }
        }
        List<Card> hostDeck = receivedDecks[hostId];
        Debug.Log($"ホストのデッキ枚数{hostDeck.Count}");
        List<Card> clientDeck = receivedDecks[clientId];
        Debug.Log($"クライアントのデッキ枚数{clientDeck.Count}");
        host = new Player(hostDeck);
        client = new Player(clientDeck);
        remoteClientId = clientId;
        localPlayer = host;
        remotePlayer = client;

        host.OnFailSafeTriggered += (card) => NotifyFailSafe(Card.GetCardId(card));
        client.OnFailSafeTriggered += (card) => NotifyFailSafe(Card.GetCardId(card));

        first = SelectFirstPlayer();
        Debug.Log("ゲームを開始します");
        gm = new GameManager(first,GetEnemyPlayer(first));
        
        gm.OnGameFinished += (winner) => GameEnd(winner);
        
        PackageData(host);
        PackageData(client);
    }
    //ゲーム終了
    private void GameEnd(Player winner)
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }
        if(winner == host)
        {
            EndClientRpc(1);
        }
        else if(winner == client)
        {
            EndClientRpc(2);
        }
        else
        {
            EndClientRpc(0);
        }
    }
    [ClientRpc]
    private void EndClientRpc(int winner)
    {
        if (visualManager == null) return;
        if (IsServer)
        {
            if(winner == 1)
            {            
                visualManager.EndGame(true);
            }
            else if(winner == 2)
            {
                visualManager.EndGame(false);
            }
            else
            {
                visualManager.DrawGame();
            }
        }
        else
        {
            if(winner == 2)
            {            
                visualManager.EndGame(true);
            }
            else if(winner == 1)
            {
                visualManager.EndGame(false);
            }
            else
            {
                visualManager.DrawGame();
            }
        }
    }

    private void NotifyFailSafe(int cardId)
    {
        PackageData(host);
        PackageData(client);
        NotifyFailSafeClientRpc(cardId);
    }
    [ClientRpc]
    private void NotifyFailSafeClientRpc(int cardId)
    {
        // クライアント側で、指定されたIDからカード名を復元してポップアップを出す
        string cardName = Card.GetCardClassName(cardId);
        Debug.Log($"【画面演出】フェイルセーフ発動！: {cardName}");


    }
    private void SendBoardDataToClient(ulong targetId, CardData[] myHand, CardData[] myField, CardData[] enemyField, int[] myMemory, int[] enemyMemory, int scope)
    {
        if (targetId == NetworkManager.ServerClientId && IsServer)
        {
            ReceiveBoardData(myHand, myField, enemyField, myMemory, enemyMemory, scope);
            return;
        }
        RpcSendParams sendParams = new RpcSendParams { Target = RpcTarget.Single(targetId, RpcTargetUse.Temp) };
        RpcParams rpcParams = new RpcParams { Send = sendParams };
        
        SetupBoardClientRpc(myHand, myField, enemyField, myMemory, enemyMemory, scope, rpcParams);
    }
    [Rpc(SendTo.SpecifiedInParams)]
    private void SetupBoardClientRpc(CardData[] selfHand, CardData[] selfField, CardData[] enemyField,int[] selfMemory,int[] enemyMemory,int scope,RpcParams rpcParams = default)
    {
        ReceiveBoardData(selfHand, selfField, enemyField, selfMemory, enemyMemory, scope);
    }

    private void ReceiveBoardData(CardData[] selfHand, CardData[] selfField,
        CardData[] enemyField, int[] selfMemory, int[] enemyMemory, int scope)
    {
        cachedSelfHand = selfHand ?? new CardData[0];
        cachedSelfField = selfField ?? new CardData[0];
        cachedEnemyField = enemyField ?? new CardData[0];
        if (visualManager != null)
            visualManager.SetupInitialBoard(selfHand, selfField, enemyField,
                selfMemory, enemyMemory, scope);
    }
    private bool CanAttackNow(Card card)
    {
        return card != null &&
               card.Type == Card.CardType.Object &&
               card.player == gm.turn &&
               gm.currentPhase == PhaseState.Main &&
               card.player.field.Contains(card) &&
               card.isCanAttack &&
               (!card.isFirstTurn || card.isImmediate) &&
               card.isAttacked < card.attackTimes;
    }

    private CardData[] transCardData(List<Card> cards)
    {
        CardData[] data = new CardData[cards.Count];
        for(int i = 0; i < cards.Count; i++)
        {
            data[i] = Card.PackingCard(cards[i]);
            data[i].canAttackNow = CanAttackNow(cards[i]);
        }
        return data;   
    }
    private Player SelectFirstPlayer()
    {
        int rnd = Random.Range(0,2);
        switch (rnd)
        {
            case 0:return host;
            case 1:return client;
        }
        return host;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SubmitPlayByIdServerRpc(int sourceUniqueId, bool addCost,
        int[] targetUniqueIds, RpcParams rpcParams = default)
    {
        Player sender = GetPlayerForClient(rpcParams.Receive.SenderClientId);
        if (sender == null || gm == null || gm.turn != sender) return;

        Card source = sender.hand.FirstOrDefault(c => c.uniqueId == sourceUniqueId);
        if (source == null) return;
        Player enemy = GetEnemyPlayer(sender);
        List<Card> targets = ResolvePlayTargets(sender, enemy, source, targetUniqueIds);
        if (targetUniqueIds != null && targetUniqueIds.Length > 0 && targets == null) return;

        PlayerAction action = new PlayerAction(ActionType.Play, source, targets)
        {
            isAddCost = addCost
        };
        if (!gm.ExecuteAction(sender, enemy, action)) return;

        CardData playedCard = Card.PackingCard(source);
        PresentPlayRpc(rpcParams.Receive.SenderClientId, playedCard);
        PackageData(host);
        PackageData(client);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SubmitAttackByIdServerRpc(int attackerUniqueId, int targetUniqueId,
        RpcParams rpcParams = default)
    {
        Player sender = GetPlayerForClient(rpcParams.Receive.SenderClientId);
        if (sender == null || gm == null || gm.turn != sender) return;
        Player enemy = GetEnemyPlayer(sender);
        Card attacker = sender.field.FirstOrDefault(c => c.uniqueId == attackerUniqueId);
        if (attacker == null) return;

        Card target = targetUniqueId >= 0
            ? enemy.field.FirstOrDefault(c => c.uniqueId == targetUniqueId) : null;
        if (targetUniqueId >= 0 && target == null) return;

        CardData attackerData = Card.PackingCard(attacker);
        CardData targetData = target != null ? Card.PackingCard(target) : default;
        PlayerAction action = target == null
            ? new PlayerAction(ActionType.Attack, attacker)
            : new PlayerAction(ActionType.Attack, attacker, new List<Card> { target });
        if (!gm.ExecuteAction(sender, enemy, action)) return;

        PresentAttackRpc(rpcParams.Receive.SenderClientId, attackerData,
            targetData, target != null);
        PackageData(host);
        PackageData(client);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SubmitSelfGarbageByIdServerRpc(int[] selectedUniqueIds,
        RpcParams rpcParams = default)
    {
        Player sender = GetPlayerForClient(rpcParams.Receive.SenderClientId);
        if (sender == null || gm == null || gm.turn != sender) return;
        List<Card> selected = new List<Card>();
        foreach (int id in selectedUniqueIds ?? new int[0])
        {
            Card card = sender.field.FirstOrDefault(c => c.uniqueId == id);
            if (card == null) return;
            selected.Add(card);
        }

        if (!gm.ExecuteAction(sender, GetEnemyPlayer(sender),
            new PlayerAction(ActionType.SelfGarbage, selected))) return;
        PackageData(host);
        PackageData(client);
    }

    private List<Card> ResolvePlayTargets(Player sender, Player enemy, Card source,
        int[] targetUniqueIds)
    {
        if (targetUniqueIds == null || targetUniqueIds.Length == 0) return null;
        List<Card> pool;
        switch (source.select.whereTarget)
        {
            case where.hand: pool = sender.hand; break;
            case where.selfField: pool = sender.field; break;
            case where.enemyField: pool = enemy.field; break;
            default: return null;
        }

        List<Card> targets = new List<Card>();
        foreach (int id in targetUniqueIds.Distinct())
        {
            Card target = pool.FirstOrDefault(c => c.uniqueId == id);
            if (target == null) return null;
            targets.Add(target);
        }
        return targets;
    }

    private Player GetPlayerForClient(ulong clientId)
    {
        if (clientId == NetworkManager.ServerClientId) return host;
        return clientId == remoteClientId ? client : null;
    }

    [Rpc(SendTo.Everyone)]
    private void PresentPlayRpc(ulong actorClientId, CardData cardData)
    {
        bool isLocalActor = actorClientId == NetworkManager.LocalClientId;
        CardLayoutManager from = isLocalActor ? p1HandLayout : p2HandLayout;
        CardLayoutManager to = isLocalActor ? p1FieldLayout : p2FieldLayout;
        if (to == null) return;

        GameObject cardObject = from != null ? from.FindCardObject(cardData) : null;
        if (cardObject != null) to.ReceiveCard(from, cardData, cardObject);
        else if (to.FindCardObject(cardData) == null)
            to.CreateCard(cardData, GetCardImage(cardData));

        to.UpdateCard(cardData, isLocalActor, GetAbilityText(cardData), true,
            GetCardImage(cardData));
    }

    [Rpc(SendTo.Everyone)]
    private void PresentAttackRpc(ulong actorClientId, CardData attackerData,
        CardData targetData, bool hasTarget)
    {
        bool isLocalActor = actorClientId == NetworkManager.LocalClientId;
        CardLayoutManager attackerLayout = isLocalActor ? p1FieldLayout : p2FieldLayout;
        CardLayoutManager targetLayout = isLocalActor ? p2FieldLayout : p1FieldLayout;
        if (attackerLayout == null) return;

        GameObject targetObject = hasTarget && targetLayout != null
            ? targetLayout.FindCardObject(targetData) : null;
        Vector3 targetPosition = targetObject != null
            ? targetObject.transform.position
            : (targetLayout != null ? targetLayout.CenterPosition : attackerLayout.CenterPosition);
        isPresenting = true;
        attackerLayout.PlayAttack(attackerData, targetPosition, () => isPresenting = false);
    }

    private string GetAbilityText(CardData data)
    {
        CardSetting setting = GetCardSetting(data);
        return setting != null ? setting.ability : string.Empty;
    }

    private Sprite GetCardImage(CardData data)
    {
        CardSetting setting = GetCardSetting(data);
        return setting != null ? setting.cardImage : null;
    }

    private CardSetting GetCardSetting(CardData data)
    {
        if (cardDatabase == null) return null;
        string className = Card.GetCardClassName(data.id);
        return cardDatabase.cards.FirstOrDefault(c => c.className == className);
    }
    //マリガン用
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void DecideMariganRpc(int[] target,RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        ulong hostId = NetworkManager.ServerClientId;
        if(senderId == hostId && !isDidMariganHost)
        {
            hostMarigan = target;
            isDidMariganHost = true;
        }
        else if(senderId != hostId && !isDidMariganClient)
        {
            clientMarigan = target;
            isDidMariganClient = true;
        }
        if(isDidMariganClient && isDidMariganHost)
            MariganAction();
    }
    int[] hostMarigan;
    int[] clientMarigan;
    bool isDidMariganHost;
    bool isDidMariganClient;
    private void MariganAction()
    {
        PlayerAction hostAction = new PlayerAction(ActionType.Marigan,Player.SearchCard(hostMarigan,host.hand));
        PlayerAction clientAction = new PlayerAction(ActionType.Marigan,Player.SearchCard(clientMarigan,client.hand));
        Debug.Log($"{gm.systemTurn}が今のターン数");
        bool isCorrect1 = gm.ExecuteAction(host,client,hostAction);
        bool isCorrect2 = gm.ExecuteAction(client,host,clientAction);
        
        Debug.Log($"isCorrect1={isCorrect1} isCorrect2={isCorrect2}");
        if (isCorrect1 && isCorrect2)
        {
            PackageData(host);
            PackageData(client);
            SendEndMariganClientRpc();
            Debug.Log($"画面をアップデートします");
        }
    }
    [Rpc(SendTo.Everyone)]
    private void SendEndMariganClientRpc()
    {
        if (visualManager != null)
        {
            visualManager.EndMarigan();
        }
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void GarbageActionRpc(int[] targetIndices, RpcParams rpcParams = default){
        ulong senderId = rpcParams.Receive.SenderClientId;
        Player senderPlayer = (senderId == NetworkManager.ServerClientId) ? host : client;
        if (gm.turn != senderPlayer)
        {
            Debug.LogWarning($"不正な操作：プレイヤー {senderId} がターン外にセルフガベージをしようとしました！");
            return;
        }
        
        List<Card> targetList = new List<Card>();
        foreach(int index in targetIndices)
        {
            // 範囲外エラーを防ぐ安全チェック
            if (index >= 0 && index < gm.turn.field.Count)
            {
                targetList.Add(gm.turn.field[index]);
            }
        }
        var action = new PlayerAction(ActionType.SelfGarbage,targetList);
        bool isCorrect;
        isCorrect = gm.ExecuteAction(gm.turn,GetEnemyPlayer(),action);
        if (isCorrect)
        {
            Debug.Log($"セルフガベージが実行されました。");
            PackageData(host);
            PackageData(client);
        }
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void PlayActionRpc(bool isAddCost,int playCardIndex, RpcParams rpcParams = default){
        ulong senderId = rpcParams.Receive.SenderClientId;
        Player senderPlayer = (senderId == NetworkManager.ServerClientId) ? host : client;
        
        if (gm.turn != senderPlayer)
        {
            Debug.LogWarning($"不正な操作：プレイヤー {senderId} がターン外にカードをプレイしようとしました！");
            return;
        }

        if (playCardIndex < 0 || playCardIndex >= gm.turn.hand.Count)
        {
            Debug.LogError("エラー：指定されたインデックスが手札の範囲外です。");
            return;
        }
        Card playCard = gm.turn.hand[playCardIndex];
        var action = new PlayerAction(ActionType.Play,playCard);
        action.isAddCost = isAddCost;
        if (isAddCost)
        {
            Debug.Log($"＋１コストでプレイします。");
        }
        bool isCorrect;
        isCorrect = gm.ExecuteAction(gm.turn,GetEnemyPlayer(),action);

        if (isCorrect)
        {
            Debug.Log($"正常にカードがプレイされました。");            
            PresentPlayRpc(senderId, Card.PackingCard(playCard));
            PackageData(host);
            PackageData(client);
        }
        else
        {
            Debug.Log($"カードプレイが何らかの要因で失敗しました。");
        }
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void PlayActionSelectRpc(bool isAddCost,int playCardIndex, int[] targetIndices, where here,RpcParams rpcParams = default){
        ulong senderId = rpcParams.Receive.SenderClientId;
        Player senderPlayer = (senderId == NetworkManager.ServerClientId) ? host : client;
        
        if (gm.turn != senderPlayer)
        {
            Debug.LogWarning($"不正な操作：プレイヤー {senderId} がターン外にカードをプレイしようとしました！");
            return;
        }

        if (playCardIndex < 0 || playCardIndex >= gm.turn.hand.Count)
        {
            Debug.LogError("エラー：指定されたインデックスが手札の範囲外です。");
            return;
        }
        Card playCard = gm.turn.hand[playCardIndex];
        
        List<Card> targetList = new List<Card>();
        if (targetIndices != null && targetIndices.Length > 0)
        {
            List<Card> searchArea = null;

            // どこからターゲットを探すか決定する
            if (here == where.hand)
            {
                searchArea = gm.turn.hand;
            }
            else if (here == where.enemyField)
            {
                searchArea = GetEnemyPlayer().field;
            }
            else if (here == where.selfField)
            {
                searchArea = gm.turn.field;
            }

            // 指定されたインデックスのカードをリストに追加
            if (searchArea != null)
            {
                foreach(int index in targetIndices)
                {
                    // 範囲外エラーを防ぐ安全チェック
                    if (index >= 0 && index < searchArea.Count)
                    {
                        targetList.Add(searchArea[index]);
                    }
                }
            }
        }

        var action = new PlayerAction(ActionType.Play,playCard, targetList);
        action.isAddCost = isAddCost;
        if (isAddCost)
        {
            Debug.Log($"＋１コストでプレイします。");
        }
        bool isCorrect;
        isCorrect = gm.ExecuteAction(gm.turn,GetEnemyPlayer(),action);

        if (isCorrect)
        {
            Debug.Log($"正常にカードがプレイされました。");            
            PresentPlayRpc(senderId, Card.PackingCard(playCard));
            PackageData(host);
            PackageData(client);
        }
        else
        {
            Debug.Log($"カードプレイが何らかの要因で失敗しました。");
        }
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void AskCanAttackRpc(int attackerFieldIndex, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        Player senderPlayer = (senderId == NetworkManager.ServerClientId) ? host : client;

        // 1. ターンチェック
        if(gm.turn != senderPlayer) return;

        // 2. 範囲チェック（エラー防止）
        if(attackerFieldIndex < 0 || attackerFieldIndex >= senderPlayer.field.Count) return;

        // 3. サーバー側にある本物のカードデータを取得
        Card realCard = senderPlayer.field[attackerFieldIndex];

        // 4. 攻撃可能かどうかの判定
        if((!realCard.isImmediate && realCard.isFirstTurn) || !realCard.isCanAttack)
        {
            Debug.Log($"サーバー判定：プレイヤー {senderId} の {realCard.GetType().Name} は攻撃できません。");
            return; // 攻撃不可ならここで処理を終了
        }

        // 5. 攻撃可能なら、質問してきたクライアント「だけ」に返事をする！
        ClientRpcParams replyParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { 
                TargetClientIds = new ulong[] { senderId }
            }
        };
        AllowAttackTargetClientRpc(attackerFieldIndex, replyParams);
    }
    [ClientRpc]
    public void AllowAttackTargetClientRpc(int attackerFieldIndex, ClientRpcParams rpcParams = default)
    {
        Debug.Log("サーバーから攻撃の許可が降りました！ターゲット選択を開きます。");
        if (visualManager != null)
            visualManager.OpenAttackSelectUI(attackerFieldIndex);
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void AttackActionRpc(int attackerIndex,int targetIndex,RpcParams rpcParams = default){
        ulong senderId = rpcParams.Receive.SenderClientId;
        Player senderPlayer = (senderId == NetworkManager.ServerClientId) ? host : client;
        
        if (gm.turn != senderPlayer)
        {
            Debug.LogWarning($"不正な操作：プレイヤー {senderId} がターン外にカードを攻撃しようとしました！");
            return;
        }

        if (attackerIndex < 0 || attackerIndex >= senderPlayer.field.Count) return;
        Card attackerCard = senderPlayer.field[attackerIndex];
        
        PlayerAction action;
        Card targetCard = null;

        if (targetIndex == -1)
        {
            // ダイレクトアタック（ターゲット無しでPlayerActionを作成）
            action = new PlayerAction(ActionType.Attack, attackerCard);
        }
        else
        {
            // 通常の攻撃（ターゲットの取得もインデックスで直接行う！）
            if (targetIndex < 0 || targetIndex >= GetEnemyPlayer().field.Count) return;
            targetCard = GetEnemyPlayer().field[targetIndex];
            
            action = new PlayerAction(ActionType.Attack, attackerCard, new List<Card> { targetCard });
        }
        CardData attackerData = Card.PackingCard(attackerCard);
        CardData targetData = targetCard != null ? Card.PackingCard(targetCard) : default;
        bool isCorrect;
        isCorrect = gm.ExecuteAction(gm.turn,GetEnemyPlayer(),action);

        if (isCorrect)
        {
            Debug.Log($"攻撃処理が正常に処理されました。");
            PresentAttackRpc(senderId, attackerData, targetData, targetCard != null);
            PackageData(host);
            PackageData(client);
            return;
        }
        Debug.Log($"何らかの要因によって攻撃処理が失敗しました。");
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void TurnEndRpc(RpcParams rpcParams = default)
    {
        Debug.Log("サーバーでTurnEndRpcが受信されました!");
        ulong senderId = rpcParams.Receive.SenderClientId;
        Player senderPlayer = (senderId == NetworkManager.ServerClientId) ? host : client;
        if (gm.turn != senderPlayer)
        {
            Debug.LogWarning($"不正な操作：プレイヤー {senderId} がターン外にターンを終了しようとしました！");
            return;
        }
        PlayerAction action = new PlayerAction();
        action.type = ActionType.End;

        bool isCorrect = gm.ExecuteAction(gm.turn, GetEnemyPlayer(), action);

        Debug.Log("ターンを終了し、データを更新します。");
        PackageData(host);
        PackageData(client);
    }    
    private Player GetEnemyPlayer()
    {
        return GetEnemyPlayer(gm.turn);
    }
    private new Player GetEnemyPlayer(Player pl)
    {
        return pl == host ? client : host;
    }
    private ulong GetClientId()
    {
        // NetworkManager.ConnectedClientsIds には、現在繋がっている全員のIDが入っています
        foreach (ulong id in NetworkManager.ConnectedClientsIds)
        {
            // もし「ホストのID（0）」じゃなければ、それがクライアントだ！
            if (id != NetworkManager.ServerClientId)
            {
                return id; 
            }
        }

        Debug.LogError("通信エラー：クライアントのIDが見つかりません！");
        return 0; // 見つからなかった時の保険
    }
}
