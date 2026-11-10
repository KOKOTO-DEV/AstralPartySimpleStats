using AstralPartySimpleStats;

internal static class PlayerDetailsTests
{
    public static IReadOnlyList<(string Name, Func<Task> Run)> Cases { get; } = new[]
    {
        Case("Missing participant details never imply private or score 100", Missing),
        Case("Public statistics and private history remain independent", Mixed),
        Case("Credit deductions clamp without integer overflow", ScoreBounds),
        Case("A replaced participant cannot inherit another player's details", Identity),
        Case("A changed profile getter does not hide a received credit score", ChangedProfile),
        Case("A changed credit getter does not hide received visibility", ChangedCredit),
        Case("Lobby targets use the actual room list without inspecting GRoot", RoomTargets),
        Case("Selection targets follow all four named slots and skip empty slots", SelectionTargets),
        Case("Missing or unsupported panels cannot retain participant targets", MissingTargets),
        Case("Unexpected room list growth has a bounded target read", BoundedTargets),
        Case("Requested win-rate and likes caption uses real values and truncates likes", NumericCaption),
        Case("A profile reply for another account cannot replace participant statistics", WrongProfile),
        Case("Private statistics remain private while public likes remain visible", PrivateProfile),
        Case("Zero games avoids division by zero and missing likes remain unknown", ZeroGames),
        Case("Mixed-mode recent history keeps order and five-row columns", RecentColumns),
        Case("Private and malformed recent history do not expose records", RecentPrivacy),
        Case("PvP placements render separately while summary retains losses", Placements),
        Case("Mode filtering retains its own order counts ranks and numbering", ModeSeparation),
        Case("0.4.0 profile actions distinguish self, host and existing friend", ActionLayout),
        Case("Character selection blocks kicking but preserves self-exit", SelectionKickDisabled),
        Case("Only explicit room exit/close button names and captions are eligible", ExitButtonNames),
        Case("Direct room exit chooses named protocol enum members", DirectExitEnum),
        Case("Automatic room leave confirmation accepts only a positive action", ExitConfirmLabels)
    };
    private static (string, Func<Task>) Case(string name, Action action) =>
        (name, () => { action(); return Task.CompletedTask; });
    private static void Assert(bool value) { if (!value) throw new Exception("Player details assertion failed"); }
    private static void ActionLayout()
    {
        var missing=ProfileActionLayout.FromState(-1, true);
        Assert(!missing.ShowLeave && !missing.ShowFriends && !missing.ShowKick);
        var self=ProfileActionLayout.FromState(9, true);
        Assert(self.ShowLeave && self.IsHost && !self.ShowFriends && !self.ShowKick);
        var visitor=ProfileActionLayout.FromState(0, true);
        Assert(!visitor.ShowLeave && visitor.ShowFriends && !visitor.ShowKick && visitor.CanRegister && visitor.CanBlock);
        var friend=ProfileActionLayout.FromState(2, true);
        Assert(friend.ShowFriends && !friend.CanRegister && friend.CanBlock);
        var blocked=ProfileActionLayout.FromState(4, true);
        Assert(blocked.ShowFriends && !blocked.CanRegister && !blocked.CanBlock);
        var host=ProfileActionLayout.FromState(8, true);
        Assert(host.ShowKick && host.CanRegister && host.CanBlock);
        var hostFriend=ProfileActionLayout.FromState(10, true);
        Assert(hostFriend.ShowKick && !hostFriend.CanRegister);
    }
    private static void SelectionKickDisabled()
    {
        Assert(RoomKickPolicy.AllowsScreen("방"));
        Assert(!RoomKickPolicy.AllowsScreen("캐릭터 선택"));
        Assert(!RoomKickPolicy.AllowsScreen(""));
        Assert(RoomKickPolicy.AllowsPanel("RoomWaitPanel"));
        Assert(!RoomKickPolicy.AllowsPanel("RoomHeroPanel"));
        Assert(!RoomKickPolicy.AllowsPanel("RoomListPanel"));
        Assert(!RoomKickPolicy.AllowsPanel(""));

        var waitingHost = ProfileActionLayout.FromState(8, RoomKickPolicy.AllowsScreen("방"));
        var choosingHost = ProfileActionLayout.FromState(8, RoomKickPolicy.AllowsScreen("캐릭터 선택"));
        Assert(waitingHost.ShowKick && !choosingHost.ShowKick);
        Assert(choosingHost.ShowFriends && choosingHost.CanRegister && choosingHost.CanBlock);
        var waitingSelf = ProfileActionLayout.FromState(9, RoomKickPolicy.AllowsScreen("방"));
        var choosingSelf = ProfileActionLayout.FromState(9, RoomKickPolicy.AllowsScreen("캐릭터 선택"));
        Assert(waitingSelf.ShowLeave && choosingSelf.ShowLeave && choosingSelf.IsHost);
        var nonHostSelf = ProfileActionLayout.FromState(1, false);
        Assert(nonHostSelf.ShowLeave && !nonHostSelf.ShowKick);
        var choosingHostFriend = ProfileActionLayout.FromState(10, false);
        Assert(!choosingHostFriend.ShowKick && !choosingHostFriend.CanRegister);
    }

    private static void DirectExitEnum()
    {
        var values = new[]
        {
            new RoomExitEnumSelection.Value("NormalExit", 3),
            new RoomExitEnumSelection.Value("ForceExit", 5),
            new RoomExitEnumSelection.Value("CloseRoom", 8)
        };
        Assert(RoomExitEnumSelection.Choose(values, false).Number == 3);
        Assert(RoomExitEnumSelection.Choose(values, true).Number == 8);
        var force = new[]
        {
            new RoomExitEnumSelection.Value("NotForceExit", 2),
            new RoomExitEnumSelection.Value("ForceExit", 4)
        };
        Assert(RoomExitEnumSelection.Choose(force, false).Number == 2);
        Assert(RoomExitEnumSelection.Choose(force, true).Number == 4);
        try
        {
            RoomExitEnumSelection.Choose(new[] { new RoomExitEnumSelection.Value("Unspecified", 0) }, false);
            throw new Exception("Unspecified enum must be rejected");
        }
        catch (InvalidOperationException) { }
    }

    private static void ExitConfirmLabels()
    {
        Assert(RoomExitConfirmSelection.PositiveScore("btn_OK", "") > 0);
        Assert(RoomExitConfirmSelection.PositiveScore("btn_confirm", "") > 0);
        Assert(RoomExitConfirmSelection.PositiveScore("btn_yes", "확인") > 0);
        Assert(RoomExitConfirmSelection.PositiveScore("btn_unknown", "确定") > 0);
        Assert(RoomExitConfirmSelection.PositiveScore("btn_unknown", "はい") > 0);
        Assert(RoomExitConfirmSelection.PositiveScore("btn1", "") > 0);
        Assert(RoomExitConfirmSelection.PositiveScore("btn_enter", "") > 0);
        Assert(RoomExitConfirmSelection.PositiveScore("button_sure", "") > 0);
        Assert(RoomExitConfirmSelection.PositiveScore("btn_cancel", "취소") == 0);
        Assert(RoomExitConfirmSelection.PositiveScore("btn_no", "No") == 0);
        Assert(RoomExitConfirmSelection.PositiveScore("btn_left", "") == 0);
        Assert(RoomExitConfirmSelection.PositiveScore("btn_kick", "") == 0);
        Assert(RoomExitConfirmSelection.ExitContext("정말로 방에서 나가겠습니까?"));
        Assert(RoomExitConfirmSelection.ExitContext("Leave room?"));
        Assert(!RoomExitConfirmSelection.ExitContext("설정 저장하시겠습니까?"));
    }

    private static void ExitButtonNames()
    {
        Assert(RoomExitButtonNames.Score("btn_CloseRoom", "", true) > 0);
        Assert(RoomExitButtonNames.Score("btn_ExitRoom", "", false) > 0);
        Assert(RoomExitButtonNames.Score("button_LeaveRoom", "", false) > 0);
        Assert(RoomExitButtonNames.Score("btn_anything", "방 닫기", true) > 0);
        Assert(RoomExitButtonNames.Score("btn_anything", "방 나가기", false) > 0);
        Assert(RoomExitButtonNames.Score("btn_anything", "Leave Room", false) > 0);
        Assert(RoomExitButtonNames.Score("btn_Close", "", true) == 0);
        Assert(RoomExitButtonNames.Score("btn_Back", "", false) == 0);
        Assert(RoomExitButtonNames.Score("btn_KickPlayer", "", true) == 0);
        Assert(RoomExitButtonNames.Score("btn_ClosePopup", "", true) == 0);
        Assert(RoomExitButtonNames.Score("btn_exit", "", false) > 0);
        Assert(RoomExitButtonNames.Score("btn_anything", "나가기", false) == 0);
    }
    private static void Missing()
    {
        var d = new PlayerDetailsReader(new Data { HasProfile = false, HasCredit = false }).Read((IntPtr)1);
        Assert(d.StatisticsPublic == null && d.HistoryPublic == null && d.CreditScore == null);
        Assert(d.Caption == "승률: 확인 불가 · 좋아요: 확인 불가\n매너: 확인 불가");
    }
    private static void Mixed()
    {
        var d = new PlayerDetailsReader(new Data()).Read((IntPtr)1);
        Assert(d.PlayerId == 42 && d.StatisticsPublic == true && d.HistoryPublic == false && d.CreditScore == 93);
        Assert(d.Caption == "승률: 확인 불가 · 좋아요: 확인 불가\n매너: 93");
    }
    private static void ScoreBounds()
    {
        foreach (var (deduction, score) in new[] { (0, 100), (100, 0), (101, 0), (-1, 100), (int.MinValue, 100), (int.MaxValue, 0) })
            Assert(new PlayerDetailsReader(new Data { Deduction = deduction }).Read((IntPtr)1).CreditScore == score);
    }
    private static void Identity()
    {
        var d = new PlayerDetailsReader(new Data { ServerId = 99 }).Read((IntPtr)1);
        Assert(d.PlayerId == 42 && d.StatisticsPublic == null && d.CreditScore == null);
    }
    private static void ChangedProfile()
    {
        var d = new PlayerDetailsReader(new Data { BrokenProfile = true }).Read((IntPtr)1);
        Assert(d.StatisticsPublic == null && d.CreditScore == 93);
    }
    private static void ChangedCredit()
    {
        var d = new PlayerDetailsReader(new Data { BrokenCredit = true }).Read((IntPtr)1);
        Assert(d.StatisticsPublic == true && d.HistoryPublic == false && d.CreditScore == null);
    }
    private static void RoomTargets()
    {
        var paths = new Dictionary<(IntPtr, string), IntPtr>
        {
            [((IntPtr)1, "com_Player")] = (IntPtr)2,
            [((IntPtr)2, "list_playerWait")] = (IntPtr)3
        };
        var rows = PlayerDetailTargets.Find("방", (IntPtr)1, (p, f) => paths[(p, f)],
            p => { Assert(p == (IntPtr)3); return 4; }, (p, i) => (IntPtr)(10 + i));
        Assert(rows.SequenceEqual(new IntPtr[] { (IntPtr)10, (IntPtr)11, (IntPtr)12, (IntPtr)13 }));
    }
    private static void SelectionTargets()
    {
        var fields = new List<string>();
        var rows = PlayerDetailTargets.Find("캐릭터 선택", (IntPtr)1,
            (p, f) => { fields.Add(f); return f == "com_player_3" ? IntPtr.Zero : (IntPtr)fields.Count; },
            _ => throw new Exception("Selection must not traverse a child tree"),
            (_, _) => throw new Exception("Selection must not traverse a child tree"));
        Assert(fields.SequenceEqual(new[] { "com_player_1", "com_player_2", "com_player_3", "com_player_4" }));
        Assert(rows.Count == 3 && rows[2] == (IntPtr)4);
    }
    private static void MissingTargets()
    {
        foreach (var (phase, panel) in new[] { ("방", IntPtr.Zero), ("플레이", (IntPtr)1), ("방", (IntPtr)1) })
        {
            var rows = PlayerDetailTargets.Find(phase, panel, (_, _) => IntPtr.Zero,
                _ => throw new Exception("Unexpected traversal"), (_, _) => throw new Exception("Unexpected traversal"));
            Assert(rows.Count == 0);
        }
    }
    private static void BoundedTargets()
    {
        var calls = 0;
        var rows = PlayerDetailTargets.Find("방", (IntPtr)1, (_, _) => (IntPtr)2,
            _ => 99999, (_, _) => { calls++; return (IntPtr)3; });
        Assert(calls == 16 && rows.Count == 16);
    }
    private static void NumericCaption()
    {
        var d = new PlayerDetails(42, null, null, 100).WithProfile("42|1|203|230|656");
        Assert(d.Caption == "승률: 88.26% (203/230) · 좋아요: 218.6 (656)\n매너: 100");
    }
    private static void WrongProfile()
    {
        var d = new PlayerDetails(42, null, null, 100);
        Assert(d.WithProfile("99|1|203|230|656") == d);
        Assert(d.WithProfile("invalid") == d);
    }
    private static void PrivateProfile()
    {
        var d = new PlayerDetails(42, null, null, 100).WithProfile("42|0|203|230|656");
        Assert(d.WinCount == null && d.PlayCount == null);
        Assert(d.Caption == "승률: 비공개 · 좋아요: 218.6 (656)\n매너: 100");
    }
    private static void ZeroGames()
    {
        var d = new PlayerDetails(42, null, null, 100).WithProfile("42|1|0|0|?");
        Assert(d.Caption == "승률: — (0/0) · 좋아요: 확인 불가\n매너: 100");
        Assert(new PlayerDetails(42, null, null, 100).WithProfile("42|1|300|230|656").WinCount == null);
    }
    private static void RecentColumns()
    {
        var rows=Enumerable.Range(0,10).Select(i=>(i%2==0?"PvE":"PvP")+"\t"+(i==5?"패":"승")+"\t캐릭터"+i);
        var payload=Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(string.Join("\n",rows)));
        var recent=RecentHistory.Parse("1",payload)!;
        Assert(recent.Summary=="최근: 승승승승승패승승승승 (9/10)");
        Assert(recent.Column(0).Split('\n').Length==5 && recent.Column(5).StartsWith("06. PvP 패 캐릭터5"));
        Assert(new PlayerDetails(42,null,null,100).WithProfile("42|1|203|230|656|1|"+payload).Recent?.Rows.Count==10);
        var named=RecentHistory.Parse("1",Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("PvE\t승\t운명의 소녀\t카이세이")))!;
        Assert(named.Column(0,false)=="01. PvE 승 운명의 소녀");
        Assert(named.Column(0,true)=="01. PvE 승 카이세이");
    }
    private static void RecentPrivacy()
    {
        Assert(RecentHistory.Parse("0","not-base64")!.Rows.Count==0);
        Assert(RecentHistory.Parse("0","")!.Summary=="최근: 비공개");
        Assert(RecentHistory.Parse("1","invalid")==null);
        Assert(RecentHistory.Parse("1","")!.Summary=="최근: 기록 없음");
    }
    private static void Placements()
    {
        var text="PvP\t승\t카이세이\nPvP\t2위\t페니\nPvP\t3위\t렌\nPvP\t4위\t파르낫\nPvE\t패\t알라나";
        var recent=RecentHistory.Parse("1",Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text)))!;
        Assert(recent.Summary=="최근: 승패패패패 (1/5)");
        Assert(recent.Column(0).Contains("02. PvP 2위 페니") && recent.Column(0).Contains("03. PvP 3위 렌") && recent.Column(0).Contains("04. PvP 4위 파르낫"));
        Assert(recent.Column(0).Contains("05. PvE 패 알라나"));
        Assert(RecentHistory.Parse("1",Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("PvE\t2위\t렌")))==null);
    }
    private static void ModeSeparation()
    {
        var text="PvP\t2위\t페니\nPvE\t승\t카이세이\nPvP\t승\t렌\nPvE\t패\t알라나";
        var history=RecentHistory.Parse("1",Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text)))!;
        Assert(history.ForMode("PvE").Summary=="최근: 승패 (1/2)");
        Assert(history.ModeSummary("PvP")=="PvP 최근: 패승 (1/2)");
        Assert(history.ForMode("PvP").Column(0)=="01. PvP 2위 페니\n02. PvP 승 렌");
        Assert(history.ForMode("PvE").Column(0)=="01. PvE 승 카이세이\n02. PvE 패 알라나");
        Assert(RecentHistory.Parse("0","")!.ModeSummary("PvE")=="PvE 최근: 비공개");
        var many=RecentHistory.Parse("1",Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(string.Join("\n",Enumerable.Repeat("PvE\t승\t렌",20)))))!;
        Assert(many.ForMode("PvE").Rows.Count==10 && many.ForMode("PvP").Summary=="최근: 기록 없음");
    }
    private sealed class Data : IGameDataAccess
    {
        public bool HasProfile = true, HasCredit = true, BrokenProfile, BrokenCredit;
        public long ServerId = 42;
        public int Deduction = 7;
        public IntPtr ReadObject(IntPtr p, string field) => field == "serverPlayer" ? (IntPtr)2 : throw new MissingFieldException(field);
        public long InvokeInt64(IntPtr p, string method) => p == (IntPtr)1 ? 42 : ServerId;
        public IntPtr Invoke(IntPtr p, string method) => method switch
        {
            "get_ShowPlayer" when BrokenProfile => throw new MissingMethodException(method),
            "get_CreditInfo" when BrokenCredit => throw new MissingMethodException(method),
            "get_ShowPlayer" => HasProfile ? (IntPtr)3 : IntPtr.Zero,
            "get_CreditInfo" => HasCredit ? (IntPtr)4 : IntPtr.Zero,
            _ => throw new MissingMethodException(method)
        };
        public bool InvokeBoolean(IntPtr p, string method) => method == "get_IsShowData";
        public int InvokeInt32(IntPtr p, string method) => Deduction;
        public IntPtr FindClass(string a, string n, string c) => throw new NotSupportedException();
        public IntPtr ReadStaticObject(IntPtr p, string f) => throw new NotSupportedException();
        public int ReadInt32(IntPtr p, string f) => throw new NotSupportedException();
        public IntPtr Invoke(IntPtr p, string m, long arg) => throw new NotSupportedException();
        public string InvokeString(IntPtr p, string m) => throw new NotSupportedException();
        public string InvokeString(IntPtr p, string m, bool arg) => throw new NotSupportedException();
    }
}

