using BrawlCrate.Studio;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class Program
{
    // Explicit fixture-assisted review through the normal metadata dialog.
    // The fixture supplies semantic bindings, never numerical solver settings.
    static async Task ReviewedFittingRoleFixture(ModelForm form,MDL0Node model,AuthenticFloatDenseFixture fixture,string output)
    {
        var setup=await Await<ControlReply>(Call(form,"LoadControls"));
        var roles=fixture.FittingRoles;string[] path={"SceneRoot","RootMotion","BodyOrientation","RootOrientation","Hips"};
        Check(!string.IsNullOrWhiteSpace(fixture.FittingBindingEvidence)&&roles.Count==path.Length&&path.All(roles.ContainsKey)&&roles.Values.Distinct(StringComparer.Ordinal).Count()==path.Length,
            "Assisted fitting fixture explicitly supplies complete distinct ancestor/body roles and evidence");
        var bones=path.Select(role=>model.AllBones.Single(b=>b.Name==roles[role])).ToArray();
        Check(!(bones[0].Parent is MDL0BoneNode)&&Enumerable.Range(1,bones.Length-1).All(i=>ReferenceEquals(bones[i].Parent,bones[i-1]))&&
            setup.BodyBone.HasValue&&setup.BodyBone.Value>=0&&setup.BoneNames[setup.BodyBone.Value]==bones.Last().Name&&
            setup.ProtectedMotionBones.Select(i=>setup.BoneNames[i]).OrderBy(n=>n,StringComparer.Ordinal).SequenceEqual(bones.Take(bones.Length-1).Select(b=>b.Name).OrderBy(n=>n,StringComparer.Ordinal)),
            "Assisted native role fixture proves the exact protected ancestor path to the structurally resolved body, without changing bones or axes");
        string modelBefore=(string)Call(form,"NativeControlHash",model,".mdl0");
        form.ControlReviewReady=dialog=>{
            SimpleControl<CheckBox>(dialog,"reviewApplyIk").Checked=false;
            dialog.Controls.OfType<TabControl>().Single().SelectedIndex=1;
            var grid=SimpleControl<DataGridView>(dialog,"reviewSharedBones");
            foreach(var pair in roles)
            {
                var row=grid.Rows.Cast<DataGridViewRow>().SingleOrDefault(r=>(string)r.Cells["Role"].Value==pair.Key);
                if(row==null){SimpleControl<TextBox>(dialog,"reviewNewRole").Text=pair.Key;SimpleControl<Button>(dialog,"reviewAddRole").PerformClick();row=grid.Rows.Cast<DataGridViewRow>().Single(r=>(string)r.Cells["Role"].Value==pair.Key);}
                row.Cells["Bone"].Value=Array.IndexOf(setup.BoneNames,pair.Value);row.Cells["Confirm"].Value=true;
            }
            SimpleControl<Button>(dialog,"reviewSave").PerformClick();
        };
        try{await (Task)Call(form,"ReviewControlSetup");}finally{form.ControlReviewReady=null;}
        if(fixture.FittingFootChains!=null)
        {
            Check(fixture.FittingFootChains.Count==2&&new[]{"Foot.L","Foot.R"}.All(fixture.FittingFootChains.ContainsKey),"Assisted floor fixture explicitly identifies both exact native foot chains");
            foreach(var pair in fixture.FittingFootChains)
            {
                var chain=setup.Chains.Single(c=>c.Role==pair.Key);
                Check(new[]{chain.Root,chain.Middle,chain.Effector}.Select(i=>setup.BoneNames[i]).SequenceEqual(pair.Value)&&pair.Value.Length==3,
                    "Explicit foot fixture agrees with the complete existing native structural proposal");
                form.ControlReviewReady=dialog=>{
                    SimpleControl<ComboBox>(dialog,"reviewIkRole").SelectedItem=pair.Key;
                    SimpleControl<CheckBox>(dialog,"reviewApplyIk").Checked=true;
                    SimpleControl<Button>(dialog,"reviewSave").PerformClick();
                };
                try{await (Task)Call(form,"ReviewControlSetup");}finally{form.ControlReviewReady=null;}
            }
        }
        var reviewed=await Await<ControlReply>(Call(form,"LoadControls"));
        Check(roles.All(pair=>reviewed.Roles.Any(r=>r.Reviewed&&r.Role==pair.Key&&r.Bone.HasValue&&reviewed.BoneNames[r.Bone.Value]==pair.Value))&&
            (string)Call(form,"NativeControlHash",model,".mdl0")==modelBefore,
            "Normal control-role review stores only the declared fixture metadata and preserves exact native hierarchy, binds and every skin weight");
        File.WriteAllText(Path.Combine(output,"assisted-fitting-role-review.json"),new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new{Fixture=fixture,HumanReviewClaimed=false,ChangedNativeModel=false,AxesOverride=false}));
    }
}
