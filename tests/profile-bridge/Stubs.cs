namespace UnityEngine { public static class Time { public static float unscaledTime; } }
namespace Cysharp.Threading.Tasks { public readonly struct UniTask { public static UniTask CompletedTask => default; } }
namespace Tools
{
    public static class SimpleSingletonProvider<T> where T : class { public static T inst = null!; }
    public static class MonoSingletonProvider<T> where T : class { public static T inst = null!; }
}
namespace party.protocol
{
    public class GetShowPlayerC2S { public long PlayerId; }
    public class ShowPlayerStatistics { public int FightCount, WinFightCount; }
    public class ShowPlayerShortFight { public long Time; public int Rank,HeroId,Index,MapType; }
    public class ShowPlayer { public long PlayerId; public bool IsShowData,IsShowFight; public int PraiseNum; public ShowPlayerStatistics? Statistics; public List<ShowPlayerShortFight> Record = new(); }
    public class GetShowPlayerS2C { public ShowPlayer ShowData = new(); }
}
namespace GameLogic
{
    public static class BattleConfig { public static bool IsPVE(int map) => map==2; }
    public class PlayerCareer { public long Id; }
    public class AccountLogic
    {
        private PlayerCareer playerCareer = new();
        public long Id = 42;
        public long GetPlayerID() => Id;
        public PlayerCareer Current => playerCareer;
    }
    public class FriendLogic
    {
        public readonly HashSet<long> Friends=new(),Blocked=new();
        public readonly List<long> FriendRequests=new(),BlockRequests=new();
        public bool IsFriend(long id)=>Friends.Contains(id);
        public bool IsBlack(long id)=>Blocked.Contains(id);
        public void RequestFriendApplyC2S(long id)=>FriendRequests.Add(id);
        public void RequestFriendOpC2S(long id,string nick,int op,Action? done) { if(op==2) BlockRequests.Add(id); }
    }
    public class GameLogicManager { public AccountLogic account = new(); public FriendLogic friend=new(); }
}
public static class CharacterHandle { public static string GetCharacterNickName(int id) => "캐릭터"+id; public static string GetCharacterName(int id)=>"이름"+id; }
namespace UI
{
    public class AccountInfoWindow { public bool isShowing; }
    public class UIManager { public AccountInfoWindow AccountInfo = new(); }
}
namespace Core.Net
{
    public enum ProtolcalType { Background, Ahead, RealTime }
    public class GetShowPlayerS2CRPC
    {
        public delegate Cysharp.Threading.Tasks.UniTask OnGetShowPlayerS2CServerDelegate(party.protocol.GetShowPlayerS2C model, int error, bool dispatch);
        public OnGetShowPlayerS2CServerDelegate? OnGetShowPlayerS2CServerCallBackAsync;
    }
    public class RPCAsyncResult { public bool keepWaiting = true; }
    public class RPCMsgManager
    {
        internal sealed class Message { internal RPCAsyncResult callAction { get; set; } = null!; }
        private readonly List<Message> _OnPushRPCMsg = new();
        public void StartReply(RPCAsyncResult action) { _OnPushRPCMsg.Add(new Message { callAction = action }); }
        public void EndReply() { _OnPushRPCMsg.Clear(); }
        public GetShowPlayerS2CRPC GetShowPlayerS2C = new();
        public static List<(long Id, int Command, ProtolcalType Type, RPCAsyncResult Result)> Requests = new();
        public static RPCAsyncResult RPCCallStatic(party.protocol.GetShowPlayerC2S req, int cmd, ProtolcalType type)
        {
            var result = new RPCAsyncResult(); Requests.Add((req.PlayerId, cmd, type, result)); return result;
        }
    }
    public class NetManager { public bool IsConnected = true; public RPCMsgManager RPC = new(); }
}
