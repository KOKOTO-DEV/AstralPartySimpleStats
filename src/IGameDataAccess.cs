using System;
namespace AstralPartySimpleStats;
internal interface IGameDataAccess
{
    IntPtr FindClass(string assemblyName, string namespaze, string name);
    IntPtr ReadStaticObject(IntPtr klass, string fieldName);
    IntPtr ReadObject(IntPtr instance, string fieldName);
    int ReadInt32(IntPtr instance, string fieldName);
    IntPtr Invoke(IntPtr instance, string methodName);
    IntPtr Invoke(IntPtr instance, string methodName, long argumentValue);
    long InvokeInt64(IntPtr instance, string methodName);
    int InvokeInt32(IntPtr instance, string methodName);
    bool InvokeBoolean(IntPtr instance, string methodName);
    string InvokeString(IntPtr instance, string methodName);
    string InvokeString(IntPtr instance, string methodName, bool argumentValue);
}

