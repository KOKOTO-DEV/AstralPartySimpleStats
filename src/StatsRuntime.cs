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
    private static readonly RoomState _gameState = new();
    private static readonly ScreenState _snapshot = new();
    private static float _nextRead;
    private sealed class RoomState
    {
        public bool Available;
        public bool InformationReady => Available;
        public string RoomId = string.Empty;
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
            var uiManager = FairyGui.ReadStaticObject(FairyGui.Class("UI", "UIManager"), "_inst");
            var panel = uiManager == IntPtr.Zero ? IntPtr.Zero : FairyGui.Call(uiManager, "get_currentPanel");
            var ui = panel == IntPtr.Zero ? IntPtr.Zero : FairyGui.Call(panel, "get_UIObject");
            _snapshot.ScreenPhase = FairyGui.ClassName(panel) switch
            {
                "RoomWaitPanel" => "방", "RoomHeroPanel" => "캐릭터 선택", _ => string.Empty
            };
            _gameState.Available = false;
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
                }
            }
            RefreshPlayerDetails();
            RefreshVisibilityToggle();
        }
        catch (Exception ex)
        {
            ClearPlayerDetails();
            _nextRead = Time.unscaledTime + 1;
            LogRuntimeWarning("tick", "Stats update unavailable: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static void LogRuntimeWarning(string key, string message)
    {
        if (Warnings.TryGetValue(key, out var next) && Time.unscaledTime < next) return;
        Warnings[key] = Time.unscaledTime + 30;
        _log?.LogWarning(message);
    }

    public static void Shutdown()
    {
        ClearPlayerDetails();
        DisposeVisibilityToggle();
        try { ProfileQuery.Shutdown(); } catch (Exception ex) { _log?.LogWarning(ex.Message); }
        FguiNode.ShutdownResources();
        FairyGui.Clear(); NativeData.Clear(); Warnings.Clear();
        _gameState.Available = false; _gameState.RoomId = string.Empty;
        _snapshot.ScreenPhase = string.Empty; _nextRead = 0; _log = null;
    }
}
