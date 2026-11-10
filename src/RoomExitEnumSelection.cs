using System;
using System.Collections.Generic;
using System.Linq;

namespace AstralPartySimpleStats;

/// <summary>
/// Choose only a *named* enum member discovered in the current game, never
/// invent protocol enum numbers. Prefer normal leave for guests and close/
/// force-close for hosts where the protocol exposes distinct enum members.
/// </summary>
internal static class RoomExitEnumSelection
{
    internal readonly record struct Value(string Name, int Number);

    public static Value Choose(IReadOnlyList<Value> values, bool host)
    {
        if (values.Count == 0)
            throw new InvalidOperationException("ForceExitType has no readable enum constants.");

        var candidates = values.Where(v => !string.IsNullOrWhiteSpace(v.Name)
                && !string.Equals(v.Name, "value__", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (candidates.Length == 0)
            throw new InvalidOperationException("ForceExitType constants are unavailable.");

        // Match before broad substring rules; a 'NotForce' value MUST NOT be
        // mistaken for an affirmative force-exit mode.
        var best = candidates.Select(v => (Value: v, Score: Score(v.Name, host)))
            .OrderByDescending(v => v.Score)
            .ThenBy(v => v.Value.Number)
            .First();
        if (best.Score <= 0)
            throw new InvalidOperationException("ForceExitType member names cannot be safely mapped: "
                + string.Join(", ", candidates.Select(v => v.Name + "=" + v.Number)));
        return best.Value;
    }

    private static int Score(string name, bool host)
    {
        var n = name.Replace("_", "", StringComparison.Ordinal).ToLowerInvariant();
        var negativeForce = n.Contains("notforce", StringComparison.Ordinal)
            || n.Contains("noforce", StringComparison.Ordinal)
            || n.Contains("nonforce", StringComparison.Ordinal)
            || n.Contains("withoutforce", StringComparison.Ordinal);
        var unspecified = n.Contains("unspec", StringComparison.Ordinal)
            || n.Contains("unknown", StringComparison.Ordinal)
            || n.Contains("invalid", StringComparison.Ordinal);
        if (unspecified) return -100;
        if (host)
        {
            if (n.Contains("close", StringComparison.Ordinal) || n.Contains("disband", StringComparison.Ordinal)
                || n.Contains("dismiss", StringComparison.Ordinal) || n.Contains("dissolve", StringComparison.Ordinal)
                || n.Contains("destroy", StringComparison.Ordinal)) return 120;
            if (n.Contains("host", StringComparison.Ordinal) || n.Contains("master", StringComparison.Ordinal)
                || n.Contains("owner", StringComparison.Ordinal)) return 110;
            if (!negativeForce && n.Contains("force", StringComparison.Ordinal)) return 90;
            if (n.Contains("normal", StringComparison.Ordinal) || n.Contains("leave", StringComparison.Ordinal)
                || n.Contains("exit", StringComparison.Ordinal)) return 70;
            if (negativeForce || n == "none" || n == "default") return 40;
        }
        else
        {
            if (negativeForce) return 130;
            if (n.Contains("normal", StringComparison.Ordinal) || n.Contains("leave", StringComparison.Ordinal)) return 120;
            if (n.Contains("exit", StringComparison.Ordinal) && !n.Contains("force", StringComparison.Ordinal)) return 110;
            if (n == "none" || n == "default") return 90;
            if (n.Contains("force", StringComparison.Ordinal)) return 10;
        }
        return 0;
    }
}
