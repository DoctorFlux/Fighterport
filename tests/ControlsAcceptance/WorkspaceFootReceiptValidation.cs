using BrawlCrate.Studio;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

internal static partial class Program
{
    // Called with a successfully prepared native floor recipe. Mutations affect
    // detached JSON/recipe copies only, never the live animation or its receipt.
    static void WorkspaceFootReceiptValidation(NativeTransitionFit valid,string windowSide=null)
    {
        valid.Validate();
        var json=new JavaScriptSerializer{MaxJsonLength=32*1024*1024};
        object Get(IDictionary<string,object> node,string name)=>node.First(p=>string.Equals(p.Key,name,StringComparison.OrdinalIgnoreCase)).Value;
        void Set(IDictionary<string,object> node,string name,object value)=>node[node.Keys.First(k=>string.Equals(k,name,StringComparison.OrdinalIgnoreCase))]=value;
        IDictionary<string,object> Region(IDictionary<string,object> floor)=>((IEnumerable)Get(floor,"Regions")).Cast<object>().Cast<IDictionary<string,object>>().First();
        void Reject(string name,Action<IDictionary<string,object>> mutate)
        {
            var root=(IDictionary<string,object>)json.DeserializeObject(valid.Receipt);
            var receipt=(IDictionary<string,object>)Get(root,"Receipt");
            var floor=windowSide==null?(IDictionary<string,object>)Get(receipt,"FloorRecovery"):(IDictionary<string,object>)Get(((IEnumerable)Get(receipt,"RecoveryWindows")).Cast<IDictionary<string,object>>().Single(w=>Convert.ToString(Get(w,"Side"))==windowSide),"Floor");mutate(floor);
            var copy=new NativeTransitionFit{Algorithm=valid.Algorithm,ModelSha256=valid.ModelSha256,InitialResultSha256=valid.InitialResultSha256,Options=valid.Options,Receipt=json.Serialize(root),Inputs=valid.Inputs};
            bool rejected=false;try{copy.Validate();}catch(InvalidDataException){rejected=true;}
            Check(rejected,"Saved floor receipt rejects "+name);
        }
        Reject("an internal algorithm/version mismatch",floor=>Set(floor,"Version","contact-exit-recovery-2"));
        Reject("a missing geometry hash",floor=>Set(floor,"GeometryHash",null));
        Reject("a short geometry hash",floor=>Set(floor,"GeometryHash","abc"));
        Reject("a nonhex geometry hash",floor=>Set(floor,"GeometryHash",new string('z',64)));
        foreach(string field in new[]{"MaximumAddedPenetration","MaximumLift","LiftLimit","AllowedAddedPenetration"})
            Reject("nonnumeric / nonfinite "+field,floor=>Set(floor,field,"NaN"));
        Reject("negative added penetration",floor=>Set(floor,"MaximumAddedPenetration",-.00001));
        Reject("added penetration beyond the frozen gate",floor=>Set(floor,"MaximumAddedPenetration",.00020001));
        Reject("negative lift",floor=>Set(floor,"MaximumLift",-.00001));
        Reject("zero lift limit",floor=>Set(floor,"LiftLimit",0));
        Reject("lift beyond its recorded budget",floor=>Set(floor,"MaximumLift",Convert.ToDouble(Get(floor,"LiftLimit"))+.001));
        Reject("a changed added-penetration tolerance",floor=>Set(floor,"AllowedAddedPenetration",.00021));
        foreach(var pair in new[]{Tuple.Create("Evaluations",200000),Tuple.Create("ValidationSamples",200000),Tuple.Create("KnotCount",8192)})
        {
            Reject("negative "+pair.Item1,floor=>Set(floor,pair.Item1,-1));
            Reject("fractional "+pair.Item1,floor=>Set(floor,pair.Item1,1.5));
            Reject("over-budget "+pair.Item1,floor=>Set(floor,pair.Item1,pair.Item2+1));
            Reject("missing "+pair.Item1,floor=>Set(floor,pair.Item1,null));
        }
        Reject("zero pose evaluations",floor=>Set(floor,"Evaluations",0));
        Reject("zero validation samples",floor=>Set(floor,"ValidationSamples",0));
        Reject("missing foot regions",floor=>Set(floor,"Regions",null));
        Reject("empty foot regions",floor=>Set(floor,"Regions",new object[0]));
        Reject("a missing reviewed foot region",floor=>Set(floor,"Regions",new object[]{Region(floor)}));
        Reject("a null foot region",floor=>Set(floor,"Regions",new object[]{null}));
        Reject("duplicate reviewed foot identities",floor=>Set(floor,"Regions",new object[]{Region(floor),Region(floor)}));
        Reject("an unsupported region role",floor=>Set(Region(floor),"Role","Hand.L"));
        Reject("a region outside the reviewed body mask",floor=>Set(Region(floor),"Bone","unreviewed detached fixture bone"));
        Reject("an empty full foot region",floor=>Set(Region(floor),"PointCount",0));
        Reject("negative legacy region membership",floor=>Set(Region(floor),"LegacyPointCount",-1));
        Reject("legacy membership exceeding complete geometry",floor=>Set(Region(floor),"LegacyPointCount",Convert.ToInt32(Get(Region(floor),"PointCount"))+1));
        foreach(string field in new[]{"MinimumSource","MinimumReference","MinimumBefore","MinimumAfter","MaximumAddedPenetration"})
            Reject("invalid region "+field,floor=>Set(Region(floor),field,"NaN"));
        Reject("negative regional added penetration",floor=>Set(Region(floor),"MaximumAddedPenetration",-.00001));
        Reject("regional penetration above the recorded maximum",floor=>Set(Region(floor),"MaximumAddedPenetration",Convert.ToDouble(Get(floor,"MaximumAddedPenetration"))+.00001));
        Reject("a coerced floor-plane height",floor=>Set((IDictionary<string,object>)Get(floor,"Plane"),"Height","0"));
        Reject("a coerced plane-review boolean",floor=>Set((IDictionary<string,object>)Get(floor,"Plane"),"Reviewed","true"));
        valid.Validate();
        Check(true,"Detached floor-receipt mutation checks preserve the valid live recipe, including legitimate zero added penetration");
    }
}
