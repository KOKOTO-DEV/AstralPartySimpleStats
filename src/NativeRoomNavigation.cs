using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;

namespace AstralPartySimpleStats;

/// <summary>
/// Client-only panel routing after the game confirms that the local account has
/// left the room. No exit-button events or confirmation dialog callbacks are
/// invoked here. All method signatures below were observed in the user's
/// 2026-11-10 GameAssembly diagnostics.
/// </summary>
internal static class NativeRoomNavigation
{
    private const string BackSignature = "UI.IBasePanel,System.Object[]";
    private const string OpenSignature = "UIPanelType,System.Object[]";

    [DllImport("GameAssembly.dll", EntryPoint = "il2cpp_field_get_default_value",
        ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr FieldDefaultValue(IntPtr field, out IntPtr fieldType);

    [DllImport("GameAssembly.dll", EntryPoint = "il2cpp_get_corlib",
        ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr GetCorlib();

    [DllImport("GameAssembly.dll", EntryPoint = "il2cpp_array_new",
        ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr NewArray(IntPtr elementClass, nuint length);

    public static bool IsOutsideRoom(string panel) =>
        panel is "RoomListPanel" or "MainPanel" or "MainMenuPanel";

    private static bool TryGetUi(out IntPtr ui, out IntPtr panel, out string reason)
    {
        ui = IntPtr.Zero;
        panel = IntPtr.Zero;
        reason = string.Empty;
        if (!FairyGui.TryClass("UI", "UIManager", out var cls))
        {
            reason = "UIManager class unavailable";
            return false;
        }
        ui = FairyGui.ReadStaticObject(cls, "_inst");
        if (ui == IntPtr.Zero)
        {
            reason = "UIManager instance unavailable";
            return false;
        }
        panel = FairyGui.Call(ui, "get_currentPanel");
        return true;
    }

    public static string CurrentPanel()
    {
        if (!TryGetUi(out _, out var panel, out var reason)) return reason;
        return FairyGui.ClassName(panel);
    }

    /// <summary>
    /// The game's native back-stack operation. This uses the confirmed
    /// UIManager.ReturnToLastPanel(IBasePanel, object[]) signature rather than
    /// guessing a method on RoomWaitPanel. The object[] must be a valid empty
    /// IL2CPP array, not a pointer to a managed .NET array.
    /// </summary>
    public static bool TryReturnToPrevious(out string diagnosis)
    {
        diagnosis = string.Empty;
        try
        {
            if (!TryGetUi(out var ui, out var panel, out diagnosis)) return false;
            var current = FairyGui.ClassName(panel);
            if (current is not ("RoomWaitPanel" or "RoomHeroPanel"))
            {
                diagnosis = "Not a room panel: " + current;
                return false;
            }
            FairyGui.Method(IL2CPP.il2cpp_object_get_class(ui), "ReturnToLastPanel", BackSignature);
            var args = EmptyObjectArray();
            FairyGui.CallObjects(ui, "ReturnToLastPanel", BackSignature, panel, args);
            diagnosis = "UIManager.ReturnToLastPanel(" + current + ", [])";
            return true;
        }
        catch (Exception ex)
        {
            diagnosis = "ReturnToLastPanel failed: " + ex.GetType().Name + ": " + ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Fallback when a room panel's back stack is missing or stale. Looks up
    /// UIPanelType.RoomList[Panel] from live IL2CPP enum metadata. Never uses
    /// a guessed integer panel ID and never invokes the room-leave RPC twice.
    /// </summary>
    public static bool TryOpenRoomList(out string diagnosis)
    {
        diagnosis = string.Empty;
        try
        {
            if (!TryGetUi(out var ui, out _, out diagnosis)) return false;
            var klass = IL2CPP.il2cpp_object_get_class(ui);
            var method = FairyGui.Method(klass, "OpenPanel", OpenSignature);
            var parameterType = IL2CPP.il2cpp_method_get_param(method, 0);
            var enumClass = IL2CPP.il2cpp_class_from_type(parameterType);
            if (enumClass == IntPtr.Zero || !IL2CPP.il2cpp_class_is_enum(enumClass))
            {
                diagnosis = "OpenPanel parameter is not a UIPanelType enum";
                return false;
            }
            if (IL2CPP.il2cpp_type_get_type(IL2CPP.il2cpp_class_enum_basetype(enumClass)) != 8)
            {
                diagnosis = "UIPanelType is not Int32-backed";
                return false;
            }
            if (!TryRoomListPanelType(enumClass, out var panelValue, out var panelName, out var names))
            {
                diagnosis = "No unambiguous RoomList UIPanelType. Found: " + names;
                return false;
            }
            FairyGui.CallValueObject(ui, "OpenPanel", OpenSignature, panelValue, EmptyObjectArray());
            diagnosis = "UIManager.OpenPanel(UIPanelType." + panelName + "=" + panelValue + ", [])";
            return true;
        }
        catch (Exception ex)
        {
            diagnosis = "OpenPanel failed: " + ex.GetType().Name + ": " + ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Final fail-safe: the real no-argument UIManager return-home method.
    /// Only called when server-side exit is established and two more precise
    /// room-list routes have failed. It does not create a confirmation dialog.
    /// </summary>
    public static bool TryReturnHome(out string diagnosis)
    {
        diagnosis = string.Empty;
        try
        {
            if (!TryGetUi(out var ui, out _, out diagnosis)) return false;
            FairyGui.Call(ui, "ReturnHomePanelDirectly");
            diagnosis = "UIManager.ReturnHomePanelDirectly()";
            return true;
        }
        catch (Exception ex)
        {
            diagnosis = "ReturnHomePanelDirectly failed: " + ex.GetType().Name + ": " + ex.Message;
            return false;
        }
    }

    private static IntPtr EmptyObjectArray()
    {
        var corlib = GetCorlib();
        if (corlib == IntPtr.Zero) throw new InvalidOperationException("Game corlib unavailable");
        var objectClass = IL2CPP.il2cpp_class_from_name(corlib, "System", "Object");
        if (objectClass == IntPtr.Zero) throw new InvalidOperationException("System.Object class unavailable");
        var array = NewArray(objectClass, 0);
        if (array == IntPtr.Zero) throw new InvalidOperationException("IL2CPP empty object[] unavailable");
        return array;
    }

    private static bool TryRoomListPanelType(IntPtr enumClass, out int panelValue,
        out string panelName, out string discovered)
    {
        panelValue = 0;
        panelName = string.Empty;
        var names = new List<string>();
        var matches = new List<(int Value, string Name)>();
        var iterator = IntPtr.Zero;
        IntPtr field;
        while ((field = IL2CPP.il2cpp_class_get_fields(enumClass, ref iterator)) != IntPtr.Zero)
        {
            if ((IL2CPP.il2cpp_field_get_flags(field) & 0x40) == 0) continue;
            var name = Marshal.PtrToStringAnsi(IL2CPP.il2cpp_field_get_name(field)) ?? string.Empty;
            if (names.Count < 150) names.Add(name);
            var normalized = name.Replace("_", "", StringComparison.Ordinal).ToLowerInvariant();
            if (normalized is not ("roomlistpanel" or "roomlist" or "roomlistui")) continue;
            if (TryConstantInt32(field, out var numeric)) matches.Add((numeric, name));
        }
        discovered = string.Join(", ", names);
        if (matches.Count != 1) return false;
        (panelValue, panelName) = matches[0];
        return true;
    }

    private static bool TryConstantInt32(IntPtr field, out int number)
    {
        number = 0;
        try
        {
            var ptr = FieldDefaultValue(field, out _);
            if (ptr != IntPtr.Zero)
            {
                number = Marshal.ReadInt32(ptr);
                return true;
            }
        }
        catch (EntryPointNotFoundException) { }
        try
        {
            var boxed = IL2CPP.il2cpp_field_get_value_object(field, IntPtr.Zero);
            if (boxed == IntPtr.Zero) return false;
            number = Marshal.ReadInt32(IL2CPP.il2cpp_object_unbox(boxed));
            return true;
        }
        catch (Exception) { return false; }
    }

    public static string DescribeAvailableRoutes()
    {
        try
        {
            if (!TryGetUi(out var ui, out var panel, out var error)) return error;
            return "UIManager: " + DescribeMethods(ui) + " | " + FairyGui.ClassName(panel) + ": " + DescribeMethods(panel);
        }
        catch (Exception ex) { return "Cannot enumerate UI methods: " + ex.Message; }
    }

    private static string DescribeMethods(IntPtr instance)
    {
        if (instance == IntPtr.Zero) return "null";
        var names = new List<string>();
        var cls = IL2CPP.il2cpp_object_get_class(instance);
        for (var parent = cls; parent != IntPtr.Zero; parent = IL2CPP.il2cpp_class_get_parent(parent))
        {
            var iter = IntPtr.Zero;
            IntPtr method;
            while ((method = IL2CPP.il2cpp_class_get_methods(parent, ref iter)) != IntPtr.Zero)
            {
                var name = Marshal.PtrToStringAnsi(IL2CPP.il2cpp_method_get_name(method)) ?? "";
                var lower = name.ToLowerInvariant();
                if (!lower.Contains("panel") && !lower.Contains("room") && !lower.Contains("back") &&
                    !lower.Contains("exit") && !lower.Contains("switch") && !lower.Contains("change")) continue;
                if (names.Count >= 40) break;
                var parameters = new List<string>();
                var count = IL2CPP.il2cpp_method_get_param_count(method);
                for (uint index = 0; index < count; index++)
                {
                    var value = IL2CPP.il2cpp_type_get_name(IL2CPP.il2cpp_method_get_param(method, index));
                    try { parameters.Add(Marshal.PtrToStringAnsi(value) ?? "?"); }
                    finally { IL2CPP.il2cpp_free(value); }
                }
                names.Add(name + "(" + string.Join(",", parameters) + ")");
            }
            if (names.Count >= 40) break;
        }
        return names.Count == 0 ? "none" : string.Join("; ", names);
    }
}
