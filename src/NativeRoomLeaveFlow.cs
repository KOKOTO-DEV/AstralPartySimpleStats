using System;
using BepInEx.Logging;
using UnityEngine;

namespace AstralPartySimpleStats;

/// <summary>
/// Request a single voluntary exit (server RPC), then close the client-side
/// room panel using the game's native UIManager stack. No room-exit button,
/// confirmation dialog, or synthetic RPC is invoked.
/// </summary>
internal static class NativeRoomLeaveFlow
{
    private static readonly NativeGameData Data = new();
    private static bool _running;
    private static float _startedAt;
    private static float _lastRouteAt;
    private static long _roomId;
    private static string _startedPanel = "";
    private static string _rpcDescription = "";
    private static int _routeStage;

    public static string Start(long playerId, string expectedRoom)
    {
        if (_running) return "room exit request already in progress";
        var panel = NativeRoomNavigation.CurrentPanel();
        if (panel is not ("RoomWaitPanel" or "RoomHeroPanel"))
            throw new InvalidOperationException("Leave requires a room or character-selection panel (actual=" + panel + ").");
        if (!long.TryParse(expectedRoom, out var roomId) || roomId <= 0)
            throw new InvalidOperationException("Current room ID is unavailable.");

        var description = NativeRoomActions.Invoke("leave", roomId, playerId);
        if (string.IsNullOrWhiteSpace(description))
            throw new InvalidOperationException("Game room-exit request could not be dispatched.");
        _roomId = roomId;
        _rpcDescription = description;
        _startedPanel = panel;
        _startedAt = Time.unscaledTime;
        _lastRouteAt = 0;
        _routeStage = 0;
        _running = true;
        return description + " (direct RPC from " + panel + "; UI cleanup after server response; no confirmation)";
    }

    public static void Tick(ManualLogSource log)
    {
        if (!_running) return;
        try
        {
            var panel = NativeRoomNavigation.CurrentPanel();
            if (NativeRoomNavigation.IsOutsideRoom(panel))
            {
                StatsRuntime.LogDiagnostic("Room leave finished: " + _startedPanel + " -> " + panel + ".");
                Reset();
                return;
            }
            var elapsed = Time.unscaledTime - _startedAt;
            if (elapsed > 13f)
            {
                log.LogWarning("Room leave UI synchronization timed out (panel=" + panel + 
                    "; roomId=" + _roomId + "; rpc=" + _rpcDescription +
                    "; routeStage=" + _routeStage + "). " + NativeRoomNavigation.DescribeAvailableRoutes());
                Reset();
                return;
            }
            // Critical: server-side exit must be acknowledged in the game room
            // model before changing the current panel. Otherwise client and
            // server room states can get out of sync.
            if (elapsed < 0.2f || !RoomHasEnded()) return;

            // Stage 0: the real UIManager history operation. From hero-select,
            // this can lead to the wait panel instead of the room list.
            if (_routeStage == 0)
            {
                _routeStage = 1;
                _lastRouteAt = Time.unscaledTime;
                if (NativeRoomNavigation.TryReturnToPrevious(out var route))
                    StatsRuntime.LogDiagnostic("Room leave navigation: " + route);
                else
                    StatsRuntime.LogDiagnostic("Room leave previous-panel navigation unavailable: " + route);
                return;
            }
            if (Time.unscaledTime - _lastRouteAt < 0.65f) return;

            // Stage 1: If character selection returned to the waiting-room
            // screen, pop the same UIManager stack once more. Otherwise use
            // the exact room-list panel enum discovered at runtime.
            if (_routeStage == 1)
            {
                if (_startedPanel == "RoomHeroPanel" && panel == "RoomWaitPanel")
                {
                    _routeStage = 2;
                    _lastRouteAt = Time.unscaledTime;
                    if (NativeRoomNavigation.TryReturnToPrevious(out var route))
                        StatsRuntime.LogDiagnostic("Room leave navigation (second panel): " + route);
                    else
                        StatsRuntime.LogDiagnostic("Room leave second-panel navigation unavailable: " + route);
                    return;
                }
                _routeStage = 2;
            }
            // Stage 2: open the game's actual RoomListPanel, using metadata
            // rather than a hard-coded UIPanelType integer.
            if (_routeStage == 2)
            {
                _routeStage = 3;
                _lastRouteAt = Time.unscaledTime;
                if (NativeRoomNavigation.TryOpenRoomList(out var route))
                    StatsRuntime.LogDiagnostic("Room leave navigation (room list): " + route);
                else
                    StatsRuntime.LogDiagnostic("Room leave list routing unavailable: " + route);
                return;
            }
            // Stage 3: last resort, close the stale room UI by returning home.
            // This is better than leaving an unusable room overlay. Only after
            // server exit has completed, never while still in an active room.
            if (_routeStage == 3)
            {
                _routeStage = 4;
                _lastRouteAt = Time.unscaledTime;
                if (NativeRoomNavigation.TryReturnHome(out var route))
                    StatsRuntime.LogDiagnostic("Room leave navigation (home fallback): " + route);
                else
                    StatsRuntime.LogDiagnostic("Room leave navigation failed: " + route);
            }
        }
        catch (Exception ex)
        {
            log.LogWarning("Room leave state/UI synchronization failed: " + ex.GetType().Name + ": " + ex.Message +
                "; routes=" + NativeRoomNavigation.DescribeAvailableRoutes());
            Reset();
        }
    }

    private static bool RoomHasEnded()
    {
        var klass = Data.FindClass("AstralParty.Runtime", "GameLogic", "GameLogicManager");
        if (klass == IntPtr.Zero) return false;
        var manager = Data.ReadStaticObject(klass, "_inst");
        if (manager == IntPtr.Zero) return false;
        var roomLogic = Data.ReadObject(manager, "<room>k__BackingField");
        if (roomLogic == IntPtr.Zero) return true;
        var controller = Data.ReadObject(roomLogic, "roomController");
        if (controller == IntPtr.Zero) return true;
        if (Data.ReadInt32(controller, "roomStateType") == 0) return true;
        var room = Data.ReadObject(controller, "_LocalRoom");
        return room == IntPtr.Zero || Data.InvokeInt64(room, "get_Id") != _roomId;
    }

    public static void Reset()
    {
        _running = false;
        _startedAt = 0;
        _lastRouteAt = 0;
        _roomId = 0;
        _startedPanel = "";
        _rpcDescription = "";
        _routeStage = 0;
    }
}
