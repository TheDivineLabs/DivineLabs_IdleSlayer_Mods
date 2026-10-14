#if X64 && (WINDOWS || OSX || LINUX)
using System.Diagnostics;
using System.IO.Compression;

namespace MelonLoader.Bootstrap.RuntimeHandlers.Dotnet;

internal static class DotnetPortable
{
    private const string dotnetRuntimeDownload =
#if LINUX
        "https://github.com/LavaGang/PortableDotnet/raw/refs/heads/6.0.25/dotnet6.linux.x86_64.zip";
#elif OSX
        "https://github.com/LavaGang/PortableDotnet/raw/refs/heads/6.0.25/dotnet6.macos.x86_64.zip";
#elif WINDOWS
        "https://github.com/LavaGang/PortableDotnet/raw/refs/heads/6.0.25/dotnet6.windows.x86_64.zip";
#endif
    
    private static readonly FileDownload downloadRequest = new(dotnetRuntimeDownload);

    public static bool AttemptInstall()
    {
        Core.Logger.Msg($"Downloading the Portable .NET Runtime from: {dotnetRuntimeDownload}");
        var tempPath = Path.GetTempFileName() + ".zip";
        (bool, HttpResponseMessage?)? resp = null;
        try
        {
            resp = downloadRequest.Attempt(tempPath);
            if (!resp.Value.Item1)
            {
                Core.Logger.Error("Failed to download the Portable .NET Runtime. Check your internet connection.");
                
                if (resp.Value.Item2 != null)
                    Core.Logger.Error(resp.Value.Item2.ReasonPhrase!);
                
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
                return false;
            }
        }
        catch (Exception ex)
        {
            Core.Logger.Error("Failed to download the Portable .NET Runtime. Check your internet connection.");
            
            if (resp.HasValue
                && (resp.Value.Item2 != null))
                Core.Logger.Error(resp.Value.Item2.ReasonPhrase!);
            
            Core.Logger.Error(ex.ToString());
            
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            return false;
        }

        Core.Logger.Msg("Extracting the Portable .NET Runtime...");
        string dependenciesDir = Path.Combine(LoaderConfig.Current.Loader.BaseDirectory, "MelonLoader", "Dependencies");
        string dotnetDir = Path.Combine(dependenciesDir, "dotnet");
        if (!Directory.Exists(dependenciesDir))
            Directory.CreateDirectory(dependenciesDir);
        try
        {
            if (Directory.Exists(dotnetDir))
                Directory.Delete(dotnetDir, true);
            ZipFile.ExtractToDirectory(tempPath, dependenciesDir);
        }
        catch (Exception ex)
        {
            Core.Logger.Error($"Failed to extract the Portable .NET Runtime");
            Core.Logger.Error(ex.ToString());
            if (Directory.Exists(dotnetDir))
                Directory.Delete(dotnetDir, true);
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            return false;
        }
        if (File.Exists(tempPath))
            File.Delete(tempPath);
        return true;
    }
}
#endif
