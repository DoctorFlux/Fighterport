using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections;
using System.Collections.Generic;
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
        public bool MatchReferenceControls {get;set;}
        public bool BindingReviewed {get;set;}
        public bool PreserveContacts {get;set;}
        public bool ExitContactRecovery {get;set;}
        public bool PreventFootDipping {get;set;}
        public double FloorHeight {get;set;}
        public bool FloorReviewed {get;set;}
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
            bool floor=Algorithm=="contact-floor-recovery-1"||Algorithm=="contact-floor-recovery-2";
            bool recovery=Algorithm=="contact-exit-recovery-1"||Algorithm=="contact-exit-recovery-2"||Algorithm=="contact-exit-recovery-3"||floor;
            if((Algorithm!="fixed-duration-fk-hermite-1"&&Algorithm!="fixed-duration-fk-hermite-2"&&Algorithm!="contact-trajectory-preserve-1"&&Algorithm!="contact-trajectory-preserve-2"&&!recovery)||!Hash(ModelSha256)||!Hash(InitialResultSha256)||string.IsNullOrEmpty(Options)||Options.Length>65536||string.IsNullOrEmpty(Receipt)||Receipt.Length>1024*1024||Inputs==null||Inputs.Length<2||Inputs.Length>3||Inputs.Any(i=>i==null)||Inputs.Select(i=>i.Role).Distinct().Count()!=Inputs.Length)throw new InvalidDataException("Invalid native transition recipe.");
            var settings=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<TransitionFitOptions>(Options);
            if(settings==null||settings.ExitContactRecovery!=recovery||settings.ExitContactRecovery&&(!settings.PreserveContacts||settings.Entry!=null||settings.Exit==null||!settings.ExitFrozen||settings.ExitFrame!=0||settings.Components!=7)||settings.PreventFootDipping!=floor)throw new InvalidDataException("Invalid saved exit contact recovery policy.");
            if(new[]{"fixed-duration-fk-hermite-2","contact-trajectory-preserve-2","contact-exit-recovery-3","contact-floor-recovery-2"}.Contains(Algorithm))
            {
                var savedSettings=ReceiptValue(SavedReceipt(),"Settings") as IDictionary<string,object>;
                if(!(ReceiptValue(savedSettings,"MatchReferenceControls") is bool enabled)||enabled!=settings.MatchReferenceControls)throw new InvalidDataException("Saved reference control options and receipt disagree.");
            }
            if(Algorithm=="contact-exit-recovery-2"||Algorithm=="contact-exit-recovery-3"||floor||settings.MatchReferenceControls)
            {
                var receipt=SavedReceipt();
                string policy=settings.MatchReferenceControls?"reference controls with source-authored motion-position control retained":"authentic reference body under source-authored protected locals at each time";
                if(settings.MatchReferenceControls)
                {
                    if(Algorithm=="fixed-duration-fk-hermite-1"||Algorithm=="contact-trajectory-preserve-1"||Algorithm=="contact-exit-recovery-1"||Algorithm=="contact-exit-recovery-2"||Algorithm=="contact-floor-recovery-1")throw new InvalidDataException("Legacy recipes cannot assert reference control fitting.");
                    var matched=(ReceiptValue(receipt,"MatchedReferenceControls") as IEnumerable)?.Cast<object>().Select(Convert.ToString).ToArray();
                    var savedSettings=ReceiptValue(receipt,"Settings") as IDictionary<string,object>;
                    if(!(ReceiptValue(savedSettings,"MatchReferenceControls") is bool enabled)||!enabled||matched==null||matched.Length==0||matched.Length>3||matched.Distinct(StringComparer.Ordinal).Count()!=matched.Length||settings.Bones==null||matched.Any(name=>string.IsNullOrWhiteSpace(name)||!settings.Bones.Contains(name,StringComparer.Ordinal)))throw new InvalidDataException("Invalid saved reference control fitting policy.");
                }
                if(!string.Equals(Convert.ToString(ReceiptValue(receipt,"Version")),Algorithm,StringComparison.Ordinal)||!string.Equals(Convert.ToString(ReceiptValue(receipt,"ReferenceRootPolicy")),policy,StringComparison.Ordinal))throw new InvalidDataException("The saved recovery receipt does not retain the source-authored root placement policy.");
            }
            if(settings.PreventFootDipping)
            {
                if(!settings.FloorReviewed||double.IsNaN(settings.FloorHeight)||double.IsInfinity(settings.FloorHeight)||float.IsInfinity((float)settings.FloorHeight))throw new InvalidDataException("The saved floor recovery requires an explicit finite reviewed plane and authentic penetration baseline.");
                ValidateFloorReceipt(settings);
            }
            long bytes=0;foreach(var input in Inputs)
            {
                if(input==null||!new[]{"source","entry","exit"}.Contains(input.Role)||string.IsNullOrEmpty(input.Name)||input.Name.Length>256||input.Samples<1||input.Samples>100000||string.IsNullOrEmpty(input.AssetId)||input.AssetId.Length>128||string.IsNullOrEmpty(input.Entry)||input.Entry.Length>16384||!Hash(input.Sha256)||string.IsNullOrEmpty(input.Snapshot)||input.Snapshot.Length>12*1024*1024)throw new InvalidDataException("Invalid frozen transition reference.");
                byte[] data;try{data=Convert.FromBase64String(input.Snapshot);}catch(FormatException error){throw new InvalidDataException("A frozen native transition reference is damaged.",error);}bytes+=data.Length;if(bytes>8*1024*1024)throw new InvalidDataException("Frozen transition inputs exceed the 8 MiB review budget. Use a smaller native clip.");
                using(var sha=SHA256.Create())if(!string.Equals(BitConverter.ToString(sha.ComputeHash(data)).Replace("-",""),input.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("A frozen transition reference was changed.");
            }
            if(!Inputs.Any(i=>i.Role=="source"))throw new InvalidDataException("Missing authentic transition source snapshot.");
        }
        private void ValidateFloorReceipt(TransitionFitOptions settings)
        {
            object Get(IDictionary<string,object> node,string name)=>ReceiptValue(node,name);
            InvalidDataException Invalid()=>new InvalidDataException("The saved floor verification has invalid geometry, limits or region measurements.");
            double Numeric(object value)
            {
                if(!(value is byte||value is sbyte||value is short||value is ushort||value is int||value is uint||value is long||value is ulong||value is float||value is double||value is decimal))throw Invalid();
                double number=Convert.ToDouble(value);if(double.IsNaN(number)||double.IsInfinity(number))throw Invalid();return number;
            }
            double Number(IDictionary<string,object> node,string name)=>Numeric(Get(node,name));
            int Count(IDictionary<string,object> node,string name,int maximum)
            {double number=Number(node,name);if(number<0||number>maximum||number!=Math.Truncate(number))throw Invalid();return (int)number;}
            var receipt=SavedReceipt();
            var floor=Get(receipt,"FloorRecovery") as IDictionary<string,object>;
            var plane=Get(floor,"Plane") as IDictionary<string,object>;
            var normal=Get(plane,"Normal") as IEnumerable;
            double[] components=normal?.Cast<object>().Select(Numeric).ToArray();
            string evidence=Get(plane,"Evidence") as string;
            if(!string.Equals(Convert.ToString(Get(receipt,"Version")),Algorithm,StringComparison.Ordinal)||!string.Equals(Convert.ToString(Get(floor,"BaselinePolicy")),settings.MatchReferenceControls?"per-region min(0, original, authentic reference under retained motion-position control)":"per-region min(0, original, authentic reference body under source-root placement)",StringComparison.Ordinal)||plane==null||components==null||components.Length!=3||components[0]!=0||components[1]!=1||components[2]!=0||(float)Number(plane,"Height")!=(float)settings.FloorHeight||!(Get(plane,"Reviewed") is bool reviewed)||!reviewed||string.IsNullOrWhiteSpace(evidence)||evidence.Length>2048)throw new InvalidDataException("The saved floor receipt does not match its reviewed native Y plane and recovery policy.");
            string geometry=Convert.ToString(Get(floor,"GeometryHash"));
            if(!string.Equals(Convert.ToString(Get(floor,"Version")),Algorithm,StringComparison.Ordinal)||geometry==null||geometry.Length!=64||!geometry.All(Uri.IsHexDigit))throw Invalid();
            double added=Number(floor,"MaximumAddedPenetration"),lift=Number(floor,"MaximumLift"),limit=Number(floor,"LiftLimit");
            if(added<0||added>.0002||lift<0||limit<=0||lift>limit||Number(floor,"AllowedAddedPenetration")!=.0002)throw Invalid();
            if(Count(floor,"Evaluations",200000)==0||Count(floor,"ValidationSamples",200000)==0)throw Invalid();Count(floor,"KnotCount",8192);
            var regions=(Get(floor,"Regions") as IEnumerable)?.Cast<object>().Select(value=>value as IDictionary<string,object>).ToArray();
            if(regions==null||regions.Length!=2||regions.Any(region=>region==null)||regions.Select(region=>Convert.ToString(Get(region,"Role"))).Distinct(StringComparer.Ordinal).Count()!=regions.Length)throw Invalid();
            foreach(var region in regions)
            {
                string role=Convert.ToString(Get(region,"Role")),bone=Convert.ToString(Get(region,"Bone"));
                if((role!="Foot.L"&&role!="Foot.R")||string.IsNullOrWhiteSpace(bone)||settings.Bones==null||!settings.Bones.Contains(bone,StringComparer.Ordinal))throw Invalid();
                int points=Count(region,"PointCount",200000000),legacy=Count(region,"LegacyPointCount",200000000);
                if(points==0||legacy>points)throw Invalid();
                foreach(string name in new[]{"MinimumSource","MinimumReference","MinimumBefore","MinimumAfter"})Number(region,name);
                double regionAdded=Number(region,"MaximumAddedPenetration");if(regionAdded<0||regionAdded>added)throw Invalid();
            }
        }
        private static object ReceiptValue(IDictionary<string,object> node,string name)=>node?.FirstOrDefault(pair=>string.Equals(pair.Key,name,StringComparison.OrdinalIgnoreCase)).Value;
        private IDictionary<string,object> SavedReceipt()
        {
            var root=new System.Web.Script.Serialization.JavaScriptSerializer{MaxJsonLength=1024*1024}.DeserializeObject(Receipt) as IDictionary<string,object>;
            return ReceiptValue(root,"Receipt") as IDictionary<string,object>;
        }
    }
}
