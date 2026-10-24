using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FighterPort.Core;

namespace FighterPort.Adapters.Brawl;

public record GameAssetInventory(SourceMount Source,string? FighterRoot,string[] Files,long Length,long ModifiedTicks,string Diagnostics,string? ToolVersion,string? ToolHash)
{
    public Dictionary<string,long> Sizes {get;init;}=new(StringComparer.Ordinal);
}
public record PreparedGameAssets(string Directory,string Receipt,Dictionary<string,string> Files,string Diagnostics);

// Read-only Brawl-family source discovery. WIT is an explicitly configured,
// external GPL tool, not a linked component or bundled executable. Other games
// keep their registrations but require their own native source providers.
public static class BrawlGameSources
{
    static readonly SemaphoreSlim Worker=new(1);
    public const int MaximumFiles=10000;
    public const long MaximumBytes=512L*1024*1024;
    static bool Native(string path)=>new[]{".pac",".pcs",".brres",".mdl0",".chr0"}.Contains(Path.GetExtension(path),StringComparer.OrdinalIgnoreCase);
    public static string Canonical(string path)
    {
        if(string.IsNullOrWhiteSpace(path)||path.Length>1024||path.Contains('\\')||path.Contains(':')||path.Contains(';')||path.Contains('%')||path.Contains('\n')||path.Contains('\r')||path.Contains('\0')||path.Split('/').Any(p=>p is "" or "." or ".."||p.IndexOfAny(Path.GetInvalidFileNameChars())>=0)||!path.StartsWith("fighter/",StringComparison.Ordinal))throw new InvalidDataException("Invalid game asset identity: "+path);
        return path;
    }
    static void NoLinks(string path)
    {
        for(string? p=Path.GetFullPath(path);p is not null;p=Path.GetDirectoryName(p))if((File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new IOException("Game source links/reparse points require an explicit ordinary folder: "+p);
    }
    static string Tool(SourceMount source)
    {
        string tool=Path.GetFullPath(source.DiscToolPath??Environment.GetEnvironmentVariable("FIGHTERPORT_WIT")??throw new NotSupportedException("Configure WIT (wit.exe) in Game sources & builds to browse WBFS/ISO. Registration alone does not decode a disc."));
        if(!File.Exists(tool))throw new FileNotFoundException("Configured WIT tool is unavailable.",tool);return tool;
    }
    static async Task<(string Output,string Diagnostics)> Run(string tool,IEnumerable<string> arguments,CancellationToken ct)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromMinutes(3));
        await Worker.WaitAsync(timeout.Token);
        try
        {
            var start=new ProcessStartInfo(tool){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};foreach(string a in arguments)start.ArgumentList.Add(a);
            using var process=new Process{StartInfo=start};process.Start();
            async Task<string> Read(StreamReader reader,int maximum)
            {
                try{var result=new StringBuilder();var buffer=new char[4096];int count;while((count=await reader.ReadAsync(buffer,timeout.Token))>0){if(result.Length+count>maximum)throw new InvalidDataException("Disc tool exceeded its bounded output limit.");result.Append(buffer,0,count);}return result.ToString();}catch{timeout.Cancel();throw;}
            }
            var stdout=Read(process.StandardOutput,8*1024*1024);var stderr=Read(process.StandardError,1024*1024);
            try
            {
                // A failed/overflowed pipe cancels the process immediately;
                // waiting for exit first can deadlock a tool with a full pipe.
                var exited=process.WaitForExitAsync(timeout.Token);await Task.WhenAll(exited,stdout,stderr).WaitAsync(timeout.Token);
                if(process.ExitCode!=0)throw new IOException($"WIT exited {process.ExitCode}: {(await stderr)[..Math.Min((await stderr).Length,4000)]}");
                return(await stdout,await stderr);
            }
            catch{if(!process.HasExited)process.Kill(true);throw;}
        }
        finally{Worker.Release();}
    }
    static string Folder(string root)
    {
        root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        NoLinks(root);
        var candidates=new[]{root,Path.Combine(root,"fighter"),Path.Combine(root,"files","fighter"),Path.Combine(root,"DATA","files","fighter"),Path.Combine(root,"pf","fighter"),Path.Combine(root,"projectm","pf","fighter"),Path.Combine(root,"project+","pf","fighter"),Path.Combine(root,"private","wii","app","rsbe","pf","fighter")}.Where(p=>Directory.Exists(p)&&Path.GetFileName(p).Equals("fighter",StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if(candidates.Length!=1)throw new InvalidDataException(candidates.Length==0?"No fighter folder found. Select this build's extracted files or pf/fighter folder.":"Several fighter folders found. Select the exact build's fighter folder.");
        NoLinks(candidates[0]);return candidates[0];
    }
    public static async Task<GameAssetInventory> BrowseAsync(SourceMount source,CancellationToken ct=default)
    {
        if(source.Build.Game is not ("Brawl" or "Project M" or "Project+"))throw new NotSupportedException("This source browser reads Brawl-family assets. Other games' native source providers remain unavailable.");
        if(source.Kind==SourceKind.DiscImage)
        {
            string path=Path.GetFullPath(source.Path);if(Path.GetExtension(path).ToLowerInvariant() is not (".wbfs" or ".iso"))throw new NotSupportedException("This WIT provider currently accepts WBFS and ISO only.");NoLinks(path);
            await using var held=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);long modified=File.GetLastWriteTimeUtc(path).Ticks;string tool=Tool(source);
            var version=await Run(tool,["VERSION"],ct);if(!version.Output.StartsWith("wit: Wiimms ISO Tool",StringComparison.Ordinal))throw new InvalidDataException("Configured executable is not a recognized WIT command-line provider.");
            var result=await Run(tool,["FILES",path,"--psel","DATA","--files","+/files/fighter/*","--show","=SIZE,PATH,DEC","--no-header"],ct);
            var files=new List<string>();var sizes=new Dictionary<string,long>(StringComparer.Ordinal);foreach(string line in result.Output.Split('\n',StringSplitOptions.RemoveEmptyEntries))
            {
                string entry=line.TrimEnd('\r');int marker=entry.IndexOf("/files/fighter/",StringComparison.Ordinal);if(marker<0||entry.EndsWith('/'))continue;
                string relative=Canonical(entry[(marker+7)..]);if(!Native(relative))continue;
                string number=entry.TrimStart().Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries)[0];if(!long.TryParse(number,NumberStyles.None,CultureInfo.InvariantCulture,out long size)||size<0)throw new InvalidDataException("WIT did not return a recognized decimal file size.");
                if(!sizes.TryAdd(relative,size))throw new InvalidDataException("Ambiguous duplicate native entry in disc DATA inventory: "+relative);files.Add(relative);
            }
            var unique=files.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();if(unique.Length==0||unique.Length>MaximumFiles)throw new InvalidDataException("Disc returned no bounded native fighter inventory.");
            return new(source,null,unique,held.Length,modified,result.Diagnostics,version.Output.Trim(),ProjectStore.Hash(tool)){Sizes=sizes};
        }
        string folder=Folder(source.Path);var paths=new List<string>();var pending=new Stack<string>();pending.Push(folder);int directories=0;
        while(pending.TryPop(out var directory))
        {
            ct.ThrowIfCancellationRequested();NoLinks(directory);if(++directories>MaximumFiles)throw new InvalidDataException("Source folder exceeds the directory budget.");
            foreach(string child in Directory.EnumerateDirectories(directory))pending.Push(child);
            foreach(string file in Directory.EnumerateFiles(directory)){ct.ThrowIfCancellationRequested();if(!Native(file))continue;NoLinks(file);paths.Add(Canonical("fighter/"+Path.GetRelativePath(folder,file).Replace('\\','/')));if(paths.Count>MaximumFiles)throw new InvalidDataException("Source exceeds the native file budget.");}
        }
        return new(source,folder,paths.Order(StringComparer.Ordinal).ToArray(),0,0,"",null,null){Sizes=paths.ToDictionary(p=>p,p=>new FileInfo(Path.Combine(folder,p[8..].Replace('/',Path.DirectorySeparatorChar))).Length,StringComparer.Ordinal)};
    }
    public static async Task<PreparedGameAssets> PrepareAsync(GameAssetInventory inventory,IEnumerable<string> selected,string cacheRoot,CancellationToken ct=default)
    {
        ct.ThrowIfCancellationRequested();string[] requested=selected.Select(Canonical).Distinct(StringComparer.Ordinal).ToArray();if(requested.Length is <1 or >32||requested.Any(p=>!inventory.Files.Contains(p,StringComparer.Ordinal)))throw new InvalidDataException("Select 1–32 exact entries from the inspected source inventory.");
        long expected=0;foreach(string entry in requested){if(!inventory.Sizes.TryGetValue(entry,out long size)||size<0||size>MaximumBytes||expected>MaximumBytes-size)throw new IOException("Selected native files exceed the 512 MiB preparation budget or need a fresh size inventory.");expected+=size;}
        string root=Path.GetFullPath(cacheRoot);Directory.CreateDirectory(root);NoLinks(root);string stage=Path.Combine(root,Guid.NewGuid().ToString("N"));string diagnostic=inventory.Diagnostics;var result=new Dictionary<string,string>(StringComparer.Ordinal);
        if(inventory.FighterRoot is null)
        {
            await using var held=new FileStream(inventory.Source.Path,FileMode.Open,FileAccess.Read,FileShare.Read);
            if(held.Length!=inventory.Length||File.GetLastWriteTimeUtc(inventory.Source.Path).Ticks!=inventory.ModifiedTicks)throw new IOException("Disc changed after browsing; refresh its inventory.");
            string tool=Tool(inventory.Source);if(ProjectStore.Hash(tool)!=inventory.ToolHash)throw new IOException("Disc tool changed after browsing; inspect again.");
            var extracted=await Run(tool,["EXTRACT",Path.GetFullPath(inventory.Source.Path),"--psel","DATA","--pmode","NONE","--files",string.Join(';',requested.Select(p=>"+/files/"+p)),"--dest",stage],ct);diagnostic=extracted.Diagnostics;
            long total=0;foreach(string entry in requested)
            {
                string file=Path.Combine(stage,"files",entry.Replace('/',Path.DirectorySeparatorChar));NoLinks(file);long length=new FileInfo(file).Length;total+=length;if(total>MaximumBytes||length!=inventory.Sizes[entry])throw new IOException("Prepared native file size differs from its inspected inventory.");result.Add(entry,file);
            }
        }
        else
        {
            Directory.CreateDirectory(stage);long total=0;
            foreach(string entry in requested)
            {
                ct.ThrowIfCancellationRequested();string source=Path.Combine(inventory.FighterRoot,entry[8..].Replace('/',Path.DirectorySeparatorChar));NoLinks(source);
                await using var held=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read);total+=held.Length;if(total>MaximumBytes||held.Length!=inventory.Sizes[entry])throw new IOException("Selected native file changed size after browsing; refresh its inventory.");
                string dest=Path.Combine(stage,"files",entry.Replace('/',Path.DirectorySeparatorChar));Directory.CreateDirectory(Path.GetDirectoryName(dest)!);await using(var output=new FileStream(dest,FileMode.CreateNew,FileAccess.Write,FileShare.None))await held.CopyToAsync(output,ct);result.Add(entry,dest);
            }
        }
        ct.ThrowIfCancellationRequested();string receipt=Path.Combine(stage,"source-receipt.json");var files=new List<object>();
        foreach(var (entry,file) in result){ct.ThrowIfCancellationRequested();await using var stream=File.OpenRead(file);files.Add(new{Entry=entry,Length=stream.Length,Sha256=Convert.ToHexString(await SHA256.HashDataAsync(stream,ct))});}
        await File.WriteAllTextAsync(receipt,JsonSerializer.Serialize(new{Provider="brawl.game-source.1",inventory.Source,inventory.Length,inventory.ModifiedTicks,inventory.ToolVersion,inventory.ToolHash,Diagnostics=diagnostic,Files=files,Claim="Selected DATA/native assets only; file stamp is not a full-disc integrity proof or verified game build."},new JsonSerializerOptions(ProjectStore.Json){IgnoreReadOnlyProperties=false}),ct);
        return new(stage,receipt,result,diagnostic);
    }
}
