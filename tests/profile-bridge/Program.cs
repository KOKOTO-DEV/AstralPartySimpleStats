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
        var tests = new Action[] { OwnQuery, Throttle, ManualWindow, Isolation, PrivateStats, Failure, Reset };
        foreach (var test in tests) { Setup(); test(); Console.WriteLine("PASS bridge " + test.Method.Name); }
        ProfileBridge.Stop(); Console.WriteLine("7/7 profile bridge regressions passed (mock game API).");
    }
    private static void OwnQuery()
    {
        Assert(ProfileBridge.Read(42) == "");
        var request = RPCMsgManager.Requests.Single();
        Assert(request.Id == 0 && request.Command == 5153 && request.Type == ProtolcalType.RealTime);
        Reply(42, false); request.Result.keepWaiting = false;
        Assert(ProfileBridge.Read(42) == "42|1|203|230|656");
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
        Assert(ProfileBridge.Read(99) == "99|0|?|?|656");
    }
    private static void Failure()
    {
        var previous = Account.Current;
        ProfileBridge.Read(99); Reply(99, true, 9); RPCMsgManager.Requests[0].Result.keepWaiting = false;
        Time.unscaledTime = 5; Assert(ProfileBridge.Read(99) == "");
        Assert(ReferenceEquals(previous, Account.Current) && RPCMsgManager.Requests.Count == 1);
    }
    private static void Reset()
    {
        var original = Rpc.GetShowPlayerS2C.OnGetShowPlayerS2CServerCallBackAsync;
        ProfileBridge.Read(99); ProfileBridge.Stop();
        Assert(Rpc.GetShowPlayerS2C.OnGetShowPlayerS2CServerCallBackAsync == original);
        Reply(99, own: false); Assert(Account.Current.Id == 99);
    }
}
