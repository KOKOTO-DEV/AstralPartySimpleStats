using System;

namespace AstralPartySimpleStats;

/// <summary>Recognize the positive action of a newly opened room-exit confirmation.</summary>
internal static class RoomExitConfirmSelection
{
    public static int PositiveScore(string? name, string? label)
    {
        var n = Normalize(name);
        var t = Normalize(label);
        if (Negative(n, t)) return 0;
        if (t is "확인" or "예" or "네" or "나가기" or "퇴장" or "방나가기" or
            "확인하고나가기" or "ok" or "yes" or "confirm" or "accept" or "leave" or
            "leaveroom" or "exit" or "exitroom" or "sure" or "确定" or "確認" or
            "确认" or "是" or "退出" or "退出房间" or "退出房間" or "はい" or "決定" or
            "退出する" or "部屋を出る") return 100;
        if (n is "btnok" or "btnyes" or "btnconfirm" or "btnsure" or "btnaccept" or
            "btnpositive" or "btnenter" or "btnagree" or "btnsubmit" or "btntrue" or
            "buttonok" or "buttonyes" or "buttonconfirm" or "buttonsure" or
            "buttonaccept" or "buttonpositive" or "btnleave" or "btnexit" or "btn1") return 60;
        return 0;
    }

    public static bool Negative(string? name, string? label)
    {
        var n = Normalize(name);
        var t = Normalize(label);
        return t is "취소" or "아니오" or "아니요" or "돌아가기" or "cancel" or "no" or
            "back" or "取消" or "否" or "いいえ" or "キャンセル" or "戻る" ||
            n is "btncancel" or "btnno" or "btnnegative" or "btnback" or
            "buttoncancel" or "buttonno" or "buttonnegative";
    }

    public static bool ExitContext(string? text)
    {
        var t = Normalize(text);
        return t.Contains("나가", StringComparison.Ordinal) ||
            t.Contains("퇴장", StringComparison.Ordinal) ||
            t.Contains("떠나", StringComparison.Ordinal) ||
            t.Contains("방닫", StringComparison.Ordinal) ||
            t.Contains("방종료", StringComparison.Ordinal) ||
            t.Contains("exit", StringComparison.Ordinal) ||
            t.Contains("leave", StringComparison.Ordinal) ||
            t.Contains("quitroom", StringComparison.Ordinal) ||
            t.Contains("退出", StringComparison.Ordinal) ||
            t.Contains("离开", StringComparison.Ordinal) ||
            t.Contains("離開", StringComparison.Ordinal) ||
            t.Contains("部屋を出", StringComparison.Ordinal) ||
            t.Contains("退室", StringComparison.Ordinal);
    }

    private static string Normalize(string? text) => (text ?? "").Trim().Replace("_", "", StringComparison.Ordinal)
        .Replace(" ", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal)
        .Replace("\r", "", StringComparison.Ordinal).ToLowerInvariant();
}
