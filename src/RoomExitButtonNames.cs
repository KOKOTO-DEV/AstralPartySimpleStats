using System;

namespace AstralPartySimpleStats;

/// <summary>
/// Conservative selector for the game's existing room-exit UI. Returning 0
/// means "not identified"; generic close/back controls are never dispatched.
/// Only controls already wired by the game are eligible for invocation.
/// </summary>
internal static class RoomExitButtonNames
{
    public static int Score(string? name, string? title, bool isHost)
    {
        var n = Normalize(name);
        var t = Normalize(title);
        if (n.Length == 0 && t.Length == 0) return 0;
        // Stronger than a control's name: localized text explicitly says that
        // this button leaves or closes the room, not an unrelated popup.
        if (HasRoomActionText(t, isHost)) return 300;
        if (HasRoomActionText(t, !isHost)) return 220; // same back button for both roles
        // Room-specific names can be used when the button is an image without text.
        if (IsRoomExitName(n, isHost)) return 200;
        if (IsRoomExitName(n, !isHost)) return 150;
        // On the room panel only, these names are commonly used for the
        // built-in exit button (possibly an icon with no title).
        if (n is "btnexit" or "btnquit" or "btnleave" or
            "buttonexit" or "buttonquit" or "buttonleave") return 110;
        // 'back' and 'close' on their own can mean closing an unrelated dialog,
        // so an unnamed generic control is never activated.
        return 0;
    }

    private static bool HasRoomActionText(string t, bool host)
    {
        if (host)
            return t is "방닫기" or "방종료" or "방해산" or "방폭파" or
                "closeroom" or "disbandroom" or "dissolveroom" or
                "解散房间" or "解散房間" or "关闭房间" or "關閉房間" or "部屋を解散" or "部屋を閉じる";
        return t is "방나가기" or "방떠나기" or "방퇴장" or
            "leaveroom" or "exitroom" or "quitroom" or
            "退出房间" or "退出房間" or "離開房間" or "离开房间" or "部屋を退出" or "部屋を出る";
    }

    private static bool IsRoomExitName(string n, bool isHost)
    {
        if (!n.StartsWith("btn", StringComparison.Ordinal) && !n.StartsWith("button", StringComparison.Ordinal))
            return false;
        if (isHost)
            return n.EndsWith("closeroom", StringComparison.Ordinal) ||
                n.EndsWith("dismissroom", StringComparison.Ordinal) ||
                n.EndsWith("disbandroom", StringComparison.Ordinal) ||
                n.EndsWith("dissolveroom", StringComparison.Ordinal) ||
                n.EndsWith("destroyroom", StringComparison.Ordinal);
        return n.EndsWith("leaveroom", StringComparison.Ordinal) ||
            n.EndsWith("exitroom", StringComparison.Ordinal) ||
            n.EndsWith("quitroom", StringComparison.Ordinal) ||
            n.EndsWith("backtolobby", StringComparison.Ordinal) ||
            n.EndsWith("returntolobby", StringComparison.Ordinal);
    }

    private static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var chars = new char[text.Length];
        var length = 0;
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c)) chars[length++] = char.ToLowerInvariant(c);
        }
        return new string(chars, 0, length);
    }
}
