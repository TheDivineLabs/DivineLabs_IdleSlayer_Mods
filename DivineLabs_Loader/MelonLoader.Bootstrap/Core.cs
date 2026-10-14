using System.Diagnostics;
using MelonLoader.Logging;
using MelonLoader.Bootstrap.RuntimeHandlers.Il2Cpp;
using MelonLoader.Bootstrap.RuntimeHandlers.Mono;
using MelonLoader.Bootstrap.Utils;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using MelonLoader.Bootstrap.Logging;
using Tomlet;

namespace MelonLoader.Bootstrap;

public static class Core
{
    internal static InternalLogger Logger { get; private set; } = new(ColorARGB.BlueViolet, "MelonLoader.Bootstrap");
    internal static InternalLogger PlayerLogger { get; private set; } = new(ColorARGB.Turquoise, "UNITY");

    [RequiresDynamicCode("Calls InitConfig")]
    public static void Init()
    {
        LoaderConfig.Initialize();

        if (LoaderConfig.Current.Loader.Disable)
            return;

        MelonLogger.Init();
        if (!LoaderConfig.Current.Loader.CapturePlayerLogs)
            ConsoleHandler.NullHandles();

        ModuleSymbolRedirect.Attach();
    }
}
