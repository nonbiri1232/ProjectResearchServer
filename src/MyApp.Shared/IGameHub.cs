
using System.Threading.Tasks;
using MagicOnion;
using MessagePack;

namespace MyApp.Shared
{
    [MessagePackObject]
    public struct NetCardData
    {
        [Key(0)]
        public int uniqueId;
        
        [Key(1)]
        public int id;
        
        [Key(2)]
        public int type; //1.Object 2.Method 3.Scope
        
        [Key(3)]
        public int cost;
        
        [Key(4)]
        public int atk;
        
        [Key(5)]
        public int hp;
        
        [Key(6)]
        public bool canAttackNow;
    }
    public interface IGameHub : IStreamingHub<IGameHub, IGameHubReceiver>
    {
        Task JoinMatchmakingAsync(NetCardData[] deckCards);
    }
}