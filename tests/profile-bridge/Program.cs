using AstralPartySimpleStats.RuntimeBridge;
using Core.Net;
using GameLogic;
using Tools;
using UnityEngine;
using party.protocol;

static class Program
{
    private static AccountLogic Account => SimpleSingletonProvider<GameLogicManager>.inst.account;
    private static RPCMsgManager Rpc => MonoSingletonProvider<NetManager>.inst.RPC;
    private static void Assert(bool value) { if (!value) throw new Exception("Bridge regression failed"); }
    private static void Setup()
    {
        ProfileBridge.Stop();
        Time.unscaledTime = 1;
        SimpleSingletonProvider<GameLogicManager>.inst = new GameLogicManager();
        MonoSingletonProvider<NetManager>.inst = new NetManager();
        SimpleSingletonProvider<UI.UIManager>.inst = new UI.UIManager();
        RPCMsgManager.Requests.Clear();
        ProfileBridge.BeginRoom(123);
        ProfileBridge.SetParticipants("42,99,100,101");
        ProfileBridge.SetRoomMaster(42);
        Rpc.GetShowPlayerS2C.OnGetShowPlayerS2CServerCallBackAsync = (model, error, dispatch) =>
        {
            if (error == 0) Account.Current.Id = model.ShowData.PlayerId;
            return Cysharp.Threading.Tasks.UniTask.CompletedTask;
        };
    }
    private static void Reply(long id, bool visible = true, int error = 0, bool own = true)
    {
        Rpc.StartReply(own ? RPCMsgManager.Requests.Last().Result : new RPCAsyncResult());
        Rpc.GetShowPlayerS2C.OnGetShowPlayerS2CServerCallBackAsync!(new GetShowPlayerS2C
        {
            ShowData = new ShowPlayer { PlayerId = id, IsShowData = visible, PraiseNum = 656,
                Statistics = new ShowPlayerStatistics { FightCount = 230, WinFightCount = 203 } }
        }, error, false);
        Rpc.EndReply();
    }
    private static void Main()
    {
        var tests = new Action[] { OwnQuery, Throttle, ManualWindow, Isolation, PrivateStats, Failure, Reset, HistoryOrder,
            TenMinuteCache, OncePerRoom, CacheBound, ReconnectCache, AccountIsolation, LateDeparted, FriendActions, RoomActionsGuarded, RoomActionsOwnerChanges };
        foreach (var test in tests) { Setup(); test(); Console.WriteLine("PASS bridge " + test.Method.Name); }
        ProfileBridge.Stop(); Console.WriteLine(tests.Length + "/" + tests.Length + " profile bridge regressions passed (mock game API).");
    }
    private static void OwnQuery()
    {
        Assert(ProfileBridge.Read(42) == "");
        var request = RPCMsgManager.Requests.Single();
        Assert(request.Id == 0 && request.Command == 5153 && request.Type == ProtolcalType.RealTime);
        Reply(42, false); request.Result.keepWaiting = false;
        Assert(ProfileBridge.Read(42).StartsWith("42|1|203|230|656|1|"));
        Assert(RPCMsgManager.Requests.Count == 1);
        Assert(Account.Current.Id == 0); // no automatic write to the game's career
    }
    private static void Throttle()
    {
        ProfileBridge.Read(99); ProfileBridge.Read(100); Assert(RPCMsgManager.Requests.Count == 1);
        Reply(99); RPCMsgManager.Requests[0].Result.keepWaiting = false;
        ProfileBridge.Read(100); Assert(RPCMsgManager.Requests.Count == 1);
        Time.unscaledTime = 3; ProfileBridge.Read(100); Assert(RPCMsgManager.Requests.Count == 2);
        Assert(RPCMsgManager.Requests[1].Id == 100);
    }
    private static void ManualWindow()
    {
        SimpleSingletonProvider<UI.UIManager>.inst.AccountInfo.isShowing = true;
        ProfileBridge.Read(99); Assert(RPCMsgManager.Requests.Count == 0);
        SimpleSingletonProvider<UI.UIManager>.inst.AccountInfo.isShowing = false;
        ProfileBridge.Read(99); Assert(RPCMsgManager.Requests.Count == 1);
    }
    private static void Isolation()
    {
        var previouslyOpen = Account.Current; previouslyOpen.Id = 123;
        ProfileBridge.Read(99);
        Reply(99);
        Assert(previouslyOpen.Id == 123);
        Reply(99, own: false); var manuallyOpened = Account.Current;
        Assert(manuallyOpened.Id == 99); // manual request to the SAME player forwards
        Reply(101, own: false);
        Reply(99);
        Assert(manuallyOpened.Id == 101 && ReferenceEquals(manuallyOpened, Account.Current));
    }
    private static void PrivateStats()
    {
        ProfileBridge.Read(99); Reply(99, false); RPCMsgManager.Requests[0].Result.keepWaiting = false;
        Assert(ProfileBridge.Read(99).StartsWith("99|0|?|?|656|0|"));
    }
    private static void Failure()
    {
        var previous = Account.Current;
        ProfileBridge.Read(99); Reply(99, true, 9); RPCMsgManager.Requests[0].Result.keepWaiting = false;
        Time.unscaledTime = 5; Assert(ProfileBridge.Read(99) == "");
        Assert(ReferenceEquals(previous, Account.Current) && RPCMsgManager.Requests.Count == 1);
    }
    private static void HistoryOrder()
    {
        ProfileBridge.Read(99);
        Rpc.StartReply(RPCMsgManager.Requests.Last().Result);
        var data=new ShowPlayer {PlayerId=99,IsShowFight=true};
        for(var i=0;i<12;i++) data.Record.Add(new ShowPlayerShortFight {Time=i,Index=i,MapType=i%2==0?2:1,HeroId=i,Rank=i%3==0?2:1});
        Rpc.GetShowPlayerS2C.OnGetShowPlayerS2CServerCallBackAsync!(new GetShowPlayerS2C {ShowData=data},0,false);
        Rpc.EndReply(); RPCMsgManager.Requests.Last().Result.keepWaiting=false;
        var parts=ProfileBridge.Read(99).Split('|');
        var rows=System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(parts[6])).Split('\n');
        Assert(parts[5]=="1" && rows.Length==10 && rows[0]=="PvP\t승\t캐릭터11\t이름11" && rows[9]=="PvE\t승\t캐릭터2\t이름2");
        Assert(rows[2].StartsWith("PvP\t2위\t"));
    }
    private static void TenMinuteCache()
    {
        ProfileBridge.Read(99); Reply(99); RPCMsgManager.Requests.Last().Result.keepWaiting=false;
        Time.unscaledTime=600;
        Assert(ProfileBridge.Read(99)!="" && RPCMsgManager.Requests.Count==1);
        Time.unscaledTime=601;
        Assert(ProfileBridge.Read(99)=="" && RPCMsgManager.Requests.Count==2);
    }
    private static void OncePerRoom()
    {
        ProfileBridge.Read(99); Reply(99,true,9); RPCMsgManager.Requests.Last().Result.keepWaiting=false;
        Time.unscaledTime=1000;
        ProfileBridge.Read(99); Assert(RPCMsgManager.Requests.Count==1);
        ProfileBridge.BeginRoom(124);
        ProfileBridge.SetParticipants("99");
        ProfileBridge.Read(99); Assert(RPCMsgManager.Requests.Count==2);
    }
    private static void CacheBound()
    {
        ProfileBridge.SetParticipants("99,100,101,102");
        for(var i=0;i<4;i++)
        {
            Time.unscaledTime=1+i*2; ProfileBridge.Read(99+i); Reply(99+i);
            RPCMsgManager.Requests.Last().Result.keepWaiting=false;
        }
        var cache=(System.Collections.IDictionary)typeof(ProfileBridge).GetField("Cache",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.GetValue(null)!;
        Assert(cache.Count==4);
        ProfileBridge.SetParticipants("99"); Assert(cache.Count==1);
        Assert(ProfileBridge.Read(100)=="" && RPCMsgManager.Requests.Count==4);
        ProfileBridge.BeginRoom(0); Assert(cache.Count==0);
        ProfileBridge.BeginRoom(124); ProfileBridge.SetParticipants("99"); Time.unscaledTime=20;
        Assert(ProfileBridge.Read(99)=="" && RPCMsgManager.Requests.Count==5);
    }
    private static void ReconnectCache()
    {
        ProfileBridge.Read(99); Reply(99); RPCMsgManager.Requests.Last().Result.keepWaiting=false;
        var replacement=new NetManager();
        replacement.RPC.GetShowPlayerS2C.OnGetShowPlayerS2CServerCallBackAsync=(m,e,d)=>Cysharp.Threading.Tasks.UniTask.CompletedTask;
        MonoSingletonProvider<NetManager>.inst=replacement;
        Time.unscaledTime=300;
        Assert(ProfileBridge.Read(99)!="" && RPCMsgManager.Requests.Count==1);
    }
    private static void AccountIsolation()
    {
        ProfileBridge.Read(99); Reply(99); RPCMsgManager.Requests.Last().Result.keepWaiting=false;
        Account.Id=43; Time.unscaledTime=3;
        Assert(ProfileBridge.Read(99)=="" && RPCMsgManager.Requests.Count==2);
    }
    private static void LateDeparted()
    {
        ProfileBridge.Read(99); ProfileBridge.SetParticipants("100");
        Reply(99); RPCMsgManager.Requests.Last().Result.keepWaiting=false;
        var cache=(System.Collections.IDictionary)typeof(ProfileBridge).GetField("Cache",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.GetValue(null)!;
        Assert(cache.Count==0 && Account.Current.Id==0 && ProfileBridge.Read(99)=="");
    }
    private static void FriendActions()
    {
        var friend=SimpleSingletonProvider<GameLogicManager>.inst.friend;
        Assert(ProfileBridge.ActionState(42)=="9" && ProfileBridge.ActionState(99)=="8");
        ProfileBridge.RequestAction("friend|42"); ProfileBridge.RequestAction("block|9999");
        Assert(friend.FriendRequests.Count==0 && friend.BlockRequests.Count==0);
        ProfileBridge.RequestAction("friend|99"); ProfileBridge.RequestAction("friend|99");
        Assert(friend.FriendRequests.SequenceEqual(new long[]{99}));
        Time.unscaledTime=3; friend.Friends.Add(99);
        Assert(ProfileBridge.ActionState(99)=="10");
        ProfileBridge.RequestAction("friend|99"); Assert(friend.FriendRequests.Count==1);
        ProfileBridge.RequestAction("block|100"); Assert(friend.BlockRequests.SequenceEqual(new long[]{100}));
        Time.unscaledTime=5; friend.Blocked.Add(100);
        Assert(ProfileBridge.ActionState(100)=="12");
        ProfileBridge.RequestAction("block|100"); Assert(friend.BlockRequests.Count==1);
        ProfileBridge.SetParticipants("99");
        ProfileBridge.RequestAction("block|100"); Assert(friend.BlockRequests.Count==1);
    }
    private static void RoomActionsGuarded()
    {
        var room=SimpleSingletonProvider<GameLogicManager>.inst.room.roomController;
        ProfileBridge.RequestAction("kick|42");
        ProfileBridge.RequestAction("leave|99");
        ProfileBridge.RequestAction("kick|9999");
        Assert(room.Kicks.Count==0 && room.Leaves==0);
        ProfileBridge.RequestAction("kick|99");
        Assert(room.Kicks.SequenceEqual(new long[]{99}));
        ProfileBridge.RequestAction("kick|100");
        Assert(room.Kicks.Count==1); // shared action cooldown
        Time.unscaledTime=3;
        ProfileBridge.RequestAction("leave|42");
        Assert(room.Leaves==1);
        Time.unscaledTime=5;
        room._LocalRoom.Id=321; // stale room after server transition
        try { ProfileBridge.RequestAction("kick|100"); Assert(false); }
        catch(InvalidOperationException) { }
        Assert(room.Kicks.Count==1);
    }
    private static void RoomActionsOwnerChanges()
    {
        var room=SimpleSingletonProvider<GameLogicManager>.inst.room.roomController;
        room._LocalRoom.MasterId=99;
        ProfileBridge.RequestAction("kick|99");
        Assert(room.Kicks.Count==0); // live ownership check, not just UI flag
        ProfileBridge.SetRoomMaster(99);
        Assert(ProfileBridge.ActionState(99)=="0" && ProfileBridge.ActionState(42)=="1");
        ProfileBridge.RequestAction("kick|100");
        Assert(room.Kicks.Count==0);
        ProfileBridge.RequestAction("leave|42");
        Assert(room.Leaves==1); // can always leave own room
        ProfileBridge.BeginRoom(456);
        Assert(ProfileBridge.ActionState(99)=="");
    }
    private static void Reset()
    {
        var original = Rpc.GetShowPlayerS2C.OnGetShowPlayerS2CServerCallBackAsync;
        ProfileBridge.Read(99); ProfileBridge.Stop();
        Assert(Rpc.GetShowPlayerS2C.OnGetShowPlayerS2CServerCallBackAsync == original);
        Reply(99, own: false); Assert(Account.Current.Id == 99);
    }
}

