using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Core.Net;
using Cysharp.Threading.Tasks;
using GameLogic;
using Tools;
using UnityEngine;
using party.protocol;

namespace AstralPartySimpleStats.RuntimeBridge;

// Loaded into the game's HybridCLR domain (not the BepInEx CLR). This lets us
// use its typed RPC API and forward the original callback without native hooks.
public static class ProfileBridge
{
    private const float CacheSeconds = 600f;
    private sealed class Entry
    {
        public long Id;
        public bool Public;
        public int? Wins, Games;
        public int Likes;
        public float At;
        public bool HistoryPublic;
        public string Recent = string.Empty;
    }
    private static readonly Dictionary<long, Entry> Cache = new Dictionary<long, Entry>();
    private static readonly HashSet<long> Active = new HashSet<long>();
    private static readonly HashSet<long> Attempted = new HashSet<long>();
    private static long _roomId, _pendingRoom, _roomMasterId;
    private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly FieldInfo? Queue = typeof(RPCMsgManager).GetField("_OnPushRPCMsg", InstanceFields);
    private static readonly PropertyInfo? CallAction = Queue?.FieldType.GetGenericArguments()[0].GetProperty("callAction", InstanceFields);
    private static RPCMsgManager? _rpc;
    private static AccountLogic? _account;
    private static GetShowPlayerS2CRPC.OnGetShowPlayerS2CServerDelegate? _original;
    private static readonly GetShowPlayerS2CRPC.OnGetShowPlayerS2CServerDelegate Handler = OnProfile;
    private static RPCAsyncResult? _pending;
    private static long _pendingId;
    private static long _accountId;
    private static float _nextSend;
    private static float _nextAction;

    public static string ActionState(long id)
    {
        if (!Active.Contains(id) || !Ensure()) return string.Empty;
        var hostFlag = _roomId != 0 && _roomMasterId == _accountId ? 8 : 0;
        if (id == _accountId) return (1 | hostFlag).ToString(CultureInfo.InvariantCulture);
        var friend = SimpleSingletonProvider<GameLogicManager>.inst.friend;
        return ((friend.IsFriend(id)?2:0) | (friend.IsBlack(id)?4:0) | hostFlag).ToString(CultureInfo.InvariantCulture);
    }

    public static void RequestAction(string command)
    {
        var parts=command.Split('|');
        if (parts.Length != 2 || !long.TryParse(parts[1], out var id) || !Active.Contains(id)
            || !Ensure() || Time.unscaledTime < _nextAction) return;
        if (parts[0] == "leave")
        {
            if (id != _accountId || _roomId <= 0) return;
            if (RoomActions.Invoke("leave", _roomId, _accountId, id))
                _nextAction = Time.unscaledTime + 2;
            return;
        }
        if (parts[0] == "kick")
        {
            if (id == _accountId || _roomMasterId != _accountId || _roomId <= 0) return;
            if (RoomActions.Invoke("kick", _roomId, _accountId, id))
                _nextAction = Time.unscaledTime + 2;
            return;
        }
        if (id == _accountId) return;
        var friend=SimpleSingletonProvider<GameLogicManager>.inst.friend;
        if(parts[0]=="friend" && !friend.IsFriend(id) && !friend.IsBlack(id))
        {
            _nextAction=Time.unscaledTime+2;
            friend.RequestFriendApplyC2S(id);
        }
        else if(parts[0]=="block" && !friend.IsBlack(id))
        {
            _nextAction=Time.unscaledTime+2;
            friend.RequestFriendOpC2S(id,string.Empty,2,null);
        }
    }

    private static bool Ensure()
    {
        var account = SimpleSingletonProvider<GameLogicManager>.inst?.account;
        var net = MonoSingletonProvider<NetManager>.inst;
        if (Queue == null || CallAction == null)
            throw new MissingMemberException("Profile RPC queue correlation metadata is unavailable.");
        if (account == null || net == null || !net.IsConnected) return false;
        var rpc = net.RPC;
        if (!ReferenceEquals(_rpc, rpc) || !ReferenceEquals(_account, account) || _accountId != account.GetPlayerID())
        {
            var changedAccount = _accountId != account.GetPlayerID();
            Detach();
            if (changedAccount) { Cache.Clear(); Attempted.Clear(); }
            _rpc = rpc;
            _account = account;
            _accountId = account.GetPlayerID();
        }
        if (_rpc.GetShowPlayerS2C.OnGetShowPlayerS2CServerCallBackAsync != Handler)
        {
            _original = _rpc.GetShowPlayerS2C.OnGetShowPlayerS2CServerCallBackAsync;
            if (_original == null) return false;
            _rpc.GetShowPlayerS2C.OnGetShowPlayerS2CServerCallBackAsync = Handler;
        }
        return true;
    }

    private static UniTask OnProfile(GetShowPlayerS2C model, int error, bool dispatch)
    {
        var ownResponse = _pending != null && _rpc != null && Queue?.GetValue(_rpc) is System.Collections.IList queue
            && queue.Count > 0 && ReferenceEquals(CallAction?.GetValue(queue[0]), _pending);
        if (error == 0 && model?.ShowData != null)
        {
            var data = model.ShowData;
            if (_roomId != 0 && Active.Contains(data.PlayerId) && (!ownResponse || _pendingRoom == _roomId))
            {
                var stats = data.Statistics;
                var visible = data.PlayerId == _accountId || data.IsShowData;
                var entry = new Entry
                {
                    Id = data.PlayerId, Public = visible,
                    Wins = visible && stats != null ? (int?)stats.WinFightCount : null,
                    Games = visible && stats != null ? (int?)stats.FightCount : null,
                    Likes = data.PraiseNum, At = Time.unscaledTime,
                    HistoryPublic = data.PlayerId == _accountId || data.IsShowFight,
                    Recent = BuildRecent(data, data.PlayerId == _accountId || data.IsShowFight)
                };
                Cache[data.PlayerId] = entry;
                TrimCache();
            }
        }
        // This response is still at the head of RPCMsgManager's dispatch queue
        // until the callback completes. Correlate its actual RPCAsyncResult,
        // not just its player ID: simultaneous manual requests can target the
        // same person. Our response only feeds Cache; manual responses retain
        // the game's original callback and PlayerCareer/window behavior.
        if (ownResponse)
            return UniTask.CompletedTask;
        return _original != null ? _original(model!, error, dispatch) : UniTask.CompletedTask;
    }

    // id|public|wins or ?|games or ?|likes; empty means not yet available.
    public static string Read(long id)
    {
        if (id <= 0 || !Active.Contains(id) || !Ensure()) return string.Empty;
        if (_pending != null && !_pending.keepWaiting)
        {
            _pending = null;
            _pendingId = 0;
        }
        var now = Time.unscaledTime;
        TrimCache();
        if (Cache.TryGetValue(id, out var entry) && now - entry.At < CacheSeconds)
        {
            Attempted.Add(id);
            return Serialize(entry);
        }
        if (Attempted.Contains(id)) return string.Empty;
        // Pause automatic requests around manual profile UI; no windows are opened.
        var window = SimpleSingletonProvider<UI.UIManager>.inst?.AccountInfo;
        if (_pending != null || now < _nextSend || (window != null && window.isShowing)) return string.Empty;
        _nextSend = now + 2f;
        Attempted.Add(id);
        _pendingRoom = _roomId;
        _pendingId = id;
        try
        {
            _pending = RPCMsgManager.RPCCallStatic(new GetShowPlayerC2S { PlayerId = id == _accountId ? 0 : id },
                5153, ProtolcalType.RealTime);
        }
        catch { _pending = null; _pendingId = 0; throw; }
        return string.Empty;
    }

    private static string Serialize(Entry entry) =>
            string.Join("|", entry.Id.ToString(CultureInfo.InvariantCulture), entry.Public ? "1" : "0",
                entry.Wins?.ToString(CultureInfo.InvariantCulture) ?? "?",
                entry.Games?.ToString(CultureInfo.InvariantCulture) ?? "?", entry.Likes.ToString(CultureInfo.InvariantCulture))
                + "|" + (entry.HistoryPublic ? "1" : "0") + "|" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(entry.Recent));

    public static void BeginRoom(long roomId)
    {
        if (_roomId == roomId) return;
        _roomId = roomId;
        _roomMasterId = 0;
        Cache.Clear(); Attempted.Clear(); Active.Clear();
    }

    public static void SetRoomMaster(long masterId)
    {
        // Re-sent after BeginRoom so stale ownership cannot outlive a room swap.
        _roomMasterId = _roomId > 0 && masterId > 0 ? masterId : 0;
    }

    public static void SetParticipants(string ids)
    {
        var next = new HashSet<long>();
        foreach (var token in ids.Split(','))
            if (next.Count < 4 && long.TryParse(token, out var id) && id > 0) next.Add(id);
        Active.Clear(); foreach (var id in next) Active.Add(id);
        foreach (var id in new List<long>(Cache.Keys)) if (!Active.Contains(id)) Cache.Remove(id);
        foreach (var id in new List<long>(Attempted)) if (!Active.Contains(id)) Attempted.Remove(id);
        TrimCache();
    }

    private static void TrimCache()
    {
        var now = Time.unscaledTime;
        foreach (var key in new List<long>(Cache.Keys))
            if (now - Cache[key].At >= CacheSeconds) { Cache.Remove(key); Attempted.Remove(key); }
    }

    private static void Detach()
    {
        if (_rpc != null && _rpc.GetShowPlayerS2C.OnGetShowPlayerS2CServerCallBackAsync == Handler)
            _rpc.GetShowPlayerS2C.OnGetShowPlayerS2CServerCallBackAsync = _original;
        _rpc = null; _account = null; _original = null;
        _pending = null; _pendingId = 0; _accountId = 0; _nextSend = 0;
    }

    public static void Stop()
    {
        Detach();
        _roomId = 0; _pendingRoom = 0; _roomMasterId = 0;
        _nextAction=0;
        Cache.Clear(); Active.Clear(); Attempted.Clear();
    }

    private static string BuildRecent(ShowPlayer data, bool visible)
    {
        if (!visible) return string.Empty;
        var records = new List<ShowPlayerShortFight>(data.Record);
        records.Sort((a,b) => { var time = b.Time.CompareTo(a.Time); return time != 0 ? time : b.Index.CompareTo(a.Index); });
        var lines = new List<string>();
        for (var i = 0; i < Math.Min(10,records.Count); i++)
        {
            var r = records[i];
            var pve = BattleConfig.IsPVE(r.MapType);
            string name;
            try { name = r.HeroId == 0 ? "미상" : CharacterHandle.GetCharacterNickName(r.HeroId); }
            catch { name = "캐릭터 " + r.HeroId.ToString(CultureInfo.InvariantCulture); }
            name = (name ?? "미상").Replace('\t',' ').Replace('\n',' ').Replace('\r',' ');
            string realName;
            try { realName = r.HeroId == 0 ? "미상" : CharacterHandle.GetCharacterName(r.HeroId); }
            catch { realName = name; }
            realName=(realName??name).Replace('\t',' ').Replace('\n',' ').Replace('\r',' ');
            var result = r.Rank == 1 ? "승" : !pve && r.Rank >= 2 && r.Rank <= 4
                ? r.Rank.ToString(CultureInfo.InvariantCulture) + "위" : "패";
            lines.Add((pve ? "PvE" : "PvP") + "\t" + result + "\t" + name + "\t" + realName);
        }
        return string.Join("\n", lines);
    }
}
