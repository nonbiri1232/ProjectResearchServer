using MessagePack;

namespace MyApp.Shared
{
    [MessagePackObject]
    public struct NetBoardData
    {
        [Key(0)] public NetCardData[] selfField;
        [Key(1)] public NetCardData[] selfHand;
        [Key(2)] public NetCardData[] selfGarbage;
        [Key(3)] public NetCardData[] enemyField;
        [Key(4)] public NetCardData[] enemyGarbage;
        
    }

    [MessagePackObject]
    public struct NetPlayLog
    {
        [Key(0)] public int turnNumber;
        [Key(1)] public bool isPlayer1;
        [Key(2)] public int type;
        [Key(3)] public NetCardData? sourceCard;
        [Key(4)] public NetCardData[] targetCards;
        [Key(5)] public int actionValue;
    }

    public interface IGameHubReceiver
    {
        void OnMatchStarted(string roomName, int playerNumber);
        void OnBoardUpdated(NetBoardData boardData,NetPlayLog playLog);
    }
}