using System;
using System.Collections.Generic;

namespace AstralPartySimpleStats;

internal static class PlayerDetailTargets
{
    // Use the very same list/slots as RoomWaitPanel and RoomHeroPanel, rather
    // than depending on GRoot visibility, HybridCLR assignability, or tree depth.
    public static IReadOnlyList<IntPtr> Find(string phase, IntPtr panel,
        Func<IntPtr, string, IntPtr> field, Func<IntPtr, int> count, Func<IntPtr, int, IntPtr> child)
    {
        var result = new List<IntPtr>(4);
        if (panel == IntPtr.Zero) return result;
        if (phase == "방")
        {
            var players = field(panel, "com_Player");
            if (players == IntPtr.Zero) return result;
            var list = field(players, "list_playerWait");
            if (list == IntPtr.Zero) return result;
            for (var index = 0; index < Math.Min(count(list), 16); index++)
            {
                var row = child(list, index);
                if (row != IntPtr.Zero) result.Add(row);
            }
        }
        else if (phase == "캐릭터 선택")
        {
            for (var slot = 1; slot <= 4; slot++)
            {
                var row = field(panel, "com_player_" + slot);
                if (row != IntPtr.Zero) result.Add(row);
            }
        }
        return result;
    }
}
