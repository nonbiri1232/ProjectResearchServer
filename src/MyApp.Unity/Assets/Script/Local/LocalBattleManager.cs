using System.Collections.Generic;
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
    }
}
public class LocalBattleManager:NetworkBehaviour
{
    [Header("描画クラスへの参照")]
    [SerializeField] private LocalBattleVisual visualManager;
    private Dictionary<ulong, List<Card>> receivedDecks = new Dictionary<ulong, List<Card>>();
    private GameManager gm;
    private Player host;
    private Player client;
    private Player first;

    public NetworkVariable<ulong> currentTurnPlayerId = new NetworkVariable<ulong>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<PhaseState> currentPhaseState = new NetworkVariable<PhaseState>(
        PhaseState.Start, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public bool IsMyTurn => currentTurnPlayerId.Value == NetworkManager.LocalClientId;
    public PhaseState CurrentPhase => currentPhaseState.Value;
    public override void OnNetworkSpawn()
    {
        isDidMariganHost = false;
        isDidMariganClient = false;
        base.OnNetworkSpawn();
        
        SubmitDeckServerRpc(DeckManager.player1Deck.ToArray());

        // ターンが変わった時のUI更新
        currentTurnPlayerId.OnValueChanged += (oldId, newId) => visualManager.UpdateUI();
        
        //フェイズが変わった時も自動で画面を更新する
        currentPhaseState.OnValueChanged += (oldState, newState) => visualManager.UpdateUI();

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
            visualManager.SetupInitialBoard(myHand, myField, enemyField, myMemory, enemyMemory, scope);
            return;
        }
        RpcSendParams sendParams = new RpcSendParams { Target = RpcTarget.Single(targetId, RpcTargetUse.Temp) };
        RpcParams rpcParams = new RpcParams { Send = sendParams };
        
        SetupBoardClientRpc(myHand, myField, enemyField, myMemory, enemyMemory, scope, rpcParams);
    }
    [Rpc(SendTo.SpecifiedInParams)]
    private void SetupBoardClientRpc(CardData[] selfHand, CardData[] selfField, CardData[] enemyField,int[] selfMemory,int[] enemyMemory,int scope,RpcParams rpcParams = default)
    {
        visualManager.SetupInitialBoard(selfHand,selfField,enemyField,selfMemory,enemyMemory,scope);
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
            data[i] = new CardData {
                id = Card.GetCardId(cards[i]),
                cost = cards[i].Cost,
                atk = cards[i].Attack,
                hp = cards[i].Hp,
                canAttackNow = CanAttackNow(cards[i])
            };
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

        if (targetIndex == -1)
        {
            // ダイレクトアタック（ターゲット無しでPlayerActionを作成）
            action = new PlayerAction(ActionType.Attack, attackerCard);
        }
        else
        {
            // 通常の攻撃（ターゲットの取得もインデックスで直接行う！）
            if (targetIndex < 0 || targetIndex >= GetEnemyPlayer().field.Count) return;
            Card targetCard = GetEnemyPlayer().field[targetIndex];
            
            action = new PlayerAction(ActionType.Attack, attackerCard, new List<Card> { targetCard });
        }
        bool isCorrect;
        isCorrect = gm.ExecuteAction(gm.turn,GetEnemyPlayer(),action);

        if (isCorrect)
        {
            Debug.Log($"攻撃処理が正常に処理されました。");
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
    private Player GetEnemyPlayer(Player pl)
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
