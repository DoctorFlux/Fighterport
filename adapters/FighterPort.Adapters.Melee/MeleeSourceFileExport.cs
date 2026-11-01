using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
namespace FighterPort.Adapters.Melee
{
    public static class MeleeSourceFileExport
    {
        public static void Publish(IReadOnlyDictionary<string,byte[]> files,string destination,IEnumerable<string> protectedSources,CancellationToken cancellation=default)
        {
            if(files==null||files.Count==0||files.Count>64||files.Values.Sum(b=>(long)b.Length)>2L*MeleeSource.MaximumAssetBytes)throw new InvalidDataException("Invalid native source export set.");
            string target=Path.GetFullPath(destination);MeleeSource.NoLinks(target);string parent=Path.GetDirectoryName(target);
            foreach(string source in protectedSources??new string[0]){string original=Path.GetFullPath(source);if(string.Equals(target,original,StringComparison.OrdinalIgnoreCase)||Directory.Exists(original)&&target.StartsWith(original.TrimEnd('\\','/')+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Original game sources are read-only. Choose a separate export folder.");}
            if(!Directory.Exists(parent))throw new DirectoryNotFoundException("Choose an existing export parent folder.");
            if(File.Exists(target)||Directory.Exists(target))throw new IOException("Choose a new, nonexistent folder. Existing destinations remain untouched.");
            var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var hashes=new Dictionary<string,string>();
            foreach(var pair in files)
            {
                cancellation.ThrowIfCancellationRequested();MeleeSource.ValidateVirtualPath(pair.Key);
                if(!MeleeSource.NativeFile(pair.Key)||!paths.Add(pair.Key))throw new InvalidDataException("Ambiguous native source export path.");
                MeleeNative.ReadMembers(pair.Value,cancellation);hashes.Add(pair.Key,MeleeSource.Sha256(pair.Value));
            }
            string scratch=Path.Combine(parent,".fighterport-native-"+Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(scratch);
                foreach(var pair in files)
                {
                    cancellation.ThrowIfCancellationRequested();string output=Path.GetFullPath(Path.Combine(scratch,pair.Key.Replace('/',Path.DirectorySeparatorChar)));
                    if(!output.StartsWith(scratch+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Native export member escaped its temporary root.");
                    Directory.CreateDirectory(Path.GetDirectoryName(output));using(var stream=new FileStream(output,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(pair.Value,0,pair.Value.Length);stream.Flush(true);}
                    byte[] reopened=File.ReadAllBytes(output);MeleeNative.ReadMembers(reopened,cancellation);
                    if(MeleeSource.Sha256(reopened)!=hashes[pair.Key])throw new InvalidDataException("The fresh native export differs from its immutable source snapshot.");
                }
                File.WriteAllLines(Path.Combine(scratch,"Fighterport-native-source-receipt.txt"),new[]{"Fighterport original native source export", "Unedited native files; no retargeting/fitting or game acceptance is claimed."}.Concat(hashes.Select(p=>p.Key+" SHA256 "+p.Value)),new UTF8Encoding(false));
                cancellation.ThrowIfCancellationRequested();Directory.Move(scratch,target);
            }
            finally
            {
                string expected=Path.GetFullPath(scratch),prefix=Path.GetFullPath(parent).TrimEnd('\\','/')+Path.DirectorySeparatorChar;
                if(!expected.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(expected).StartsWith(".fighterport-native-",StringComparison.Ordinal))throw new InvalidOperationException("Invalid native export scratch ownership.");
                if(Directory.Exists(expected)){MeleeSource.NoLinks(expected);Directory.Delete(expected,true);}
            }
        }
    }
}
