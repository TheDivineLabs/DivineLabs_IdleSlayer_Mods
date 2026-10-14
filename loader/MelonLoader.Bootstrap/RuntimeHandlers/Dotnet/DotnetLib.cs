using System.Runtime.InteropServices;
using System.Text;
using MelonLoader.Bootstrap.Utils;

namespace MelonLoader.Bootstrap.RuntimeHandlers.Dotnet;

public class DotnetLib
{
    private const CharSet hostfxrCharSet =
#if WINDOWS
        CharSet.Unicode;
#else
        CharSet.Ansi;
#endif
    private const StringMarshalling hostfxrStringMarsh =
#if WINDOWS
        StringMarshalling.Utf16;
#else
        StringMarshalling.Utf8;
#endif
    
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = hostfxrCharSet)]
    private delegate int hostfxr_initialize_for_runtime_config_Fn(string runtimeConfigPath, nint parameters, ref nint hostContextHandle);
    private hostfxr_initialize_for_runtime_config_Fn? hostfxr_initialize_for_runtime_config;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int hostfxr_get_runtime_delegate_Fn(nint context, HostfxrDelegateType type, ref LoadAssemblyAndGetFunctionPointerFn? del);
    private hostfxr_get_runtime_delegate_Fn? hostfxr_get_runtime_delegate;
    
    public required nint Handle { get; init; }
    
    public static bool GetHostFxrSystemPath(out string? path)
    {
        path = GetHostfxrPathFromExport();

        if (string.IsNullOrEmpty(path)
            || string.IsNullOrWhiteSpace(path))
            return false;

        return true;
    }

    public static DotnetLib? TryLoad(string path)
    {
        if (string.IsNullOrEmpty(path)
            || string.IsNullOrWhiteSpace(path)
            || !File.Exists(path))
            return null;

        MelonDebug.Log($"Attempting to use HostFXR Path: {path}");
        if (!NativeLibrary.TryLoad(path, out nint _module))
        {
            MelonDebug.Log($"Failed to load Native Library from HostFXR Path: {path}");
            return null;
        }

        if (!NativeFunc.GetExport(_module, "hostfxr_initialize_for_runtime_config",
                out hostfxr_initialize_for_runtime_config_Fn? hostfxr_initialize_for_runtime_config))
        {
            MelonDebug.Log($"Failed to get \"hostfxr_initialize_for_runtime_config\" from HostFXR Path: {path}");
            return null;
        }

        if (!NativeFunc.GetExport(_module, "hostfxr_get_runtime_delegate",
                out hostfxr_get_runtime_delegate_Fn? hostfxr_get_runtime_delegate))
        {
            MelonDebug.Log($"Failed to get \"hostfxr_get_runtime_delegate\" from HostFXR Path: {path}");
            return null;
        }

        Core.Logger.Msg($"Using HostFXR Path: {path}");
        return new()
        {
            Handle = _module,
            hostfxr_initialize_for_runtime_config = hostfxr_initialize_for_runtime_config,
            hostfxr_get_runtime_delegate = hostfxr_get_runtime_delegate
        };
    }
    
    public bool InitializeForRuntimeConfig(string runtimeConfigPath, out nint context)
    {
        if (hostfxr_initialize_for_runtime_config == null)
        {
            context = 0;
            return false;
        }

        nint ctx = 0;
        ConsoleHandler.NullHandles(); // Prevent it from logging its own stuff
        var status = hostfxr_initialize_for_runtime_config(runtimeConfigPath, 0, ref ctx);
        ConsoleHandler.ResetHandles();

        if (status != 0)
        {
            context = status;
            return false;
        }

        context = ctx;
        return true;
    }

    public TDelegate? LoadAssemblyAndGetFunctionUco<TDelegate>(nint context, string assemblyPath, string typeName, string methodName) where TDelegate : Delegate
    {
        if (hostfxr_get_runtime_delegate == null)
            return null;

        LoadAssemblyAndGetFunctionPointerFn? loadAssemblyAndGetFunctionPointer = null;
        hostfxr_get_runtime_delegate(context, HostfxrDelegateType.HdtLoadAssemblyAndGetFunctionPointer, ref loadAssemblyAndGetFunctionPointer);
        if (loadAssemblyAndGetFunctionPointer == null)
            return null;

        nint funcPtr = 0;
        loadAssemblyAndGetFunctionPointer(assemblyPath, typeName, methodName, -1, 0, ref funcPtr);
        if (funcPtr == 0)
            return null;

        return Marshal.GetDelegateForFunctionPointer<TDelegate>(funcPtr);
    }
    
    [DllImport("*", CharSet = hostfxrCharSet)]
    private static extern int get_hostfxr_path(StringBuilder buffer, ref nint bufferSize, nint parameters);
    private static string? GetHostfxrPathFromExport()
    {
        var buffer = new StringBuilder(1024);
        var bufferSize = (nint)buffer.Capacity;
        var result = get_hostfxr_path(buffer, ref bufferSize, 0);
        return result != 0 ? null : buffer.ToString();
    }

    private enum HostfxrDelegateType
    {
        HdtComActivation,
        HdtLoadInMemoryAssembly,
        HdtWinrtActivation,
        HdtComRegister,
        HdtComUnregister,
        HdtLoadAssemblyAndGetFunctionPointer,
        HdtGetFunctionPointer,
        HdtLoadAssembly,
        HdtLoadAssemblyBytes,
    };

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = hostfxrCharSet)]
    private delegate void LoadAssemblyAndGetFunctionPointerFn(string assemblyPath, string typeName, string methodName, nint delegateTypeName, nint reserved, ref nint funcPtr);
}