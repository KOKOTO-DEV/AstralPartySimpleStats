using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace AstralPartySimpleStats;

internal static partial class StatsRuntime
{
    public static bool ShowPlayerDetails { get; set; } = true;
    private static readonly ProfileQueryAccess ProfileQuery = new();
    private static readonly Dictionary<IntPtr, DetailLabel> DetailLabels = new();
    private static float _nextDetailsAt;
    private static string _detailsRoom = string.Empty;
    private static string _detailsSummary = string.Empty;

    private sealed class DetailLabel
    {
        public readonly FguiNode Host;
        public readonly FguiNode Container;
        private readonly FguiNode _background;
        public readonly FguiNode Text;
        public DetailLabel(IntPtr host)
        {
            Host = FguiNode.Retain(host);
            try
            {
                Container = FguiNode.Component(380, 48);
                Container.SetTouchable(false);
                Container.SetSortingOrder(10);
                _background = FguiNode.Graph(380, 48);
                _background.SetTouchable(false);
                _background.Fill(new Color32(20, 24, 32, 225), 4);
                Container.AddChild(_background);
                Text = FguiNode.Text(368, 44);
                Text.SetTouchable(false);
                Text.SetXY(6, 2);
                Container.AddChild(Text);
                Host.AddChild(Container);
            }
            catch
            {
                // Children can fail before attachment; dispose all owned nodes.
                try { Text?.Dispose(); } catch { }
                try { _background?.Dispose(); } catch { }
                try { Container?.Dispose(); } catch { }
                Host.ReleaseHandle();
                throw;
            }
        }
        public void Dispose()
        {
            try { Container.Dispose(); }
            finally
            {
                Text.ReleaseHandle();
                _background.ReleaseHandle();
                Host.ReleaseHandle();
            }
        }

        public void SetBounds(float x, float y, float width, float height)
        {
            Container.SetXY(x, y);
            Container.SetSize(width, height);
            _background.SetSize(width, height);
        }
    }

    private static void RefreshPlayerDetails()
    {
        if (!ShowPlayerDetails || !_gameState.Available || !_gameState.InformationReady
            || _snapshot.ScreenPhase is not ("방" or "캐릭터 선택"))
        {
            ClearPlayerDetails();
            return;
        }
        if (_detailsRoom != _gameState.RoomId)
        {
            ClearPlayerDetails();
            _detailsRoom = _gameState.RoomId;
        }
        if (Time.unscaledTime < _nextDetailsAt) return;
        _nextDetailsAt = Time.unscaledTime + 0.5f;
        var found = new HashSet<IntPtr>();
        var manager = FairyGui.ReadStaticObject(FairyGui.Class("UI", "UIManager"), "_inst");
        var panel = manager == IntPtr.Zero ? IntPtr.Zero : FairyGui.Call(manager, "get_currentPanel");
        var ui = panel == IntPtr.Zero ? IntPtr.Zero : FairyGui.Call(panel, "get_UIObject");
        var rows = PlayerDetailTargets.Find(_snapshot.ScreenPhase, ui, NativeData.ReadObject,
            node => FairyGui.Get<int>(node, "get_numChildren"),
            (node, index) => FairyGui.Call(node, "GetChildAt", FairyGui.Int, index));
        var type = _snapshot.ScreenPhase == "방" ? "UIRoomPlayer_Com_PlayerLabel" : "UIRoomHero_Com_Player";
        foreach (var node in rows)
        {
            try
            {
                if (!FairyGui.Get<bool>(node, "get_visible") || !FairyGui.Get<bool>(node, "get_onStage")) continue;
                RefreshDetailLabel(node, type, ui, found);
            }
            catch (Exception ex)
            {
                LogRuntimeWarning("player-details", "Player details unavailable: " + ex.GetType().Name + ": " + ex.Message);
            }
        }
        var summary = _snapshot.ScreenPhase + "; rows=" + rows.Count + "; rendered=" + found.Count;
        if (_detailsSummary != summary)
        {
            _detailsSummary = summary;
            _log?.LogInfo("Player details: " + summary + ".");
        }
        foreach (var key in new List<IntPtr>(DetailLabels.Keys))
        {
            if (found.Contains(key)) continue;
            var label = DetailLabels[key];
            DetailLabels.Remove(key);
            try { label.Dispose(); } catch { }
        }
    }

    private static void RefreshDetailLabel(IntPtr row, string type, IntPtr ui, HashSet<IntPtr> found)
    {
        var player = NativeData.ReadObject(row, type == "UIRoomHero_Com_Player" ? "playerData" : "playerInfo");
        if (player == IntPtr.Zero || NativeData.InvokeBoolean(player, "get_IsBot")) return;
        var details = DetailsReader.Read(player);
        var label = NativeData.ReadObject(row, type == "UIRoomHero_Com_Player" ? "com_PlayerLabel" : "com_Label");
        if (label == IntPtr.Zero) return;
        var name = NativeData.ReadObject(label, "com_Name");
        if (name == IntPtr.Zero) return;
        var nameText = NativeData.ReadObject(name, "txt_PlayerName");
        if (nameText == IntPtr.Zero) return;
        try { details = details.WithProfile(ProfileQuery.Read(details.PlayerId)); }
        catch (Exception ex)
        {
            LogRuntimeWarning("profile-query", "Automatic profile query unavailable: " + ex.GetType().Name + ": " + ex.Message);
        }
        if (DetailLabels.TryGetValue(name, out var decoration)
            && (decoration.Host.Pointer != ui || decoration.Container.IsDisposed))
        {
            DetailLabels.Remove(name);
            decoration.Dispose();
            decoration = null;
        }
        if (decoration == null)
        {
            decoration = new DetailLabel(ui);
            DetailLabels.Add(name, decoration);
        }
        found.Add(name);
        // Copy the game's nickname font, including its Korean font mapping.
        var format = FairyGui.Call(nameText, "get_textFormat");
        var font = IL2CPP.Il2CppStringToManaged(FairyGui.ReadObject(format, "font")) ?? string.Empty;
        decoration.Text.SetTextStyle(font, 17, new Color32(245, 248, 255, 255),
            FguiAlign.Left, FguiVerticalAlign.Middle, false, FguiAutoSize.Both);
        decoration.Text.SetText(details.Caption);
        // Keep the text left-aligned, measure its actual glyph width, and grow
        // the box to the left of a fixed right edge beside the card's chat icon.
        // Its top slightly overlaps the card, matching the user's reference.
        var selection = type == "UIRoomHero_Com_Player";
        var global = FairyGui.Unbox<Vector2>(FairyGui.Call(label, "LocalToGlobal", FairyGui.Vector2Type,
            new Vector2(selection ? 0f : FairyGui.Get<float>(label, "get_width") - 88f, 0)));
        var position = FairyGui.Unbox<Vector2>(FairyGui.Call(ui, "GlobalToLocal", FairyGui.Vector2Type, global));
        var width = FairyGui.Get<float>(ui, "get_width");
        var height = FairyGui.Get<float>(ui, "get_height");
        var boxWidth = Math.Min(width, Math.Max(64, (float)Math.Ceiling(FairyGui.Get<float>(decoration.Text.Pointer, "get_textWidth")) + 12));
        var boxHeight = Math.Max(48, (float)Math.Ceiling(FairyGui.Get<float>(decoration.Text.Pointer, "get_textHeight")) + 4);
        decoration.SetBounds(Math.Clamp(selection ? position.x : position.x - boxWidth, 0, Math.Max(0, width - boxWidth)),
            Math.Clamp(position.y - 12, 0, Math.Max(0, height - boxHeight)), boxWidth, boxHeight);
        decoration.Container.SetVisible(true);
    }

    private static void ClearPlayerDetails()
    {
        foreach (var decoration in DetailLabels.Values)
            try { decoration.Dispose(); } catch { }
        DetailLabels.Clear();
        _detailsRoom = string.Empty;
        _detailsSummary = string.Empty;
        _nextDetailsAt = 0;
    }
}
