using System;
using UnityEngine;

namespace AstralPartySimpleStats;

internal static partial class StatsRuntime
{
    private static FguiNode? _toggle, _toggleBackground, _toggleText, _nameText;
    private static bool _manualHidden;
    private static bool _useCharacterNames;
    private static string _uiFont=string.Empty;

    private static bool ContainsPointer(FguiNode node,out Vector2 local)
    {
        var mouse=Input.mousePosition;
        local=FairyGui.Unbox<Vector2>(FairyGui.Call(node.Pointer,"GlobalToLocal",FairyGui.Vector2Type,
            new Vector2(mouse.x,Screen.height-mouse.y)));
        return local.x>=0 && local.y>=0 && local.x<=node.Width && local.y<=node.Height;
    }

    private static void RefreshVisibilityToggle()
    {
        var visible=ShowPlayerDetails && _gameState.Available && _snapshot.ScreenPhase is "방" or "캐릭터 선택";
        if (!visible) { _toggle?.SetVisible(false); return; }
        var root=FguiNode.Root;
        if(root==null) return;
        if(_toggle!=null && _toggle.IsDisposed) DisposeVisibilityToggle();
        if(_toggle==null)
        {
            try
            {
                _toggle=FguiNode.Component(148,56);
                _toggle.SetTouchable(true); _toggle.SetOpaque(true); _toggle.SetSortingOrder(500);
                _toggleBackground=FguiNode.Graph(148,56);
                _toggleBackground.Fill(new Color32(24,28,38,235),6);
                _toggleBackground.SetTouchable(true);
                _toggle.AddChild(_toggleBackground);
                _toggleText=FguiNode.Text(136,24); _toggleText.SetXY(6,1);
                _toggleText.SetTouchable(false); _toggle.AddChild(_toggleText);
                _nameText=FguiNode.Text(136,24); _nameText.SetXY(6,29);
                _nameText.SetTouchable(false); _toggle.AddChild(_nameText);
                root.AddChild(_toggle);
            }
            catch { DisposeVisibilityToggle();throw; }
        }
        if(!_toggle.IsChildOf(root)) root.AddChild(_toggle);
        if(_uiFont.Length==0)
        {
            var manager=FairyGui.ReadStaticObject(FairyGui.Class("UI","UIManager"),"_inst");
            var panel=FairyGui.Call(manager,"get_currentPanel");
            var ui=FairyGui.Call(panel,"get_UIObject");
            var rows=PlayerDetailTargets.Find(_snapshot.ScreenPhase,ui,NativeData.ReadObject,
                n=>FairyGui.Get<int>(n,"get_numChildren"),(n,i)=>FairyGui.Call(n,"GetChildAt",FairyGui.Int,i));
            if(rows.Count>0)
            {
                var card=NativeData.ReadObject(rows[0],_snapshot.ScreenPhase=="방"?"com_Label":"com_PlayerLabel");
                var name=NativeData.ReadObject(card,"com_Name");
                var text=NativeData.ReadObject(name,"txt_PlayerName");
                if(text!=IntPtr.Zero)
                {
                    var format=FairyGui.Call(text,"get_textFormat");
                    _uiFont=Il2CppInterop.Runtime.IL2CPP.Il2CppStringToManaged(FairyGui.ReadObject(format,"font"))??string.Empty;
                }
            }
        }
        _toggleText!.SetTextStyle(_uiFont,15,new Color32(245,248,255,255),FguiAlign.Center,FguiVerticalAlign.Middle,true,FguiAutoSize.Shrink);
        _toggleText.SetText(_manualHidden?"유저정보 표시":"유저정보 숨김");
        _nameText!.SetTextStyle(_uiFont,15,new Color32(245,248,255,255),FguiAlign.Center,FguiVerticalAlign.Middle,true,FguiAutoSize.Shrink);
        _nameText.SetText(_useCharacterNames?"캐릭터: 이름":"캐릭터: 이명");
        _toggle.SetXY(Math.Max(0,root.Width-160),12);
        _toggle.SetVisible(true);
    }

    private static void DisposeVisibilityToggle()
    {
        try { _toggle?.Dispose(); }
        finally
        {
            _toggleText?.ReleaseHandle();_toggleBackground?.ReleaseHandle();
            _nameText?.ReleaseHandle();_nameText=null;
            _toggle=null;_toggleText=null;_toggleBackground=null;
        }
    }
}
