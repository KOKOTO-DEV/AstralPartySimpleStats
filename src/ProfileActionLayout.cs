namespace AstralPartySimpleStats;

// Status bits come from the embedded game-domain profile bridge:
// 1 = local player, 2 = friend, 4 = blocked, 8 = local player owns the room.
internal readonly record struct ProfileActionLayout(bool ShowLeave, bool ShowFriends, bool ShowKick,
    bool CanRegister, bool CanBlock, bool IsHost)
{
    public static ProfileActionLayout FromState(int state, bool allowKick)
    {
        if (state < 0) return default;
        if ((state & 1) != 0) return new ProfileActionLayout(true, false, false, false, false, (state & 8) != 0);
        return new ProfileActionLayout(false, true, allowKick && (state & 8) != 0,
            (state & 6) == 0, (state & 4) == 0, (state & 8) != 0);
    }
}


// RoomHeroPanel can display player profiles, but kicking during hero selection
// is intentionally disabled. Unknown/transitional panels fail closed.
internal static class RoomKickPolicy
{
    public static bool AllowsScreen(string screenPhase) => screenPhase == "방";
    public static bool AllowsPanel(string panelName) => panelName == "RoomWaitPanel";
}
