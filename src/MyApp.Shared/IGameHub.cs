
using System.Threading.Tasks;
using MagicOnion;
using MessagePack;

namespace MyApp.Shared
{
    [MessagePackObject]
    public struct NetCardData
    {
        [Key(0)]public int uniqueId;
        [Key(1)]public int id;
        [Key(2)]public int type; //1.Object 2.Method 3.Scope
        [Key(3)]public int cost;
        [Key(4)]public int atk;
        [Key(5)]public int hp;
        [Key(6)]public bool canAttackNow;
        [Key(7)]public bool isProxy;
    }
    [MessagePackObject]
    public struct NetPlayerAction
    {
        [Key(0)]public int type; //ActionType
        [Key(1)]public int sourceCardUniqueId; //nullの時は-1
        [Key(2)]public int[] targetCardUniqueIds;
        [Key(3)]public bool isAddCost;
    }
    public interface IGameHub : IStreamingHub<IGameHub, IGameHubReceiver>
    {
        Task JoinMatchmakingAsync(NetCardData[] deckCards);
        Task SendActionAsync(string roomName,NetPlayerAction netAction);
    }
}