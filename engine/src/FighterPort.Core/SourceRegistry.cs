using System.Text.Json;

namespace FighterPort.Core;

public static class SourceRegistry
{
    public static string DefaultPath => Path.Combine(Environment.GetEnvironmentVariable("FIGHTERPORT_DATA")??Path.Combine(AppContext.BaseDirectory,"local"),"sources.json");
    public static SourceMount Create(string label,string game,string platform,string build,SourceKind kind,string path)
    {
        if(string.IsNullOrWhiteSpace(label)||string.IsNullOrWhiteSpace(game)||string.IsNullOrWhiteSpace(platform)||string.IsNullOrWhiteSpace(build))throw new ArgumentException("Name, game, platform and build/version are required.");
        path=Path.GetFullPath(path);
        if(kind==SourceKind.DiscImage?!File.Exists(path):!Directory.Exists(path))throw new FileNotFoundException("Choose an existing source of the selected kind.",path);
        return new(Guid.NewGuid(),path,new(game,platform,build),label,kind);
    }
    public static async Task<SourceMount[]> LoadAsync(string path,CancellationToken ct=default)=>!File.Exists(path)?[]:JsonSerializer.Deserialize<SourceMount[]>(await File.ReadAllTextAsync(path,ct),ProjectStore.Json)??[];
    public static async Task SaveAsync(SourceMount[] sources,string path,CancellationToken ct=default)
    {
        if(sources.Select(s=>s.Id).Distinct().Count()!=sources.Length)throw new InvalidDataException("Source profile IDs must be unique.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp=path+".tmp";try{await File.WriteAllTextAsync(temp,JsonSerializer.Serialize(sources,ProjectStore.Json),ct);File.Move(temp,path,true);}finally{if(File.Exists(temp))File.Delete(temp);}
    }
}
