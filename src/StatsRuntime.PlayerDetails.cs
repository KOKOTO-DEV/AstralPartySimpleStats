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
        public readonly FguiNode Left;
        public readonly FguiNode Right;
        public readonly FguiNode FriendButton;
        public readonly FguiNode BlockButton;
        public readonly FguiNode KickButton;
        public readonly FguiNode LeaveButton;
        public readonly FguiNode FriendCaption;
        private readonly FguiNode _measure;
        public int ActionState=-1;
        public bool Expanded;
        public bool CanExpand;
        public long PlayerId;
        public float HeaderHeight;
        public float LineHeight;
        public FguiNode Measure => _measure;
        public void MeasureManner(string font, string manner)
        {
            _measure.SetTextStyle(font, 17, new Color32(245,248,255,255),
                FguiAlign.Left, FguiVerticalAlign.Middle, true, FguiAutoSize.Both);
            _measure.SetText(manner);
        }
        public DetailLabel(IntPtr host)
        {
            Host = FguiNode.Retain(host);
            try
            {
                Container = FguiNode.Component(380, 48);
                Container.SetTouchable(true);
                Container.SetOpaque(true);
                Container.SetSortingOrder(10);
                _background = FguiNode.Graph(380, 48);
                _background.SetTouchable(true);
                _background.Fill(new Color32(20, 24, 32, 225), 4);
                Container.AddChild(_background);
                Text = FguiNode.Text(368, 44);
                Text.SetTouchable(true);
                Text.SetXY(6, 2);
                Container.AddChild(Text);
                Left = FguiNode.Text(200,140);
                Right = FguiNode.Text(200,140);
                Container.AddChild(Left); Container.AddChild(Right);
                Left.SetVisible(false); Right.SetVisible(false);
                FriendCaption=FguiNode.Text(44,22);
                FriendCaption.SetTouchable(false);
                FriendButton=FguiNode.Text(58,22); BlockButton=FguiNode.Text(58,22);
                KickButton=FguiNode.Text(80,22); LeaveButton=FguiNode.Text(96,22);
                _measure=FguiNode.Text(200,22);
                FriendButton.SetTouchable(true); BlockButton.SetTouchable(true);
                KickButton.SetTouchable(true); LeaveButton.SetTouchable(true);
                _measure.SetTouchable(false); _measure.SetVisible(false);
                Container.AddChild(FriendCaption);
                Container.AddChild(FriendButton); Container.AddChild(BlockButton);
                Container.AddChild(KickButton); Container.AddChild(LeaveButton);
                Container.AddChild(_measure);
                FriendCaption.SetVisible(false); FriendButton.SetVisible(false);
                BlockButton.SetVisible(false); KickButton.SetVisible(false); LeaveButton.SetVisible(false);
                Host.AddChild(Container);
            }
            catch
            {
                // Children can fail before attachment; dispose all owned nodes.
                try { Text?.Dispose(); } catch { }
                try { Left?.Dispose(); } catch { }
                try { Right?.Dispose(); } catch { }
                try { FriendButton?.Dispose(); } catch { }
                try { BlockButton?.Dispose(); } catch { }
                try { KickButton?.Dispose(); } catch { }
                try { LeaveButton?.Dispose(); } catch { }
                try { FriendCaption?.Dispose(); } catch { }
                try { _measure?.Dispose(); } catch { }
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
                Left.ReleaseHandle(); Right.ReleaseHandle();
                FriendButton.ReleaseHandle(); BlockButton.ReleaseHandle();
                KickButton.ReleaseHandle(); LeaveButton.ReleaseHandle();
                FriendCaption.ReleaseHandle(); _measure.ReleaseHandle();
                _background.ReleaseHandle();
                Host.ReleaseHandle();
            }
        }

        public void SetBounds(float x, float y, float width, float height)
        {
            // Expanded details must cover later players' headers instead of
            // competing with their identical sorting order and translucent fill.
            Container.SetSortingOrder(Expanded ? 400 : 10);
            _background.Fill(new Color32(20, 24, 32, Expanded ? (byte)255 : (byte)225), 4);
            Container.SetXY(x, y);
            Container.SetSize(width, height);
            _background.SetSize(width, height);
        }
    }

    private static void RefreshPlayerDetails()
    {
        if (!ShowPlayerDetails || _manualHidden || !_gameState.Available || !_gameState.InformationReady
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
        if (VerboseLogging)
        {
            var summary = _snapshot.ScreenPhase + "; rows=" + rows.Count + "; rendered=" + found.Count;
            if (_detailsSummary != summary)
            {
                _detailsSummary = summary;
                LogDiagnostic("Player details: " + summary + ".");
            }
        }
        else _detailsSummary = string.Empty;
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
        try { details = details.WithProfile(ProfileQuery.Read(details.PlayerId,_gameState.RoomId)); }
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
        if (decoration.PlayerId != details.PlayerId) decoration.Expanded = false;
        decoration.PlayerId = details.PlayerId;
        decoration.CanExpand = details.Recent?.Rows.Count > 0;
        if (!decoration.CanExpand) decoration.Expanded = false;
        // Copy the game's nickname font, including its Korean font mapping.
        var format = FairyGui.Call(nameText, "get_textFormat");
        var font = IL2CPP.Il2CppStringToManaged(FairyGui.ReadObject(format, "font")) ?? string.Empty;
        _uiFont = font;
        decoration.Text.SetTextStyle(font, 17, new Color32(245, 248, 255, 255),
            FguiAlign.Left, FguiVerticalAlign.Middle, false, FguiAutoSize.Both);
        decoration.Text.SetText(details.Caption + "\n" + (details.Recent?.Summary ?? "최근: 확인 불가")
            + (decoration.CanExpand ? (decoration.Expanded ? " ▴" : " ▾") : string.Empty));
        decoration.LineHeight=FairyGui.Get<float>(decoration.Text.Pointer,"get_textHeight")/3;
        try { decoration.ActionState=ProfileQuery.ActionState(details.PlayerId); }
        catch(Exception ex) { decoration.ActionState=-1; LogRuntimeWarning("friend-state",ex.Message); }
        var actions = ProfileActionLayout.FromState(decoration.ActionState, RoomKickPolicy.AllowsScreen(_snapshot.ScreenPhase));
        decoration.FriendCaption.SetVisible(actions.ShowFriends);
        decoration.FriendButton.SetVisible(actions.ShowFriends);
        decoration.BlockButton.SetVisible(actions.ShowFriends);
        decoration.KickButton.SetVisible(actions.ShowKick);
        decoration.LeaveButton.SetVisible(actions.ShowLeave);

        var actionRight = 0f;
        if (actions.ShowFriends || actions.ShowLeave)
        {
            // Measure the *actual game font* on the manner line. Do not rely on
            // fixed offsets: "확인 불가" is wider than a numeric credit score.
            var mannerLine = details.Caption[(details.Caption.IndexOf('\n') + 1)..];
            decoration.MeasureManner(font, mannerLine);
            var mannerWidth = FairyGui.Get<float>(decoration.Measure.Pointer, "get_textWidth");
            // Leave is spaced a little farther from the manner score; friend actions retain their original gap.
            var actionX = 6f + mannerWidth + (actions.ShowLeave ? 20f : 10f);
            var actionY = 2f + decoration.LineHeight;
            var actionHeight = Math.Max(16f, decoration.LineHeight - 2f);
            if (actions.ShowLeave)
            {
                decoration.LeaveButton.SetTextStyle(font, 15, new Color32(255, 188, 157, 255),
                    FguiAlign.Center, FguiVerticalAlign.Middle, true, FguiAutoSize.Shrink);
                decoration.LeaveButton.SetText(actions.IsHost ? "[방 닫기]" : "[방 나가기]");
                decoration.LeaveButton.SetSize(96, actionHeight);
                decoration.LeaveButton.SetXY(actionX, actionY);
                actionRight = actionX + 96;
            }
            else
            {
                decoration.FriendCaption.SetTextStyle(font, 15, new Color32(220, 229, 242, 255),
                    FguiAlign.Left, FguiVerticalAlign.Middle, true, FguiAutoSize.Shrink);
                decoration.FriendCaption.SetText("친구:");
                decoration.FriendCaption.SetSize(43, actionHeight);
                decoration.FriendCaption.SetXY(actionX, actionY);
                actionX += 44;
                decoration.FriendButton.SetTextStyle(font, 15,
                    actions.CanRegister ? new Color32(140, 215, 255, 255) : new Color32(119, 128, 139, 255),
                    FguiAlign.Center, FguiVerticalAlign.Middle, true, FguiAutoSize.Shrink);
                decoration.FriendButton.SetText("[등록]");
                decoration.FriendButton.SetTouchable(actions.CanRegister);
                decoration.FriendButton.SetSize(58, actionHeight);
                decoration.FriendButton.SetXY(actionX, actionY);
                actionX += 61;
                decoration.BlockButton.SetTextStyle(font, 15,
                    actions.CanBlock ? new Color32(255, 180, 180, 255) : new Color32(119, 128, 139, 255),
                    FguiAlign.Center, FguiVerticalAlign.Middle, true, FguiAutoSize.Shrink);
                decoration.BlockButton.SetText("[차단]");
                decoration.BlockButton.SetTouchable(actions.CanBlock);
                decoration.BlockButton.SetSize(58, actionHeight);
                decoration.BlockButton.SetXY(actionX, actionY);
                actionX += 61;
                if (actions.ShowKick)
                {
                    decoration.KickButton.SetTextStyle(font, 15, new Color32(255, 188, 157, 255),
                        FguiAlign.Center, FguiVerticalAlign.Middle, true, FguiAutoSize.Shrink);
                    decoration.KickButton.SetText("[내보내기]");
                    decoration.KickButton.SetSize(80, actionHeight);
                    decoration.KickButton.SetXY(actionX, actionY);
                    actionX += 80;
                }
                actionRight = actionX;
            }
        }
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
        if (actionRight > 0) boxWidth = Math.Min(width, Math.Max(boxWidth, actionRight + 8));
        var boxHeight = Math.Max(68, (float)Math.Ceiling(FairyGui.Get<float>(decoration.Text.Pointer, "get_textHeight")) + 4);
        decoration.HeaderHeight = boxHeight;
        decoration.Left.SetVisible(decoration.Expanded); decoration.Right.SetVisible(decoration.Expanded);
        if (decoration.Expanded && details.Recent != null)
        {
            foreach (var column in new[] { decoration.Left, decoration.Right })
                column.SetTextStyle(font,17,new Color32(245,248,255,255),FguiAlign.Left,FguiVerticalAlign.Top,false,FguiAutoSize.Both);
            decoration.Left.SetText("최신순 01~05\n"+details.Recent.Column(0,_useCharacterNames));
            decoration.Right.SetText("06~10\n"+details.Recent.Column(5,_useCharacterNames));
            var leftWidth=FairyGui.Get<float>(decoration.Left.Pointer,"get_textWidth");
            var rightWidth=FairyGui.Get<float>(decoration.Right.Pointer,"get_textWidth");
            decoration.Left.SetXY(6,boxHeight+4); decoration.Right.SetXY(6+leftWidth+24,boxHeight+4);
            boxWidth=Math.Min(width,Math.Max(boxWidth,leftWidth+rightWidth+36));
            boxHeight+=Math.Max(FairyGui.Get<float>(decoration.Left.Pointer,"get_textHeight"),FairyGui.Get<float>(decoration.Right.Pointer,"get_textHeight"))+8;
        }
        decoration.SetBounds(Math.Clamp(selection ? position.x : position.x - boxWidth, 0, Math.Max(0, width - boxWidth)),
            Math.Clamp(position.y - 52, 0, Math.Max(0, height - boxHeight)), boxWidth, boxHeight);
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

    public static void HandleRecentInput()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        try
        {
            var root=FguiNode.Root;
            if (root==null) return;
            if (_toggle != null && !_toggle.IsDisposed && FairyGui.Get<bool>(_toggle.Pointer,"get_visible") &&
                ContainsPointer(_toggle, out var toggleLocal))
            {
                if(toggleLocal.y>=28)
                {
                    _useCharacterNames=!_useCharacterNames;
                    PlayerPrefs.SetInt("AstralPartySimpleStats.CharacterNames",_useCharacterNames?1:0);
                }
                else
                {
                    _manualHidden=!_manualHidden;
                    PlayerPrefs.SetInt("AstralPartySimpleStats.InfoHidden",_manualHidden?1:0);
                }
                PlayerPrefs.Save();
                _nextDetailsAt=0; _nextRead=0;
                return;
            }
            DetailLabel? hit=null;
            foreach(var label in DetailLabels.Values)
            {
                if(label.Expanded && !label.Container.IsDisposed && ContainsPointer(label.Container,out _)) {hit=label;break;}
            }
            foreach(var label in DetailLabels.Values)
            {
                if(hit!=null) break;
                if(!label.Container.IsDisposed && ContainsPointer(label.Container,out _)) {hit=label;break;}
            }
            var toggle=false;
            if (hit != null)
            {
                var actions = ProfileActionLayout.FromState(hit.ActionState,
                    RoomKickPolicy.AllowsScreen(_snapshot.ScreenPhase)
                    && RoomKickPolicy.AllowsPanel(NativeRoomNavigation.CurrentPanel()));
                if (actions.ShowLeave && ContainsPointer(hit.LeaveButton, out _))
                {
                    LogDiagnostic("Room leave requested from self profile (0.4.0).");
                    try
                    {
                        var isHost = (hit.ActionState & 8) != 0;
                        if (isHost)
                        {
                            // The host's direct RPC already closes the room and its panel.
                            var called = ProfileQuery.RequestAction(hit.PlayerId, "leave");
                            LogDiagnostic("Room leave API invoked: " + (called.Length == 0 ? "not dispatched" : called)
                                + ". Awaiting game/server response.");
                        }
                        else
                        {
                            // Both waiting-room and character-selection screens
                            // use the same verified native exit RPC. The game
                            // has no leave button in its character-selection UI.
                            // UI routing runs only after the room state clears.
                            var called = NativeRoomLeaveFlow.Start(hit.PlayerId, _gameState.RoomId);
                            LogDiagnostic("Room leave direct API initiated: " + called);
                        }
                    }
                    catch (Exception ex)
                    {
                        _log?.LogWarning("Room leave failed: " + ex.GetType().Name + ": " + ex.Message);
                    }
                    _nextDetailsAt = 0; _nextRead = 0; return;
                }
                if (actions.ShowFriends)
                {
                    if (ContainsPointer(hit.FriendButton, out _))
                    {
                        if (actions.CanRegister) ProfileQuery.RequestAction(hit.PlayerId, "friend");
                        _nextDetailsAt = 0; _nextRead = 0; return;
                    }
                    if (ContainsPointer(hit.BlockButton, out _))
                    {
                        if (actions.CanBlock) ProfileQuery.RequestAction(hit.PlayerId, "block");
                        _nextDetailsAt = 0; _nextRead = 0; return;
                    }
                    if (actions.ShowKick && ContainsPointer(hit.KickButton, out _))
                    {
                        ProfileQuery.RequestAction(hit.PlayerId, "kick");
                        _nextDetailsAt = 0; _nextRead = 0; return;
                    }
                }
            }
            if(hit!=null && hit.CanExpand)
            {
                var mouse=Input.mousePosition;
                var local=FairyGui.Unbox<Vector2>(FairyGui.Call(hit.Container.Pointer,"GlobalToLocal",FairyGui.Vector2Type,new Vector2(mouse.x,Screen.height-mouse.y)));
                toggle=local.y>=2+2*hit.LineHeight && local.y<2+3*hit.LineHeight;
            }
            foreach(var label in DetailLabels.Values)
                label.Expanded=label==hit ? (toggle ? !label.Expanded : label.Expanded) : false;
            _nextDetailsAt=0; _nextRead=0;
        }
        catch(Exception ex) {LogRuntimeWarning("recent-input","Recent history input: "+ex.Message);}
    }
}
