namespace MyApp.Shared
{
    public interface IGameHubReceiver
    {
        void OnMatchStarted(string roomName, int playerNumber);
    }
}