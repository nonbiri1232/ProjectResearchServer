using MessagePack;

namespace MyApp.Shared
{
    [MessagePackObject]
    public struct NetPlayLog
    {
        
    }

    public interface IGameHubReceiver
    {
        void OnMatchStarted(string roomName, int playerNumber);
    }
}