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
    public static Dictionary<string, GameManager> activeGames = new Dictionary<string, GameManager>();

    private class WaitingPlayer
    {
        public string IpAddress {get;set;}
        public NetCardData[] DeckCards {get; set;}
        public IGameHubReceiver Receiver {get;set;}
    }

    public async Task JoinMatchmakingAsync(NetCardData[] deckCards)
    {
        string actualIp = Context.CallContext.Peer;

        var me = new WaitingPlayer
        {
            IpAddress = actualIp,
            DeckCards = deckCards,
            Receiver = this.Client
        };

        WaitingPlayer player1 = null;
        WaitingPlayer player2 = null;

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

        activeGames.Add(roomName,gm);

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
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    IncludeFields = true
                };

                string jsonString =JsonSerializer.Serialize(gm.playLog.History, options);
                
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
}