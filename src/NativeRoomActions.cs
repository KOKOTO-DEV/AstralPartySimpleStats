using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;

namespace AstralPartySimpleStats;

/// <summary>
/// Uses game-owned room operations. The 0.3.0 prebuilt profile bridge has no
/// leave/kick entry points. Exit uses the actual roomLogic.RequestExitRoomC2S
/// method identified in the user's runtime log, with its exact ForceExitType
/// enum parameter. This deliberately bypasses the game's confirmation UI.
/// No synthetic RPC IDs or guessed protobuf payloads are generated.
/// </summary>
internal static class NativeRoomActions
{
    private static readonly NativeGameData Data = new();

    // Observed in BepInEx/LogOutput.log (2026-11-10):
    // roomLogic.RequestExitRoomC2S(party.protocol.ExitRoomC2S.Types.ForceExitType)
    private const string ExitMethod = "RequestExitRoomC2S";
    private const string ExitEnumSignature = "party.protocol.ExitRoomC2S.Types.ForceExitType";

    private static readonly string[] KickMethods =
    {
        "RequestKickRoomPlayerC2S", "RequestKickPlayerC2S", "RequestKickOutPlayerC2S",
        "RequestRoomKickC2S", "RequestRemoveRoomPlayerC2S",
        "RequestKickRoomPlayer", "RequestKickPlayer", "KickRoomPlayer",
        "KickPlayer", "KickOutPlayer"
    };

    public static string Invoke(string action, long expectedRoomId, long targetId)
    {
        if (action != "leave" && action != "kick") throw new ArgumentException("Unsupported room action: " + action);
        if (expectedRoomId <= 0 || targetId <= 0) throw new InvalidOperationException("Room/player ID is unavailable.");
        var klass = Data.FindClass("AstralParty.Runtime", "GameLogic", "GameLogicManager");
        if (klass == IntPtr.Zero) throw new TypeLoadException("GameLogicManager");
        var game = Data.ReadStaticObject(klass, "_inst");
        if (game == IntPtr.Zero) throw new InvalidOperationException("GameLogicManager is not initialized.");
        var account = ReadAccount(game);
        if (account == IntPtr.Zero) throw new InvalidOperationException("Account is unavailable.");
        var selfId = Data.InvokeInt64(account, "GetPlayerID");
        if (selfId <= 0) throw new InvalidOperationException("Local account ID is unavailable.");
        var roomLogic = Data.ReadObject(game, "<room>k__BackingField");
        var controller = Data.ReadObject(roomLogic, "roomController");
        if (controller == IntPtr.Zero || Data.ReadInt32(controller, "roomStateType") == 0)
            throw new InvalidOperationException("Room controller is no longer active.");
        var room = Data.ReadObject(controller, "_LocalRoom");
        if (room == IntPtr.Zero || Data.InvokeInt64(room, "get_Id") != expectedRoomId)
            throw new InvalidOperationException("Current room changed before the action was sent.");

        if (action == "kick")
        {
            // Defense in depth: permission is checked again immediately before
            // the game RPC, not solely when the profile button is rendered.
            if (!RoomKickPolicy.AllowsPanel(NativeRoomNavigation.CurrentPanel()))
                throw new InvalidOperationException("Removing players is disabled outside the room waiting panel.");
            if (targetId == selfId || Data.InvokeInt64(room, "get_MasterId") != selfId)
                throw new UnauthorizedAccessException("Only the host may remove another player.");
            // Keep the already-working kick path unchanged: one Int64 player ID.
            if (TryCall(controller, KickMethods, targetId, out var used)) return "roomController." + used;
            if (TryCall(roomLogic, KickMethods, targetId, out used)) return "roomLogic." + used;
            throw new MissingMethodException("No matching kick API found. " + Diagnose(controller, roomLogic, game));
        }

        if (targetId != selfId) throw new UnauthorizedAccessException("Leave is only allowed for the local account.");
        var isHost = Data.InvokeInt64(room, "get_MasterId") == selfId;

        // Direct RPC: no original FairyGUI button click or confirmation dialog.
        // Resolve a named ForceExitType enum member from the loaded game's
        // metadata rather than assuming numeric enum values for this build.
        var enumValue = NativeRoomExitEnum.SelectExitMode(roomLogic, ExitMethod,
            ExitEnumSignature, isHost, out var enumName, out var variants);
        FairyGui.Call(roomLogic, ExitMethod, ExitEnumSignature, enumValue);
        return "roomLogic." + ExitMethod + "(" + ExitEnumSignature + "." + enumName
            + "=" + enumValue + ") (host=" + isHost + "; direct/no confirmation; enum=" + variants + ")";
    }

    private static IntPtr ReadAccount(IntPtr game)
    {
        try { return Data.Invoke(game, "get_account"); }
        catch (MissingMethodException) { return Data.ReadObject(game, "<account>k__BackingField"); }
    }

    private static bool TryCall(IntPtr target, string[] names, long? arg, out string method)
    {
        method = string.Empty;
        if (target == IntPtr.Zero) return false;
        var signature = arg.HasValue ? "System.Int64" : string.Empty;
        foreach (var name in names)
        {
            try { FairyGui.Method(IL2CPP.il2cpp_object_get_class(target), name, signature); }
            catch (MissingMethodException) { continue; }
            if (arg.HasValue) FairyGui.Call(target, name, signature, arg.Value);
            else FairyGui.Call(target, name);
            method = name + "(" + signature + ")";
            return true;
        }
        return false;
    }

    // Discovery is read-only and runs only if the requested leave method is
    // missing. The method names/signatures allow an exact next-build fix.
    private static string Diagnose(IntPtr controller, IntPtr logic, IntPtr game)
    {
        var descriptions = new List<string>();
        foreach (var (instance, source) in new[] { (controller, "roomController"), (logic, "roomLogic"), (game, "gameManager") })
        {
            if (instance == IntPtr.Zero) continue;
            var klass = IL2CPP.il2cpp_object_get_class(instance);
            var matches = new List<string>();
            for (var type = klass; type != IntPtr.Zero; type = IL2CPP.il2cpp_class_get_parent(type))
            {
                var iter = IntPtr.Zero;
                IntPtr pointer;
                while ((pointer = IL2CPP.il2cpp_class_get_methods(type, ref iter)) != IntPtr.Zero)
                {
                    var name = Marshal.PtrToStringAnsi(IL2CPP.il2cpp_method_get_name(pointer)) ?? string.Empty;
                    if (name.IndexOf("leave", StringComparison.OrdinalIgnoreCase) < 0
                        && name.IndexOf("exit", StringComparison.OrdinalIgnoreCase) < 0
                        && name.IndexOf("quit", StringComparison.OrdinalIgnoreCase) < 0
                        && name.IndexOf("disband", StringComparison.OrdinalIgnoreCase) < 0
                        && name.IndexOf("dissolve", StringComparison.OrdinalIgnoreCase) < 0
                        && name.IndexOf("close", StringComparison.OrdinalIgnoreCase) < 0
                        && name.IndexOf("dismiss", StringComparison.OrdinalIgnoreCase) < 0
                        && name.IndexOf("back", StringComparison.OrdinalIgnoreCase) < 0
                        && name.IndexOf("kick", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var length = IL2CPP.il2cpp_method_get_param_count(pointer);
                    var args = new List<string>();
                    for (uint i = 0; i < length; i++)
                    {
                        var typeName = IL2CPP.il2cpp_type_get_name(IL2CPP.il2cpp_method_get_param(pointer, i));
                        try { args.Add(Marshal.PtrToStringAnsi(typeName) ?? "?"); }
                        finally { IL2CPP.il2cpp_free(typeName); }
                    }
                    if (matches.Count < 24) matches.Add(name + "(" + string.Join(",", args) + ")");
                }
            }
            descriptions.Add(source + " candidates: " + (matches.Count == 0 ? "none" : string.Join("; ", matches)));
        }
        return string.Join(" | ", descriptions);
    }
}
