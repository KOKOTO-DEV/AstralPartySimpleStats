using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;

namespace AstralPartySimpleStats;

/// <summary>
/// Dispatches an actual FairyGUI onClick handler already installed by the game.
/// Unlike calling guessed room-controller methods, this follows the same path
/// as the game's own room exit/close UI (including confirmation dialogs).
/// </summary>
internal static class NativeRoomExitUi
{
    private const int MaxObjects = 512;
    private const int MaxDepth = 8;

    private readonly record struct Candidate(IntPtr Pointer, string Name, string Title, int Score);

    public static bool TryInvoke(bool host, out string method, out string diagnosis)
    {
        method = string.Empty;
        diagnosis = string.Empty;
        var uiManager = FairyGui.ReadStaticObject(FairyGui.Class("UI", "UIManager"), "_inst");
        var panel = uiManager == IntPtr.Zero ? IntPtr.Zero : FairyGui.Call(uiManager, "get_currentPanel");
        var panelType = FairyGui.ClassName(panel);
        if (panelType is not ("RoomWaitPanel" or "RoomHeroPanel"))
        {
            diagnosis = "not in a supported room panel (" + panelType + ")";
            return false;
        }
        var root = FairyGui.Call(panel, "get_UIObject");
        if (root == IntPtr.Zero)
        {
            diagnosis = "room UI is not available";
            return false;
        }
        var visited = new HashSet<IntPtr>();
        var pending = new Queue<(IntPtr node, int depth)>();
        var candidates = new List<Candidate>();
        var sampledNames = new List<string>();
        pending.Enqueue((root, 0));
        while (pending.Count > 0 && visited.Count < MaxObjects)
        {
            var (node, depth) = pending.Dequeue();
            if (node == IntPtr.Zero || !visited.Add(node)) continue;
            if (!TryGetBool(node, "get_visible", true)) continue;
            var name = TryGetString(node, "get_name");
            var title = TryGetString(node, "get_title");
            // Custom FairyGUI buttons may expose their label as a child text
            // field instead of the standard GButton.title property.
            if (title.Length == 0) title = TryGetString(node, "get_text");
            if (title.Length == 0 && LooksLikeButton(name))
                title = TryChildText(node);
            var score = RoomExitButtonNames.Score(name, title, host);
            // Older room-wait layouts use an icon-only Back/Close control.
            // Use it only as a last-resort candidate on the actual room panel,
            // never in arbitrary popups or the character-selection screen.
            if (score == 0 && panelType == "RoomWaitPanel" && depth <= 3)
            {
                var shortName = name.Replace("_", string.Empty).ToLowerInvariant();
                if (shortName is "btnback" or "btnreturn") score = 40;
                if (host && shortName == "btnclose") score = 40;
            }
            if (score > 0 && TryListener(node, out _))
                candidates.Add(new Candidate(node, name, title, score));
            if (sampledNames.Count < 60 && (name.StartsWith("btn", StringComparison.OrdinalIgnoreCase)
                || name.IndexOf("exit", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("leave", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("close", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("back", StringComparison.OrdinalIgnoreCase) >= 0))
                sampledNames.Add(name);
            if (depth >= MaxDepth) continue;
            int count;
            try { count = FairyGui.Get<int>(node, "get_numChildren"); }
            catch (MissingMethodException) { continue; } // Not a GComponent
            catch (InvalidOperationException) { continue; }
            for (var i = 0; i < Math.Min(count, 128) && pending.Count + visited.Count < MaxObjects * 2; i++)
            {
                try
                {
                    var child = FairyGui.Call(node, "GetChildAt", FairyGui.Int, i);
                    if (child != IntPtr.Zero) pending.Enqueue((child, depth + 1));
                }
                catch (MissingMethodException) { break; }
            }
        }
        diagnosis = panelType + " scanned=" + visited.Count + " candidateButtons=[" +
            string.Join(",", sampledNames) + "]";
        if (candidates.Count == 0) return false;
        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
        var winner = candidates[0];
        if (candidates.Count > 1 && candidates[1].Score == winner.Score)
        {
            diagnosis += "; ambiguous exit targets=" + string.Join(",", candidates.ConvertAll(c => c.Name));
            return false;
        }
        if (!TryListener(winner.Pointer, out var listener))
        {
            diagnosis += "; handler disappeared=" + winner.Name;
            return false;
        }
        // This invokes the game's own registered handler. NativeRoomLeaveFlow
        // immediately accepts only the resulting exit confirmation, when found.
        FairyGui.Call(listener, "Call");
        method = panelType + "." + winner.Name + ".onClick.Call()";
        return true;
    }

    private static bool LooksLikeButton(string name) =>
        name.StartsWith("btn", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("button", StringComparison.OrdinalIgnoreCase);

    private static string TryChildText(IntPtr parent)
    {
        int count;
        try { count = FairyGui.Get<int>(parent, "get_numChildren"); }
        catch (MissingMethodException) { return string.Empty; }
        catch (InvalidOperationException) { return string.Empty; }
        for (var i = 0; i < Math.Min(count, 24); i++)
        {
            try
            {
                var child = FairyGui.Call(parent, "GetChildAt", FairyGui.Int, i);
                if (child == IntPtr.Zero) continue;
                var label = TryGetString(child, "get_text");
                if (label.Length != 0) return label;
                label = TryGetString(child, "get_title");
                if (label.Length != 0) return label;
            }
            catch (MissingMethodException) { break; }
        }
        return string.Empty;
    }

    private static bool TryListener(IntPtr button, out IntPtr listener)
    {
        listener = IntPtr.Zero;
        try
        {
            listener = FairyGui.Call(button, "get_onClick");
            return listener != IntPtr.Zero && !FairyGui.Get<bool>(listener, "get_isEmpty");
        }
        catch (MissingMethodException) { return false; }
        catch (InvalidOperationException) { return false; }
    }

    private static bool TryGetBool(IntPtr target, string method, bool fallback)
    {
        try { return FairyGui.Get<bool>(target, method); }
        catch (MissingMethodException) { return fallback; }
        catch (InvalidOperationException) { return fallback; }
    }

    private static string TryGetString(IntPtr target, string method)
    {
        try { return FairyGui.GetString(target, method); }
        catch (MissingMethodException) { return string.Empty; }
        catch (InvalidOperationException) { return string.Empty; }
    }
}
