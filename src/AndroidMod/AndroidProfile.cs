using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using UnityEngine;
namespace DivineLabs.IdleSlayer;
internal sealed class AndroidProfile
{
    public int SchemaVersion {get;set;}
    public bool Approved {get;set;}
    public string Package {get;set;}="";
    public string VersionName {get;set;}="";
    public string NativeSha256 {get;set;}="";
    public string MetadataSha256 {get;set;}="";
    internal static bool Supported()
    {
        if(Application.platform!=RuntimePlatform.Android||RuntimeInformation.ProcessArchitecture!=Architecture.Arm64)return false;
        using var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("DivineLabs.AndroidProfile");
        var profile=resource==null?null:JsonSerializer.Deserialize<AndroidProfile>(resource);
        if(profile==null||profile.SchemaVersion!=1||!profile.Approved||profile.Package!=Application.identifier||profile.VersionName!=Application.version)return false;
        // The original game libraries remain in the APK. No Windows GameAssembly pin is reused.
        using var apk=ZipFile.OpenRead(Application.dataPath);
        return Matches(apk,"lib/arm64-v8a/libil2cpp.so",profile.NativeSha256)&&Matches(apk,"assets/bin/Data/Managed/Metadata/global-metadata.dat",profile.MetadataSha256);
    }
    private static bool Matches(ZipArchive apk,string path,string digest)
    {
        if(digest.Length!=64||!digest.All(Uri.IsHexDigit))return false;
        var entries=apk.Entries.Where(e=>e.FullName==path).ToArray();if(entries.Length!=1)return false;
        using var stream=entries[0].Open();return Convert.ToHexString(SHA256.HashData(stream)).Equals(digest,StringComparison.OrdinalIgnoreCase);
    }
}
