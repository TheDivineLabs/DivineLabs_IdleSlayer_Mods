using MelonLoader.Bootstrap.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using MelonLoader.Bootstrap.RuntimeHandlers.Dotnet;

namespace MelonLoader.Bootstrap.RuntimeHandlers.Il2Cpp;

internal static class Il2CppHandler
{
    private static Il2CppLib il2cpp = null!;
    private static bool il2cppInitDone;
    private static bool invokeStarted;

    private static readonly Il2CppLib.InitFn Il2CPPInitDetourFn = InitDetour;
    private static readonly Il2CppLib.RuntimeInvokeFn InvokeDetourFn = InvokeDetour;
    internal static readonly Dictionary<string, (Action<nint> InitMethod, IntPtr detourPtr)> SymbolRedirects = new()
    {
        { "il2cpp_init", (Initialize, Marshal.GetFunctionPointerForDelegate(Il2CPPInitDetourFn))},
        { "il2cpp_runtime_invoke", (Initialize, Marshal.GetFunctionPointerForDelegate(InvokeDetourFn))},
    };

    public static void Initialize(nint handle)
    {
        var il2cppLib = Il2CppLib.TryLoad(handle);
        if (il2cppLib is null)
        {
            Core.Logger.Error("Could not load il2cpp");
            return;
        }

        il2cpp = il2cppLib;
    }

    internal static nint InitDetour(nint a)
    {
        if (il2cppInitDone)
            return il2cpp.Init(a);

        ConsoleHandler.ResetHandles();
        MelonDebug.Log("In init detour");

        var domain = il2cpp.Init(a);

        DotnetHandler.Initialize();
        il2cppInitDone = true;

        return domain;
    }

    internal static nint InvokeDetour(nint method, nint obj, nint args, nint exc)
    {
        var result = il2cpp.RuntimeInvoke(method, obj, args, exc);
        if (invokeStarted)
            return result;

        var name = il2cpp.GetMethodName(method);
        if (name == null || !name.Contains("Internal_ActiveSceneChanged"))
            return result;

        invokeStarted = true;
        MelonDebug.Log("Invoke hijacked");

        DotnetHandler.Start();

        return result;
    }
}