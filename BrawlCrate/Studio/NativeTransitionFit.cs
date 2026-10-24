using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace BrawlCrate.Studio
{
    internal sealed class TransitionClipChoice
    {
        internal FighterAsset Asset;internal CHR0Node Clip;internal string Key;
        public override string ToString()=>Clip.Name+" · "+Path.GetFileName(Asset.SourcePath);
    }
    internal sealed class TransitionFitOptions
    {
        public string Name {get;set;}public string Entry {get;set;}public string Exit {get;set;}
        public double EntryFrame {get;set;}public double ExitFrame {get;set;}
        public bool EntryFrozen {get;set;}public bool ExitFrozen {get;set;}public double EntryRate {get;set;}public double ExitRate {get;set;}
        public int EntrySamples {get;set;}public int ExitSamples {get;set;}public int Components {get;set;}
        public string[] Bones {get;set;}public TransitionProtected[] Protected {get;set;}public bool PreserveRootTranslation {get;set;}
        public bool NearestEquivalentAngles {get;set;}
        public bool BindingReviewed {get;set;}
        public bool PreserveContacts {get;set;}
    }
    public sealed class TransitionProtected {public double Start {get;set;}public double End {get;set;}public string Reason {get;set;}="User protected action";}
    public sealed class TransitionNativeInput
    {
        public string Role {get;set;}public string AssetId {get;set;}public string Entry {get;set;}public string Sha256 {get;set;}public string Snapshot {get;set;}
        public string Name {get;set;}public int Samples {get;set;}
    }
    public sealed class NativeTransitionFit
    {
        public string Algorithm {get;set;}public string ModelSha256 {get;set;}public string Options {get;set;}public string Receipt {get;set;}
        public string InitialResultSha256 {get;set;}
        public TransitionNativeInput[] Inputs {get;set;}
        internal void Validate()
        {
            bool Hash(string s)=>s!=null&&s.Length==64&&s.All(Uri.IsHexDigit);
            if((Algorithm!="fixed-duration-fk-hermite-1"&&Algorithm!="contact-trajectory-preserve-1")||!Hash(ModelSha256)||!Hash(InitialResultSha256)||string.IsNullOrEmpty(Options)||Options.Length>65536||string.IsNullOrEmpty(Receipt)||Receipt.Length>1024*1024||Inputs==null||Inputs.Length<2||Inputs.Length>3||Inputs.Any(i=>i==null)||Inputs.Select(i=>i.Role).Distinct().Count()!=Inputs.Length)throw new InvalidDataException("Invalid native transition recipe.");
            long bytes=0;foreach(var input in Inputs)
            {
                if(input==null||!new[]{"source","entry","exit"}.Contains(input.Role)||string.IsNullOrEmpty(input.Name)||input.Name.Length>256||input.Samples<1||input.Samples>100000||string.IsNullOrEmpty(input.AssetId)||input.AssetId.Length>128||string.IsNullOrEmpty(input.Entry)||input.Entry.Length>16384||!Hash(input.Sha256)||string.IsNullOrEmpty(input.Snapshot)||input.Snapshot.Length>12*1024*1024)throw new InvalidDataException("Invalid frozen transition reference.");
                byte[] data;try{data=Convert.FromBase64String(input.Snapshot);}catch(FormatException error){throw new InvalidDataException("A frozen native transition reference is damaged.",error);}bytes+=data.Length;if(bytes>8*1024*1024)throw new InvalidDataException("Frozen transition inputs exceed the 8 MiB review budget. Use a smaller native clip.");
                using(var sha=SHA256.Create())if(!string.Equals(BitConverter.ToString(sha.ComputeHash(data)).Replace("-",""),input.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("A frozen transition reference was changed.");
            }
            if(!Inputs.Any(i=>i.Role=="source"))throw new InvalidDataException("Missing authentic transition source snapshot.");
        }
    }
}
