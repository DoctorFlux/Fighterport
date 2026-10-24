using System;
using System.IO;
using System.Linq;
namespace BrawlCrate.Studio
{
    internal sealed class NativeContactOptions
    {
        public string Action {get;set;}public string Bone {get;set;}public int Start {get;set;}public int End {get;set;}public int Blend {get;set;}public int AnchorFrame {get;set;}public float[] Offset {get;set;}public float[] Angles {get;set;}public bool HoldRotation {get;set;}
    }
    public sealed class NativeContactBake
    {
        public string Algorithm {get;set;}public string ModelHash {get;set;}public string BaseHash {get;set;}public string BaseSnapshot {get;set;}public string Options {get;set;}public string Receipt {get;set;}
        internal void Validate()
        {
            if(Algorithm!="reviewed-contact-bake-1"||ModelHash==null||ModelHash.Length!=64||BaseHash==null||BaseHash.Length!=64||string.IsNullOrEmpty(Options)||Options.Length>65536||string.IsNullOrEmpty(Receipt)||Receipt.Length>262144||BaseSnapshot==null||BaseSnapshot.Length>12*1024*1024)throw new InvalidDataException("Invalid saved native contact recipe.");
            var bytes=Convert.FromBase64String(BaseSnapshot);using(var hash=System.Security.Cryptography.SHA256.Create())if(bytes.Length>8*1024*1024||!string.Equals(BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-",""),BaseHash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("The frozen original contact motion was changed.");
        }
    }
}
