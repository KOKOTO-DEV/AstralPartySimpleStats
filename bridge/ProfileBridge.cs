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
    private static readonly Dictionary<long, float> RetryAt = new Dictionary<long, float>();
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
            Stop();
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
        if (error == 0 && model?.ShowData != null)
        {
            var data = model.ShowData;
            if (data.PlayerId > 0)
            {
                var stats = data.Statistics;
                var visible = data.PlayerId == _accountId || data.IsShowData;
                Cache[data.PlayerId] = new Entry
                {
                    Id = data.PlayerId, Public = visible,
                    Wins = visible && stats != null ? (int?)stats.WinFightCount : null,
                    Games = visible && stats != null ? (int?)stats.FightCount : null,
                    Likes = data.PraiseNum, At = Time.unscaledTime,
                    HistoryPublic = data.PlayerId == _accountId || data.IsShowFight,
                    Recent = BuildRecent(data, data.PlayerId == _accountId || data.IsShowFight)
                };
            }
        }
        // This response is still at the head of RPCMsgManager's dispatch queue
        // until the callback completes. Correlate its actual RPCAsyncResult,
        // not just its player ID: simultaneous manual requests can target the
        // same person. Our response only feeds Cache; manual responses retain
        // the game's original callback and PlayerCareer/window behavior.
        if (_pending != null && _rpc != null && Queue?.GetValue(_rpc) is System.Collections.IList queue
            && queue.Count > 0 && ReferenceEquals(CallAction?.GetValue(queue[0]), _pending))
            return UniTask.CompletedTask;
        return _original != null ? _original(model!, error, dispatch) : UniTask.CompletedTask;
    }

    // id|public|wins or ?|games or ?|likes; empty means not yet available.
    public static string Read(long id)
    {
        if (id <= 0 || !Ensure()) return string.Empty;
        if (_pending != null && !_pending.keepWaiting)
        {
            _pending = null;
            _pendingId = 0;
        }
        var now = Time.unscaledTime;
        if (Cache.TryGetValue(id, out var entry) && now - entry.At < 60f)
            return string.Join("|", entry.Id.ToString(CultureInfo.InvariantCulture), entry.Public ? "1" : "0",
                entry.Wins?.ToString(CultureInfo.InvariantCulture) ?? "?",
                entry.Games?.ToString(CultureInfo.InvariantCulture) ?? "?", entry.Likes.ToString(CultureInfo.InvariantCulture))
                + "|" + (entry.HistoryPublic ? "1" : "0") + "|" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(entry.Recent));
        // Pause automatic requests around manual profile UI; no windows are opened.
        var window = SimpleSingletonProvider<UI.UIManager>.inst?.AccountInfo;
        if (_pending != null || now < _nextSend || (window != null && window.isShowing)
            || (RetryAt.TryGetValue(id, out var retry) && now < retry)) return string.Empty;
        _nextSend = now + 2f;
        RetryAt[id] = now + 60f;
        _pendingId = id;
        try
        {
            // RealTime avoids the foreground network spinner/reconnect UI.
            _pending = RPCMsgManager.RPCCallStatic(new GetShowPlayerC2S { PlayerId = id == _accountId ? 0 : id },
                5153, ProtolcalType.RealTime);
        }
        catch { _pending = null; _pendingId = 0; throw; }
        if (Cache.Count > 32)
        {
            foreach (var key in new List<long>(Cache.Keys)) if (now - Cache[key].At >= 60f) Cache.Remove(key);
        }
        if (RetryAt.Count > 32)
        {
            foreach (var key in new List<long>(RetryAt.Keys)) if (now >= RetryAt[key]) RetryAt.Remove(key);
        }
        return string.Empty;
    }

    public static void Stop()
    {
        if (_rpc != null && _rpc.GetShowPlayerS2C.OnGetShowPlayerS2CServerCallBackAsync == Handler)
            _rpc.GetShowPlayerS2C.OnGetShowPlayerS2CServerCallBackAsync = _original;
        _rpc = null; _account = null; _original = null;
        _pending = null; _pendingId = 0; _accountId = 0; _nextSend = 0;
        Cache.Clear(); RetryAt.Clear();
    }

    private static string BuildRecent(ShowPlayer data, bool visible)
    {
        if (!visible) return string.Empty;
        var records = new List<ShowPlayerShortFight>(data.Record);
        records.Sort((a,b) => { var time = b.Time.CompareTo(a.Time); return time != 0 ? time : b.Index.CompareTo(a.Index); });
        var lines = new List<string>();
        for (var i = 0; i < Math.Min(10, records.Count); i++)
        {
            var r = records[i];
            string name;
            try { name = r.HeroId == 0 ? "미상" : CharacterHandle.GetCharacterNickName(r.HeroId); }
            catch { name = "캐릭터 " + r.HeroId.ToString(CultureInfo.InvariantCulture); }
            name = (name ?? "미상").Replace('\t',' ').Replace('\n',' ').Replace('\r',' ');
            string realName;
            try { realName = r.HeroId == 0 ? "미상" : CharacterHandle.GetCharacterName(r.HeroId); }
            catch { realName = name; }
            realName=(realName??name).Replace('\t',' ').Replace('\n',' ').Replace('\r',' ');
            lines.Add((BattleConfig.IsPVE(r.MapType) ? "협동" : "PvP") + "\t" + (r.Rank == 1 ? "승" : "패") + "\t" + name + "\t" + realName);
        }
        return string.Join("\n", lines);
    }
}
