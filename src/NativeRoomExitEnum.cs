using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;

namespace AstralPartySimpleStats;

/// <summary>
/// Read the currently loaded HybridCLR protobuf enum from IL2CPP metadata.
/// Only resolve known protocol constants; never assume that 0/1 means
/// normal/force on all game versions.
/// </summary>
internal static class NativeRoomExitEnum
{
    // Not every GameAssembly build exports the default-value entry point.
    // In that case the boxed field getter is attempted instead.
    [DllImport("GameAssembly.dll", EntryPoint = "il2cpp_field_get_default_value",
        ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr FieldDefaultValue(IntPtr field, out IntPtr fieldType);

    public static int SelectExitMode(IntPtr roomLogic, string methodName,
        string signature, bool host, out string selectedName, out string variants)
    {
        if (roomLogic == IntPtr.Zero) throw new InvalidOperationException("Room logic is unavailable.");
        var klass = IL2CPP.il2cpp_object_get_class(roomLogic);
        var method = FairyGui.Method(klass, methodName, signature);
        var parameterType = IL2CPP.il2cpp_method_get_param(method, 0);
        var enumClass = IL2CPP.il2cpp_class_from_type(parameterType);
        if (enumClass == IntPtr.Zero || !IL2CPP.il2cpp_class_is_enum(enumClass))
            throw new InvalidOperationException("RequestExitRoomC2S parameter is not an enum.");
        var baseType = IL2CPP.il2cpp_class_enum_basetype(enumClass);
        if (IL2CPP.il2cpp_type_get_type(baseType) != 8) // IL2CPP_TYPE_I4
            throw new InvalidOperationException("ForceExitType underlying type is not Int32.");

        var values = new List<RoomExitEnumSelection.Value>();
        var iter = IntPtr.Zero;
        IntPtr field;
        while ((field = IL2CPP.il2cpp_class_get_fields(enumClass, ref iter)) != IntPtr.Zero)
        {
            var name = Marshal.PtrToStringAnsi(IL2CPP.il2cpp_field_get_name(field)) ?? string.Empty;
            var flags = IL2CPP.il2cpp_field_get_flags(field);
            if ((flags & 0x40) == 0) continue; // FieldAttributes.Literal
            if (TryReadConstant(field, out var numeric))
                values.Add(new RoomExitEnumSelection.Value(name, numeric));
        }
        variants = values.Count == 0 ? "unreadable" : string.Join(", ", values);
        var mode = RoomExitEnumSelection.Choose(values, host);
        selectedName = mode.Name;
        return mode.Number;
    }

    private static bool TryReadConstant(IntPtr field, out int value)
    {
        value = 0;
        try
        {
            var pointer = FieldDefaultValue(field, out _);
            if (pointer != IntPtr.Zero)
            {
                value = Marshal.ReadInt32(pointer);
                return true;
            }
        }
        catch (EntryPointNotFoundException) { /* older GameAssembly */ }
        try
        {
            var boxed = IL2CPP.il2cpp_field_get_value_object(field, IntPtr.Zero);
            if (boxed != IntPtr.Zero)
            {
                value = Marshal.ReadInt32(IL2CPP.il2cpp_object_unbox(boxed));
                return true;
            }
        }
        catch (Exception) { /* value_object may not expose literal fields */ }
        return false;
    }
}
