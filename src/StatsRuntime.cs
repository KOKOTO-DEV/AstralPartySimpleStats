using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace AstralPartySimpleStats;

internal static partial class StatsRuntime
{
    private static readonly NativeGameData NativeData = new();
    private static readonly PlayerDetailsReader DetailsReader = new(NativeData);
    private static readonly Dictionary<string, float> Warnings = new();
    private static ManualLogSource? _log;
    public static bool VerboseLogging { get; set; }
    private static float _uiManagerMissingSince = -1f;
    private static bool _uiManagerWasFound;
    private static readonly RoomState _gameState = new();
    private static readonly ScreenState _snapshot = new();
    private static float _nextRead;
    private sealed class RoomState
    {
        public bool Available;
        public bool InformationReady => Available;
        public string RoomId = string.Empty;
        public long RoomMasterId;
    }
    private sealed class ScreenState { public string ScreenPhase = string.Empty; }

    public static void Initialize(ManualLogSource log)
    {
        _log=log;
        _manualHidden=PlayerPrefs.GetInt("AstralPartySimpleStats.InfoHidden",0)!=0;
        _useCharacterNames=PlayerPrefs.GetInt("AstralPartySimpleStats.CharacterNames",1)!=0;
    }

    public static void Tick()
    {
        if (_log == null || Time.unscaledTime < _nextRead) return;
        _nextRead = Time.unscaledTime + 0.25f;
        try
        {
            var uiType = FairyGui.Class("UI", "UIManager");
            _uiManagerWasFound = true;
            _uiManagerMissingSince = -1f;
            var uiManager = FairyGui.ReadStaticObject(uiType, "_inst");
            var panel = uiManager == IntPtr.Zero ? IntPtr.Zero : FairyGui.Call(uiManager, "get_currentPanel");
            var ui = panel == IntPtr.Zero ? IntPtr.Zero : FairyGui.Call(panel, "get_UIObject");
            _snapshot.ScreenPhase = FairyGui.ClassName(panel) switch
            {
                "RoomWaitPanel" => "방", "RoomHeroPanel" => "캐릭터 선택", _ => string.Empty
            };
            _gameState.Available = false;
            _gameState.RoomMasterId = 0;
            if (_snapshot.ScreenPhase.Length > 0 && ui != IntPtr.Zero && FairyGui.Get<bool>(ui, "get_onStage"))
            {
                var manager = NativeData.ReadStaticObject(NativeData.FindClass("AstralParty.Runtime", "GameLogic", "GameLogicManager"), "_inst");
                var roomLogic = NativeData.ReadObject(manager, "<room>k__BackingField");
                var controller = NativeData.ReadObject(roomLogic, "roomController");
                var room = NativeData.ReadObject(controller, "_LocalRoom");
                if (room != IntPtr.Zero && NativeData.ReadInt32(controller, "roomStateType") != 0)
                {
                    var id = NativeData.InvokeInt64(room, "get_Id");
                    _gameState.Available = id > 0;
                    _gameState.RoomId = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    // Protobuf room MasterId is the current host, not the first
                    // participant or the person who initially created the room.
                    try { _gameState.RoomMasterId = NativeData.InvokeInt64(room, "get_MasterId"); }
                    catch (MissingMethodException)
                    {
                        // An unrecognized game build may still show statistics.
                        // Owner-only actions remain hidden rather than guessing.
                        _gameState.RoomMasterId = 0;
                        LogRuntimeWarning("room-master", "Room master ID getter unavailable; kick control disabled.");
                    }
                }
            }
            if (!_gameState.Available || !ShowPlayerDetails) ProfileQuery.EndRoom();
            else
            {
                var rows=PlayerDetailTargets.Find(_snapshot.ScreenPhase,ui,NativeData.ReadObject,
                    n=>FairyGui.Get<int>(n,"get_numChildren"),(n,i)=>FairyGui.Call(n,"GetChildAt",FairyGui.Int,i));
                var ids=new List<string>();
                foreach(var row in rows)
                {
                    var player=NativeData.ReadObject(row,_snapshot.ScreenPhase=="방"?"playerInfo":"playerData");
                    if(player==IntPtr.Zero || NativeData.InvokeBoolean(player,"get_IsBot")) continue;
                    var id=NativeData.InvokeInt64(player,"get_Id");
                    if(id>0) ids.Add(id.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
                ProfileQuery.SyncParticipants(_gameState.RoomId,string.Join(",",ids),_gameState.RoomMasterId);
            }
            RefreshPlayerDetails();
            RefreshVisibilityToggle();
        }
        catch (Exception ex)
        {
            ClearPlayerDetails();
            _nextRead = Time.unscaledTime + 1;
            // AstralParty.Runtime/UI.UIManager can be registered after the plugin
            // starts. Do not report this expected initialization window as a
            // failure. A genuinely missing UI type is still warned after 15s.
            if (ex is TypeLoadException && ex.Message == "Game UI type is unavailable: UI.UIManager"
                && !_uiManagerWasFound)
            {
                if (_uiManagerMissingSince < 0f) _uiManagerMissingSince = Time.unscaledTime;
                if (Time.unscaledTime - _uiManagerMissingSince < 15f) return;
            }
            LogRuntimeWarning("tick", "Stats update unavailable: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    internal static void LogDiagnostic(string message)
    {
        // BepInEx default logging stays quiet; enable explicitly for support.
        if (VerboseLogging) _log?.LogInfo(message);
    }

    private static void LogRuntimeWarning(string key, string message)
    {
        if (Warnings.TryGetValue(key, out var next) && Time.unscaledTime < next) return;
        Warnings[key] = Time.unscaledTime + 30;
        _log?.LogWarning(message);
    }

    public static void Shutdown()
    {
        NativeRoomLeaveFlow.Reset();
        ClearPlayerDetails();
        DisposeVisibilityToggle();
        try { ProfileQuery.Shutdown(); } catch (Exception ex) { _log?.LogWarning(ex.Message); }
        FguiNode.ShutdownResources();
        FairyGui.Clear(); NativeData.Clear(); Warnings.Clear();
        _gameState.Available = false; _gameState.RoomId = string.Empty; _gameState.RoomMasterId = 0;
        _snapshot.ScreenPhase = string.Empty; _nextRead = 0; _log = null;
        VerboseLogging = false;
        _uiManagerMissingSince = -1f;
        _uiManagerWasFound = false;
    }
}
