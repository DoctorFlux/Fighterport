using BrawlCrate.Studio.Retargeting;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace BrawlCrate.Studio
{
    public sealed class NativeSupportRange
    {
        public string Role {get;set;}
        public string BoneName {get;set;}
        public float First {get;set;}
        public float Last {get;set;}
        public string[] Dependencies {get;set;}
    }
    // Complete original Next support evidence, separate from target recipes.
    // Unavailable evidence is explicit and never reconstructed from counters.
    public sealed class NativeSupportEvidence
    {
        public string Version {get;set;}="original-next-support-context-1";
        public string SourceRig {get;set;}
        public string TargetRig {get;set;}
        public int Frames {get;set;}
        public string InitialResultHash {get;set;}
        public string Json {get;set;}
        public string Sha256 {get;set;}
        public string CanonicalHeader {get;set;}
        public string UnavailableReason {get;set;}
        public NativeSupportRange[] Ranges {get;set;}=new NativeSupportRange[0];
        internal static NativeSupportEvidence Capture(RetargetResult result,string resultHash)
        {
            if(result.FrozenSupportEvidence==null)return CaptureLive(result,resultHash);
            var frozen=result.FrozenSupportEvidence;
            var evidence=new NativeSupportEvidence{SourceRig=frozen.SourceRig,TargetRig=frozen.TargetRig,Frames=frozen.Frames,InitialResultHash=resultHash,
                Json=frozen.Json,UnavailableReason=frozen.UnavailableReason,Ranges=frozen.Ranges.Select(r=>new NativeSupportRange{Role=r.Role,BoneName=r.BoneName,First=r.First,Last=r.Last,Dependencies=r.Dependencies.ToArray()}).ToArray()};
            if(evidence.Json!=null){evidence.CanonicalHeader=evidence.Header();evidence.Sha256=evidence.ComputeHash();}
            evidence.Validate();return evidence;
        }
        internal static void Freeze(RetargetResult result)=>result.FrozenSupportEvidence=CaptureLive(result,new string('0',64));
        static NativeSupportEvidence CaptureLive(RetargetResult result,string resultHash)
        {
            var evidence=new NativeSupportEvidence{SourceRig=result.SourceRig,TargetRig=result.TargetRig,Frames=result.Animation.FrameCount,InitialResultHash=resultHash};
            try
            {
                if(result.Contacts==null)throw new InvalidOperationException("Complete original Next support context is unavailable for this result.");
                evidence.Json=new JavaScriptSerializer{MaxJsonLength=8*1024*1024,RecursionLimit=100}.Serialize(result.Contacts.CaptureOriginalEvidence());
                if(evidence.Json.Length>8*1024*1024)throw new InvalidOperationException("Complete support context exceeds the bounded project review capacity.");
                evidence.Ranges=result.Contacts.CaptureOriginalRanges();
                evidence.CanonicalHeader=evidence.Header();
                evidence.Sha256=evidence.ComputeHash();
            }
            catch(InvalidOperationException error){evidence.Json=null;evidence.CanonicalHeader=null;evidence.Sha256=null;evidence.Ranges=new NativeSupportRange[0];evidence.UnavailableReason=error.Message;}
            evidence.Validate();return evidence;
        }
        internal void Validate()
        {
            bool Hash(string s)=>s!=null&&s.Length==64&&s.All(Uri.IsHexDigit);
            if(Version!="original-next-support-context-1"||!Hash(SourceRig)||!Hash(TargetRig)||!Hash(InitialResultHash)||Frames<1||Frames>4096||Ranges==null||Ranges.Length>20000)
                throw new InvalidDataException("Invalid original support context identity.");
            if(Json==null){if(string.IsNullOrEmpty(UnavailableReason)||Ranges.Length!=0||Sha256!=null||CanonicalHeader!=null)throw new InvalidDataException("Missing complete support context must retain its unavailable reason without invented evidence.");return;}
            if(Json.Length>8*1024*1024||!Hash(Sha256)||UnavailableReason!=null||CanonicalHeader==null||CanonicalHeader.Length>1024*1024||CanonicalHeader!=Header())throw new InvalidDataException("Invalid original support context bounds.");
            if(!string.Equals(ComputeHash(),Sha256,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The original support context changed.");
            foreach(var range in Ranges)if(range==null||string.IsNullOrEmpty(range.Role)||string.IsNullOrEmpty(range.BoneName)||float.IsNaN(range.First)||float.IsInfinity(range.First)||float.IsNaN(range.Last)||float.IsInfinity(range.Last)||
                range.First<0||range.Last<range.First||range.Last>Frames||range.Dependencies==null||range.Dependencies.Length==0||range.Dependencies.Length>4096||range.Dependencies.Any(string.IsNullOrEmpty))
                throw new InvalidDataException("Invalid original support establishment/hold/release evidence.");
        }
        string Header()=>new JavaScriptSerializer{MaxJsonLength=8*1024*1024,RecursionLimit=100}.Serialize(new{Version,SourceRig,TargetRig,Frames,InitialResultHash,Ranges});
        string ComputeHash(){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(CanonicalHeader+"\n"+Json))).Replace("-","").ToLowerInvariant();}
    }
}
