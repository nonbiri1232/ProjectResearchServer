using MagicOnion.Server.Hubs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyApp.Shared;
using System.Text.Json;

public class GameHub : StreamingHubBase<IGameHub, IGameHubReceiver> , IGameHub
{
    private static Queue<WaitingPlayer> waitingQueue = new Queue<WaitingPlayer>();
    private static object lockObject = new object();
    public static Dictionary<string, MatchRoom> activeGames = new Dictionary<string, MatchRoom>();

    private class WaitingPlayer
    {
        public Guid ConnectionId { get; set; }
        public string IpAddress {get;set;}
        public NetCardData[] DeckCards {get; set;}
        public IGameHubReceiver Receiver {get;set;}
    }

    public class MatchRoom
    {
        public GameManager GM { get; set; }
        public Player Player1 { get; set; }
        public Player Player2 { get; set; }
        public Guid Player1ConnectionId { get; set; } // 1Pの通信ID
        public Guid Player2ConnectionId { get; set; } // 2Pの通信ID
    }
    public async Task JoinMatchmakingAsync(NetCardData[] deckCards)
    {
        string actualIp = Context.CallContext.Peer;

        var me = new WaitingPlayer
        {
            ConnectionId = Context.ContextId,
            IpAddress = actualIp,
            DeckCards = deckCards,
            Receiver = this.Client
        };

        WaitingPlayer? player1 = null;
        WaitingPlayer? player2 = null;

        lock (lockObject)
        {
            waitingQueue.Enqueue(me);

            if(waitingQueue.Count >= 2)
            {
                player1 = waitingQueue.Dequeue();
                player2 = waitingQueue.Dequeue();
            }
        }

        if(player1 != null && player2 != null)
        {
            StartGameProcess(player1,player2);
        }
    }

    private void StartGameProcess(WaitingPlayer p1,WaitingPlayer p2)
    {
        string roomName = "Room_" + Guid.NewGuid().ToString();

        List<Card> deck1 = Card.CreateCardInstance(new List<CardData>(TransCardData(p1.DeckCards)));
        List<Card> deck2 = Card.CreateCardInstance(new List<CardData>(TransCardData(p2.DeckCards)));

        var player1 = new Player(deck1);
        var player2 = new Player(deck2);

        var gm = new GameManager(player1,player2);

        var match = new MatchRoom()
        {
            GM = gm,
            Player1 = player1,
            Player2 = player2,
            Player1ConnectionId = p1.ConnectionId,
            Player2ConnectionId = p2.ConnectionId
        };
        activeGames.Add(roomName,match);

        gm.playLog.OnLogAdded += (logEntry) =>
        {
            NetPlayLog netLog = TransToNetPlayLog(logEntry);

            BoardData p1Board = gm.GetBoardData(player1);
            p1.Receiver.OnBoardUpdated(TransToNetBoard(p1Board), netLog);

            BoardData p2Board = gm.GetBoardData(player2);
            p2.Receiver.OnBoardUpdated(TransToNetBoard(p2Board), netLog);
        };

        gm.OnGameFinished += (winner) =>
        {
            string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"MatchLLogs");
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"Log_{roomName}_{timestamp}.json";
            string filePath = Path.Combine(logDirectory,fileName);

            try
            {
                int winnerNumber = (winner == player1) ? 1 : (winner == player2) ? 2 : 0;

                var outputData = new
                {
                    MatchDate = DateTime.Now.ToString("yyyyMMdd_Hh:mm:ss"),
                    WinnerPlayer = winnerNumber,
                    PlayHistory = gm.playLog.History
                };

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    IncludeFields = true
                };

                string jsonString =JsonSerializer.Serialize(outputData, options);
                
                File.WriteAllText(filePath, jsonString);

                Console.WriteLine($"[ログ保存完了]{filePath}");
            }
            catch(Exception e)
            {
                Console.WriteLine($"[ログ保存エラー]{e.Message}");
            }
            activeGames.Remove(roomName);
        };
        
        Console.WriteLine($"試合開始: {roomName} (P1: {p1.IpAddress} vs P2: {p2.IpAddress})");

        p1.Receiver.OnMatchStarted(roomName, 1);
        p2.Receiver.OnMatchStarted(roomName, 2);
    }
    public async Task SendActionAsync(string roomName, NetPlayerAction netAction)
    {
        if(!activeGames.TryGetValue(roomName, out MatchRoom match))
        {
            return;
        }
        var gm = match.GM;
        Guid senderId = Context.ContextId;

        bool isP1Turn = (gm.turn == match.Player1);
        
        bool isMariganAction = (netAction.type == (int)ActionType.Marigan);

        if(!isMariganAction){    
            if (isP1Turn && senderId != match.Player1ConnectionId)
            {
                Console.WriteLine($"2Pが1Pのターンに行動を送信しました。");
                return;
            }
            else if (!isP1Turn && senderId != match.Player2ConnectionId)
            {
                Console.WriteLine($"1Pが2Pのターンに行動を送信しました。");
                return;
            }
        }
        Player movePlayer = gm.turn;
        Player waitPlayer = gm.notrun;

        PlayerAction action = new PlayerAction();
        action.type = (ActionType)netAction.type;
        action.isAddCost = netAction.isAddCost;

        if (netAction.sourceCardUniqueId >= 0)
        {
            action.sourceCard = gm.FindCardByUniqueId(netAction.sourceCardUniqueId);
        }

        if (netAction.targetCardUniqueIds != null && netAction.targetCardUniqueIds.Length > 0)
        {
            action.targetCard = new List<Card>();
            foreach (int id in netAction.targetCardUniqueIds)
            {
                Card target = gm.FindCardByUniqueId(id);
                if (target != null)
                {
                    action.targetCard.Add(target);
                }
            }
        }
        bool isSuccess = gm.ExecuteAction(movePlayer, waitPlayer, action);

        if (!isSuccess)
        {
            Console.WriteLine($"[アクション失敗] 部屋:{roomName}, Type:{action.type}");
        }
    }

    protected override ValueTask OnDisconnected()
    {
        Guid disconnectedId = Context.ContextId;

        string targetRoomName = null;
        MatchRoom targetRoom = null;
        int disconnectedPlayerNum = 0;

        foreach (var kvp in activeGames)
        {
            if (kvp.Value.Player1ConnectionId == disconnectedId)
            {
                targetRoomName = kvp.Key;
                targetRoom = kvp.Value;
                disconnectedPlayerNum = 1;
                break;
            }
            else if (kvp.Value.Player2ConnectionId == disconnectedId)
            {
                targetRoomName = kvp.Key;
                targetRoom = kvp.Value;
                disconnectedPlayerNum = 2;
                break;
            }
        }

        if (targetRoom != null)
        {
            Console.WriteLine($"[切断検知] {targetRoomName} の {disconnectedPlayerNum}P が切断しました。");
            
            Player disconnectedPlayer = (disconnectedPlayerNum == 1) ? targetRoom.Player1 : targetRoom.Player2;
            targetRoom.GM.Surrender(disconnectedPlayer);
        }

        return base.OnDisconnected();
    }

    //補助用メソッド
    private CardData[] TransCardData(NetCardData[] cardDatas)
    {
        List<CardData> cards = new List<CardData>();
        foreach(var c in cardDatas)
        {
            var card = new CardData();
            card.uniqueId = c.uniqueId;
            card.id = c.id;
            card.cost = c.cost;
            card.atk = c.atk;
            card.hp = c.hp;
            card.type = c.type;
            card.canAttackNow = c.canAttackNow;
            card.isProxy = c.isProxy;
            cards.Add(card);
        }
        return cards.ToArray();
    }
    private NetCardData[] TransCardData(CardData[] cardDatas)
    {
        List<NetCardData> cards = new List<NetCardData>();
        foreach(var c in cardDatas)
        {
            var card = new NetCardData();
            card.uniqueId = c.uniqueId;
            card.id = c.id;
            card.cost = c.cost;
            card.atk = c.atk;
            card.hp = c.hp;
            card.type = c.type;
            card.canAttackNow = c.canAttackNow;
            card.isProxy = c.isProxy;
            cards.Add(card);
        }
        return cards.ToArray();
    }
    private NetPlayLog TransToNetPlayLog(PlayLogEntry log)
    {
        return new NetPlayLog
        {
            turnNumber = log.turnNumber,
            isPlayer1 = log.isPlayer1,
            type = (int)log.type,
            sourceCard = log.sourceCard.HasValue ? TransToNetCardData(log.sourceCard.Value) : (NetCardData?)null,
            targetCards = TransToNetCardDataArray(log.targetCards),
            actionValue = log.actionValue
        };
    }

    private NetBoardData TransToNetBoard(BoardData board)
    {
        return new NetBoardData
        {
            selfField = TransToNetCardDataArray(board.selfField),
            selfHand = TransToNetCardDataArray(board.selfHand),
            selfGarbage = TransToNetCardDataArray(board.selfGarbage),
            enemyField = TransToNetCardDataArray(board.enemyField),
            enemyGarbage = TransToNetCardDataArray(board.enemyGarbage)
        };
    }

    private NetCardData TransToNetCardData(CardData card)
    {
        return new NetCardData
        {
            uniqueId = card.uniqueId,
            id = card.id,
            type = card.type,
            cost = card.cost,
            atk = card.atk,
            hp = card.hp,
            canAttackNow = card.canAttackNow,
            isProxy = card.isProxy
        };
    }

    private NetCardData[] TransToNetCardDataArray(List<CardData> cards)
    {
        if (cards == null) return new NetCardData[0];
        
        NetCardData[] netCards = new NetCardData[cards.Count];
        for (int i = 0; i < cards.Count; i++)
        {
            netCards[i] = TransToNetCardData(cards[i]);
        }
        return netCards;
    }

    private NetCardData[] TransToNetCardDataArray(CardData[] cards)
    {
        if (cards == null) return new NetCardData[0];
        
        NetCardData[] netCards = new NetCardData[cards.Length];
        for (int i = 0; i < cards.Length; i++)
        {
            netCards[i] = TransToNetCardData(cards[i]);
        }
        return netCards;
    }
}