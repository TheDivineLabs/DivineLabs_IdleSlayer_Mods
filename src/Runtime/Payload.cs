using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
namespace DivineLabs.IdleSlayer.Runtime;
public static class Payload
{
    private sealed class PackageIndex
    { public PackageIndex() {} public string version { get; set; }=""; public string url { get; set; }=""; public string sha256 { get; set; }=""; }
    private static readonly HttpClient Http=new(){Timeout=TimeSpan.FromMinutes(15)};
    public static void Verify(string root,IProgress<SetupProgress> progress=null,CancellationToken cancel=default)
    {
        var manifest=PackageManifest.Read(root);int i=0;
        foreach(var entry in manifest.Files)
        {
            cancel.ThrowIfCancellationRequested();string file=Paths.Inside(root,entry.Key);
            if(!File.Exists(file))throw new InvalidDataException("Отсутствует компонент: "+entry.Key);
            using var stream=File.OpenRead(file);
            if(!Convert.ToHexString(SHA256.HashData(stream)).Equals(entry.Value,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Повреждён компонент: "+entry.Key);
            if(++i%20==0)progress?.Report(new("Проверка компонентов…",100.0*i/manifest.Files.Count));
        }
    }
    public static async Task<string> EnsureAsync(IProgress<SetupProgress> progress=null,CancellationToken cancel=default)
    {
        string cache=Path.Combine(Product.Cache,"Components",Product.Version);
        foreach(string candidate in new[]{Product.Components,cache})
        {
            if(!Directory.Exists(candidate))continue;
            try{Verify(candidate,progress,cancel);return candidate;}catch(Exception e)when(e is IOException or JsonException){Product.Record(e);}
        }
        using var resource=typeof(Payload).Assembly.GetManifestResourceStream("DivineLabs.PackageIndex");
        var index=JsonSerializer.Deserialize<PackageIndex>(resource!)!;
        if(index.version!=Product.Version||index.sha256.Length!=64||!index.sha256.All(Uri.IsHexDigit))throw new InvalidDataException("Не настроен источник компонентов. Используйте полный portable ZIP.");
        string download=Path.Combine(Product.Cache,"Downloads",Product.Version+"-components.zip");Directory.CreateDirectory(Path.GetDirectoryName(download)!);
        await DownloadVerifiedAsync(new Uri(index.url),download,index.sha256,progress,cancel);
        string stage=Path.Combine(Product.Cache,"Work","components-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
        progress?.Report(new("Распаковка компонентов…"));ExtractSafe(download,stage,cancel);Verify(stage,progress,cancel);
        Directory.CreateDirectory(Path.GetDirectoryName(cache)!);
        if(Directory.Exists(cache))Directory.Move(cache,cache+"-damaged-"+Guid.NewGuid().ToString("N"));
        Directory.Move(stage,cache);return cache;
    }
    public static async Task DownloadVerifiedAsync(Uri url,string target,string expected,IProgress<SetupProgress> progress=null,CancellationToken cancel=default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if(File.Exists(target)){using var old=File.OpenRead(target);if(Convert.ToHexString(SHA256.HashData(old)).Equals(expected,StringComparison.OrdinalIgnoreCase))return;}
        if(url.Scheme!=Uri.UriSchemeHttps && !(url.IsLoopback && url.Scheme==Uri.UriSchemeHttp))throw new InvalidDataException("Источник компонентов должен использовать HTTPS.");
        string part=target+".part";
        using(var response=await Http.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,cancel))
        {
            response.EnsureSuccessStatusCode();long? total=response.Content.Headers.ContentLength;
            using var input=await response.Content.ReadAsStreamAsync(cancel);using var output=new FileStream(part,FileMode.Create,FileAccess.Write,FileShare.None,81920,true);
            byte[] buffer=new byte[81920];long received=0;int count;
            while((count=await input.ReadAsync(buffer,cancel))>0){await output.WriteAsync(buffer.AsMemory(0,count),cancel);received+=count;progress?.Report(new($"Загрузка компонентов: {received/1048576.0:F1} МБ",total>0?received*100.0/total:null));}
        }
        using(var stream=File.OpenRead(part))if(!Convert.ToHexString(SHA256.HashData(stream)).Equals(expected,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Контрольная сумма загрузки не совпала. Файлы игры не изменены.");
        File.Move(part,target,true);
    }
    public static void ExtractSafe(string archive,string target,CancellationToken cancel=default)
    {
        using var zip=ZipFile.OpenRead(archive);long total=0;
        foreach(var entry in zip.Entries){Paths.Inside(target,entry.FullName);total+=entry.Length;if(total>4L*1024*1024*1024||((entry.ExternalAttributes>>16)&0xf000)==0xa000)throw new InvalidDataException("Недопустимое содержимое архива.");}
        foreach(var entry in zip.Entries){cancel.ThrowIfCancellationRequested();string path=Paths.Inside(target,entry.FullName);if(entry.FullName.EndsWith('/')){Directory.CreateDirectory(path);continue;}Directory.CreateDirectory(Path.GetDirectoryName(path)!);entry.ExtractToFile(path,true);}
    }
}
