using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace BrawlCrate.Studio
{
    // The animation bundle never writes a native game resource or modifies
    // the authoring document. A model projection is used only as the frozen
    // skeleton/skin source for independent portable candidates.
    internal static class PortableAnimationExport
    {
        private static string Sha256(string value)
        {
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value)))
                .Replace("-","").ToLowerInvariant();
        }
        private static string HashFile(string path)
        {
            using(var stream=File.OpenRead(path))using(var sha=SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        }
        private static string Filename(PortableRiggedGltf.Clip clip)
        {
            string readable=new string((clip.Name??"Animation").Select(c=>
                char.IsLetterOrDigit(c)||c=='-'||c=='_'?c:'-').ToArray()).Trim('-');
            if(readable.Length==0)readable="Animation";
            if(readable.Length>48)readable=readable.Substring(0,48);
            return readable+"-"+Sha256(clip.Identity).Substring(0,12)+".glb";
        }
        private static PortableRiggedGltf.Clip Freeze(PortableRiggedGltf.Clip clip)
            =>new PortableRiggedGltf.Clip{Name=clip.Name,Identity=clip.Identity,
                FramesPerSecond=clip.FramesPerSecond,
                Tracks=clip.Tracks.Select(track=>new PortableRiggedGltf.Track{
                    Bone=track.Bone,Frames=(float[])track.Frames.Clone(),
                    Translation=track.Translation==null?null:(System.Numerics.Vector3[])track.Translation.Clone(),
                    Rotation=(System.Numerics.Quaternion[])track.Rotation.Clone()}).ToArray()};
        internal static string[] Publish(string destination,Action<string> prepareModel,
            IReadOnlyList<PortableRiggedGltf.Clip> selected,bool onePerFile,bool includeModel,
            Action validate,CancellationToken cancellation)
        {
            if(prepareModel==null||selected==null||selected.Count==0||selected.Any(clip=>
                clip==null||string.IsNullOrWhiteSpace(clip.Identity)||clip.Tracks==null||clip.Tracks.Length==0))
                throw new InvalidDataException("Select one or more exact portable animations.");
            if(selected.Select(clip=>clip.Identity).Distinct(StringComparer.Ordinal).Count()!=selected.Count)
                throw new InvalidDataException("A portable animation is selected more than once.");
            if(selected.Any(clip=>clip.Tracks.Any(track=>track==null||track.Frames==null||track.Rotation==null)))
                throw new InvalidDataException("A selected portable animation has incomplete tracks.");
            var frozen=selected.Select(Freeze).ToArray();
            var filenames=onePerFile?frozen.Select(Filename).ToArray():new[]{"Selected-animations.glb"};
            if(filenames.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=filenames.Length)
                throw new InvalidDataException("Portable animation filenames collide.");
            destination=Path.GetFullPath(destination);
            if(File.Exists(destination)||Directory.Exists(destination))
                throw new IOException("The animation export destination already exists.");
            string parent=Path.GetDirectoryName(destination);
            if(!Directory.Exists(parent))throw new DirectoryNotFoundException("The animation export parent is unavailable.");
            string stage=Path.Combine(parent,".fighterport-animation-"+Guid.NewGuid().ToString("N"));
            string parentPrefix=Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            string stageFull=Path.GetFullPath(stage);
            if(!stageFull.StartsWith(parentPrefix,StringComparison.OrdinalIgnoreCase))
                throw new IOException("Invalid animation staging directory.");
            Directory.CreateDirectory(stage);
            try
            {
                cancellation.ThrowIfCancellationRequested();validate?.Invoke();
                prepareModel(stage);
                if(!File.Exists(Path.Combine(stage,"Model.dae")))
                    throw new InvalidDataException("The exact target skeleton was not prepared.");
                for(int i=0;i<filenames.Length;i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    IReadOnlyList<PortableRiggedGltf.Clip> clips=onePerFile?new[]{frozen[i]}:frozen;
                    PortableRiggedGltf.WriteClips(stage,clips,includeModel);
                    PortableGlbExport.Verify(stage);
                    File.Move(Path.Combine(stage,"Model.glb"),Path.Combine(stage,filenames[i]));
                    File.Delete(Path.Combine(stage,"Model.gltf"));
                    File.Delete(Path.Combine(stage,"Model.bin"));
                }
                foreach(var path in Directory.GetFiles(stage))
                    if(!filenames.Contains(Path.GetFileName(path),StringComparer.OrdinalIgnoreCase))File.Delete(path);
                var receipt=new List<string>{"Fighterport portable animation export",
                    "Model included: "+includeModel,"Grouping: "+(onePerFile?"one file per clip":"selected clips in one file"),
                    "Format: glTF 2.0 binary (GLB)","Clip count: "+frozen.Length.ToString(CultureInfo.InvariantCulture)};
                for(int i=0;i<frozen.Length;i++)
                    receipt.Add("Clip: "+frozen[i].Name+" | exact identity SHA-256 "+Sha256(frozen[i].Identity)+
                        " | file "+(onePerFile?filenames[i]:filenames[0]));
                foreach(string file in filenames)
                    receipt.Add(file+" SHA-256 "+HashFile(Path.Combine(stage,file)));
                File.WriteAllLines(Path.Combine(stage,"Export-info.txt"),receipt);
                cancellation.ThrowIfCancellationRequested();validate?.Invoke();
                if(File.Exists(destination)||Directory.Exists(destination))
                    throw new IOException("The animation export destination appeared during preparation.");
                Directory.Move(stage,destination);
                return filenames;
            }
            finally
            {
                if(Directory.Exists(stageFull))
                {
                    if(!stageFull.StartsWith(parentPrefix,StringComparison.OrdinalIgnoreCase)||
                        (File.GetAttributes(stageFull)&FileAttributes.ReparsePoint)!=0)
                        throw new IOException("Animation staging directory changed ownership.");
                    Directory.Delete(stageFull,true);
                }
            }
        }
    }
}
