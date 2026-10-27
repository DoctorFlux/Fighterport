using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using BrawlCrate.Studio;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private readonly Dictionary<string,Dictionary<string,string>> _fighterRigMaps=new Dictionary<string,Dictionary<string,string>>();
        private EngineRigReview ReviewedFighterCoordinates(MDL0Node model) =>
            _fighterProject?.RigReviews.FirstOrDefault(r=>r.Fingerprint==EngineRigReview.FingerprintOf(model));
        private System.Numerics.Quaternion? ReviewedFighterCoordinateFrame(MDL0Node model)
        {
            var review=ReviewedFighterCoordinates(model);if(review==null)return null;
            System.Numerics.Vector3 Axis(float[] values)
            {
                if(values==null||values.Length!=3)throw new InvalidDataException("The exact-rig coordinate review is incomplete.");
                return new System.Numerics.Vector3(values[0],values[1],values[2]);
            }
            return RigMath.ReviewedCoordinateFrame(Axis(review.Right),Axis(review.Up),Axis(review.Forward));
        }
        private string ReviewedFighterCoordinateKey(MDL0Node model)
        {
            var review=ReviewedFighterCoordinates(model);if(review==null)return "native-declared-up:+Y";
            return new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new{review.Fingerprint,review.Right,review.Up,review.Forward,review.NativeRoles,review.NativeBones});
        }
        private BrawlRig CaptureFighterRig(MDL0Node model,bool review=false)
        {
            string identity=BrawlRetargeter.SkeletonFingerprint(model);
            var reviewed=ReviewedFighterCoordinates(model);
            if(!_fighterRigMaps.TryGetValue(identity,out Dictionary<string,string> map))
            {
                var asset=_fighterProject?.Animations.LastOrDefault(a=>a.Retarget!=null && string.IsNullOrEmpty(a.Retarget.EngineReceipt) && (a.Retarget.SourceRig==identity || a.RigIdentity==identity));
                var saved=asset?.Retarget;
                if(saved!=null)map=saved.Mapping.ToDictionary(m=>m.Role,m=>saved.SourceRig==identity?m.Source:m.Target);
            }
            if(reviewed?.NativeRoles!=null)map=reviewed.NativeRoles.Zip(reviewed.NativeBones,(r,b)=>new{r,b}).ToDictionary(p=>p.r,p=>p.b);
            SkeletonCharacterization auto=BrawlRetargeter.Characterize(model);
            if(review || (map==null && (auto.Missing.Length>0 || auto.Warnings.Count>0)))map=ReviewFighterSkeleton(model,map,auto);
            BrawlRig captured=BrawlRetargeter.Capture(model,map,coordinateFrame:ReviewedFighterCoordinateFrame(model));
            _fighterRigMaps[identity]=captured.Rig.Roles.ToDictionary(p=>p.Key,p=>captured.Bones[p.Value].Name);
            return captured;
        }
        // Render-time capture never opens a dialog. Confirmed maps survive project
        // restart through the generated asset's source/target mapping provenance.
        private BrawlRig CaptureFighterPreviewRig(MDL0Node model)
        {
            string identity=BrawlRetargeter.SkeletonFingerprint(model);
            var reviewed=ReviewedFighterCoordinates(model);
            if(reviewed?.NativeRoles!=null)return BrawlRetargeter.Capture(model,reviewed.NativeRoles.Zip(reviewed.NativeBones,(r,b)=>new{r,b}).ToDictionary(p=>p.r,p=>p.b),coordinateFrame:ReviewedFighterCoordinateFrame(model));
            if(_fighterRigMaps.TryGetValue(identity,out Dictionary<string,string> map))return BrawlRetargeter.Capture(model,map,coordinateFrame:ReviewedFighterCoordinateFrame(model));
            var saved=_fighterProject?.Animations.LastOrDefault(a=>a.Retarget!=null && string.IsNullOrEmpty(a.Retarget.EngineReceipt) && a.RigIdentity==identity)?.Retarget;
            return BrawlRetargeter.Capture(model,saved?.Mapping.ToDictionary(m=>m.Role,m=>m.Target),coordinateFrame:ReviewedFighterCoordinateFrame(model));
        }
        private Dictionary<string,string> ReviewFighterSkeleton(MDL0Node model,Dictionary<string,string> previous,SkeletonCharacterization auto)
        {
            var bones=model.AllBones.ToArray();
            var proposed=previous??auto.Roles.ToDictionary(p=>p.Key,p=>bones[p.Value].Name);
            using(var dialog=new Form{Text="Skeleton Mapping — "+model.Name,ClientSize=new Size(650,520),StartPosition=FormStartPosition.CenterParent,
                MinimizeBox=false,MaximizeBox=false,FormBorderStyle=FormBorderStyle.FixedDialog,ShowInTaskbar=false})
            using(var grid=new DataGridView{Name="skeletonMapping",Bounds=new Rectangle(12,70,626,390),AllowUserToAddRows=false,AllowUserToDeleteRows=false,
                RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,SelectionMode=DataGridViewSelectionMode.CellSelect})
            {
                grid.Columns.Add(new DataGridViewTextBoxColumn{Name="Role",HeaderText="Body part",ReadOnly=true,FillWeight=32});
                var choices=new DataGridViewComboBoxColumn{Name="Bone",HeaderText="Model bone",FillWeight=42};choices.Items.Add("");choices.Items.AddRange(bones.Select(b=>(object)b.Name).ToArray());grid.Columns.Add(choices);
                grid.Columns.Add(new DataGridViewTextBoxColumn{Name="Evidence",HeaderText="Automatic evidence",ReadOnly=true,FillWeight=46});
                foreach(string role in SkeletonCharacterizer.CoreRoles.Concat(proposed.Keys).Distinct())
                    grid.Rows.Add(role,proposed.TryGetValue(role,out string bone)?bone:"",auto.Evidence.TryGetValue(role,out string evidence)?evidence:"Required / review");
                var status=new Label{Bounds=new Rectangle(12,12,626,54),Text=auto.Warnings.Count>0?string.Join(" ",auto.Warnings.Take(2)):"Automatic mapping from this skeleton. Review joints and left/right; original bones and files are preserved."};
                var apply=new Button{Name="applySkeletonMap",Text="Use Mapping",Bounds=new Rectangle(426,478,108,28)};
                var cancel=new Button{Text="Cancel",DialogResult=DialogResult.Cancel,Bounds=new Rectangle(544,478,94,28)};
                Dictionary<string,string> confirmed=null;
                apply.Click+=(s,e)=>
                {
                    try
                    {
                        grid.EndEdit();
                        var selected=grid.Rows.Cast<DataGridViewRow>().Where(r=>!string.IsNullOrWhiteSpace(Convert.ToString(r.Cells[1].Value)))
                            .ToDictionary(r=>Convert.ToString(r.Cells[0].Value),r=>Convert.ToString(r.Cells[1].Value));
                        BrawlRetargeter.Capture(model,selected);
                        confirmed=selected;dialog.DialogResult=DialogResult.OK;dialog.Close();
                    }
                    catch(Exception error) when(error is InvalidDataException || error is ArgumentException){status.Text=error.Message;}
                };
                dialog.Controls.AddRange(new Control[]{status,grid,apply,cancel});dialog.AcceptButton=apply;dialog.CancelButton=cancel;
                DarkTheme.Apply(dialog);
                if(dialog.ShowDialog(this)!=DialogResult.OK)throw new OperationCanceledException("Skeleton mapping canceled.");
                return confirmed;
            }
        }
        private object EngineBinding(MDL0Node model)=>_fighterProject.RigReviews.FirstOrDefault(r=>r.Fingerprint==EngineRigReview.FingerprintOf(model))?.Wire();
        private bool ReviewEngineCoordinates(MDL0Node model,Dictionary<string,string> nativeRoles)
        {
            string fingerprint=EngineRigReview.FingerprintOf(model);var previous=_fighterProject.RigReviews.FirstOrDefault(r=>r.Fingerprint==fingerprint);
            using(var dialog=new Form{Text="Rig Coordinates — "+model.Name,ClientSize=new Size(470,206),FormBorderStyle=FormBorderStyle.FixedDialog,StartPosition=FormStartPosition.CenterParent,MinimizeBox=false,MaximizeBox=false,ShowInTaskbar=false})
            {
                dialog.Controls.Add(new Label{Text="Confirm the model's right, up and forward directions from its\nexisting pose and geometry. Original native binds stay unchanged.",Bounds=new Rectangle(14,12,440,38)});
                float[][] directions={new[]{1f,0,0},new[]{-1f,0,0},new[]{0f,1,0},new[]{0f,-1,0},new[]{0f,0,1},new[]{0f,0,-1}};string[] labels={"+X","−X","+Y","−Y","+Z","−Z"};
                var inputs=new ComboBox[3];var old=new[]{previous?.Right,previous?.Up,previous?.Forward};
                for(int i=0;i<3;i++){dialog.Controls.Add(new Label{Text=new[]{"Right","Up","Forward"}[i],Bounds=new Rectangle(14,58+i*30,110,24)});var input=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Bounds=new Rectangle(130,55+i*30,320,24)};input.Items.AddRange(labels);int selected=old[i]==null?-1:Array.FindIndex(directions,v=>v.SequenceEqual(old[i]));input.SelectedIndex=selected>=0?selected:i==0?0:i==1?2:4;inputs[i]=input;dialog.Controls.Add(input);}
                var apply=new Button{Text="Use Coordinates",DialogResult=DialogResult.OK,Bounds=new Rectangle(230,166,126,28)};var cancel=new Button{Text="Cancel",DialogResult=DialogResult.Cancel,Bounds=new Rectangle(364,166,86,28)};dialog.Controls.AddRange(new Control[]{apply,cancel});dialog.AcceptButton=apply;dialog.CancelButton=cancel;DarkTheme.Apply(dialog);
                if(dialog.ShowDialog(this)!=DialogResult.OK)return false;
                var axes=inputs.Select(c=>directions[c.SelectedIndex]).ToArray();if(axes.Select(v=>Array.FindIndex(v,x=>x!=0)).Distinct().Count()!=3)throw new ArgumentException("Choose three perpendicular coordinate axes.");
                var roles=new Dictionary<string,string>();var names=new Dictionary<string,string>{{"root","SceneRoot"},{"motion","RootMotion"},{"turn","RootOrientation"},{"pivot","BodyOrientation"},{"pelvis","Hips"},{"spine","Spine"},{"chest","Chest"},{"neck","Neck"},{"head","Head"}};
                foreach(var pair in names)if(nativeRoles.TryGetValue(pair.Key,out var bone))roles[pair.Value]=bone;
                if(!roles.ContainsKey("RootMotion")&&nativeRoles.TryGetValue("root",out var root))roles["RootMotion"]=root;
                foreach(string side in new[]{"left","right"})foreach(var pair in new Dictionary<string,string>{{"upperArm","UpperArm"},{"lowerArm","Elbow"},{"hand","Wrist"},{"upperLeg","UpperLeg"},{"lowerLeg","Knee"},{"foot","Ankle"},{"toe","Toe"}})if(nativeRoles.TryGetValue(side+"."+pair.Key,out var bone))roles[pair.Value+(side=="left"?".L":".R")]=bone;
                _fighterProject.RigReviews.RemoveAll(r=>r.Fingerprint==fingerprint);_fighterProject.RigReviews.Add(new EngineRigReview{Fingerprint=fingerprint,Roles=roles.Keys.ToArray(),Bones=roles.Values.ToArray(),NativeRoles=nativeRoles.Keys.ToArray(),NativeBones=nativeRoles.Values.ToArray(),Right=axes[0],Up=axes[1],Forward=axes[2],Evidence="User reviewed native Skeleton Mapping and explicit file axes against the existing hierarchy/rest/skin geometry; exact native rest revision "+fingerprint});MarkFighterChanged();return true;
            }
        }
    }
}
