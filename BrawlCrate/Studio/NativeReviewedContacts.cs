using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace BrawlCrate.Studio
{
    public sealed class NativeReviewedContacts
    {
        public string Algorithm {get;set;}="reviewed-contact-set-1";
        public string ModelHash {get;set;}
        public string BaseHash {get;set;}
        public string BaseSnapshot {get;set;}
        public string OriginalState {get;set;}
        public string Context {get;set;}
        public string Receipt {get;set;}
        public string InitialResultHash {get;set;}
        internal void Validate()
        {
            bool Hash(string s)=>s!=null&&s.Length==64&&s.All(c=>Uri.IsHexDigit(c));
            if(Algorithm!="reviewed-contact-set-1"||!Hash(ModelHash)||!Hash(BaseHash)||!Hash(InitialResultHash)||
                BaseSnapshot==null||BaseSnapshot.Length>12*1024*1024||string.IsNullOrEmpty(OriginalState)||OriginalState.Length>8*1024*1024||
                string.IsNullOrEmpty(Context)||Context.Length>8*1024*1024||string.IsNullOrEmpty(Receipt)||Receipt.Length>1024*1024)
                throw new InvalidDataException("Invalid complete reviewed-contact recipe.");
            var bytes=Convert.FromBase64String(BaseSnapshot);
            using(var sha=SHA256.Create())if(bytes.Length>8*1024*1024||!string.Equals(BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-",""),BaseHash,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The original reviewed-contact animation snapshot changed.");
        }
    }
    public sealed class NativeClipReviewedContacts
    {
        public string Entry {get;set;}
        public NativeReviewedContacts Recipe {get;set;}
    }
    internal sealed class ReviewedContactRow
    {
        public string Id {get;set;} public string Role {get;set;} public int Bone {get;set;}
        public int Start {get;set;} public int End {get;set;} public int Blend {get;set;} public int AnchorFrame {get;set;}
        public float[] Anchor {get;set;} public bool Reviewed {get;set;}
    }
    internal sealed class ReviewedContactEdit
    {
        public string Id {get;set;} public string Role {get;set;}
        public int Start {get;set;} public int End {get;set;} public int Blend {get;set;} public int AnchorFrame {get;set;}
        public float[] Offset {get;set;}=new float[3];public bool Reviewed {get;set;}public bool Remove {get;set;}
    }
}
