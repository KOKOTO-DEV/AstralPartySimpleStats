using System;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace AstralPartySimpleStats;

internal sealed class ProfileQueryAccess
{
    private readonly NativeGameData _metadata = new();
    private IntPtr _bridge;
    private bool _loadAttempted;

    public string Read(long playerId, string roomId)
    {
        LoadBridge();
        if (_bridge == IntPtr.Zero || !long.TryParse(roomId, out var room) || room <= 0) return string.Empty;
        FairyGui.CallStatic(_bridge,"BeginRoom","System.Int64",room);
        var value = FairyGui.CallStatic(_bridge, "Read", "System.Int64", playerId);
        return value == IntPtr.Zero ? string.Empty : Il2CppInterop.Runtime.IL2CPP.Il2CppStringToManaged(value) ?? string.Empty;
    }

    private void LoadBridge()
    {
        if (_bridge == IntPtr.Zero)
        {
            if (_loadAttempted) return;
            _loadAttempted = true;
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AstralPartySimpleStats.ProfileBridge.dll")
                ?? throw new InvalidOperationException("Profile bridge resource is missing.");
            var bytes = new byte[checked((int)stream.Length)];
            stream.ReadExactlyCompat(bytes);
            // The bridge must run in the game's HybridCLR domain, where its
            // typed RPC delegates and hot-update game classes actually exist.
            Il2CppSystem.Reflection.Assembly.Load(new Il2CppStructArray<byte>(bytes));
            _bridge = _metadata.FindClass("AstralPartySimpleStats.ProfileBridge", "AstralPartySimpleStats.RuntimeBridge", "ProfileBridge");
            if (_bridge == IntPtr.Zero) throw new TypeLoadException("HybridCLR profile bridge was not loaded.");
        }
    }

    public void SyncParticipants(string roomId, string ids)
    {
        LoadBridge();
        if (_bridge == IntPtr.Zero || !long.TryParse(roomId,out var room) || room<=0) return;
        FairyGui.CallStatic(_bridge,"BeginRoom","System.Int64",room);
        FairyGui.CallStaticObject(_bridge,"SetParticipants",FairyGui.String,FairyGui.InternString(ids));
    }

    public void Shutdown()
    {
        if (_bridge != IntPtr.Zero) FairyGui.CallStatic(_bridge, "Stop");
        _metadata.Clear();
        _bridge = IntPtr.Zero;
        _loadAttempted = false;
    }

    public void EndRoom()
    {
        if (_bridge != IntPtr.Zero) FairyGui.CallStatic(_bridge,"BeginRoom","System.Int64",0L);
    }
    public int ActionState(long id)
    {
        if(_bridge==IntPtr.Zero) return -1;
        var value=FairyGui.CallStatic(_bridge,"ActionState","System.Int64",id);
        var text=value==IntPtr.Zero?string.Empty:Il2CppInterop.Runtime.IL2CPP.Il2CppStringToManaged(value);
        return int.TryParse(text,out var state)?state:-1;
    }
    public void RequestAction(long id,string action)
    {
        if(_bridge!=IntPtr.Zero) FairyGui.CallStaticObject(_bridge,"RequestAction",FairyGui.String,
            FairyGui.InternString(action+"|"+id.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }
}

internal static class ProfileResourceStream
{
    internal static void ReadExactlyCompat(this System.IO.Stream stream, byte[] bytes)
    {
        var offset = 0;
        while (offset < bytes.Length)
        {
            var read = stream.Read(bytes, offset, bytes.Length - offset);
            if (read == 0) throw new System.IO.EndOfStreamException();
            offset += read;
        }
    }
}
