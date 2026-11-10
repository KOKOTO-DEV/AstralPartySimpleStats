using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

[assembly: AssemblyVersion(AstralPartySimpleStats.Plugin.Version)]
[assembly: AssemblyFileVersion(AstralPartySimpleStats.Plugin.Version)]

namespace AstralPartySimpleStats;

[BepInPlugin(Id, Name, Version)]
public sealed class Plugin : BasePlugin
{
    public const string Id = "astral-party.simple-stats";
    public const string Name = "AstralPartySimpleStats";
    public const string Version = AstralBuildVersion.Value;
    private static Plugin? _instance;
    private Harmony? _harmony;
    private ConfigEntry<bool>? _enabled;
    private ConfigEntry<bool>? _verboseLogging;
    private int _frame = -1;

    public override void Load()
    {
        try
        {
            _enabled = Config.Bind("General", "Enabled", true, "대기/캐릭터 선택 화면의 승률·좋아요·매너 표시 및 프로필 자동 조회");
            _verboseLogging = Config.Bind("Diagnostics", "VerboseLogging", false,
                "정상 동작 추적 로그를 자세히 기록합니다. 문제 진단 시에만 true로 설정하세요.");
            StatsRuntime.Initialize(Log);
            StatsRuntime.VerboseLogging = _verboseLogging.Value;
            _instance = this;
            _harmony = new Harmony(Id);
            var update = typeof(EventSystem).GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new MissingMethodException("EventSystem.Update");
            _harmony.Patch(update, postfix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(Update), BindingFlags.Static | BindingFlags.NonPublic)));
            Log.LogInfo(Name + " " + Version + " loaded (standalone).");
        }
        catch (Exception ex) { Unload(); Log.LogError("Initialization failed: " + ex); }
    }

    private static void Update()
    {
        var plugin = _instance;
        if (plugin == null || plugin._frame == Time.frameCount) return;
        plugin._frame = Time.frameCount;
        StatsRuntime.ShowPlayerDetails = plugin._enabled?.Value ?? true;
        StatsRuntime.VerboseLogging = plugin._verboseLogging?.Value ?? false;
        StatsRuntime.HandleRecentInput();
        NativeRoomLeaveFlow.Tick(plugin.Log);
        StatsRuntime.Tick();
    }

    public override bool Unload()
    {
        _instance = null;
        try { _harmony?.UnpatchSelf(); } catch (Exception ex) { Log.LogWarning(ex.Message); }
        _harmony = null;
        StatsRuntime.Shutdown();
        _frame = -1;
        return true;
    }
}
