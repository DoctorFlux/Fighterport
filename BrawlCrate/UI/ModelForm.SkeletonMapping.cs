using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using BrawlCrate.Studio;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private readonly Dictionary<string,Dictionary<string,string>> _fighterRigMaps=new Dictionary<string,Dictionary<string,string>>();
        // This anatomical review belongs to the exact native rig and skin in
        // this session. Capture rechecks its digest after snapshot/export; it
        // is never replayed from an old project or a similarly named model.
        private readonly Dictionary<string,CranialAnchorReview> _fighterCranialReviews=new Dictionary<string,CranialAnchorReview>();
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
            var review=ReviewedFighterCoordinates(model);
            _fighterCranialReviews.TryGetValue(BrawlRetargeter.SkeletonFingerprint(model),out CranialAnchorReview cranial);
            if(review==null)return "native-declared-up:+Y;cranial:"+(cranial?.RigAndSkinDigest??"none");
            return new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new{review.Fingerprint,review.Right,review.Up,review.Forward,review.NativeRoles,review.NativeBones,
                CranialSkinDigest=cranial?.RigAndSkinDigest,CranialNeck=cranial?.NeckBoneName});
        }
        private static Dictionary<string,string> ReusableRetargetRoles(RetargetProvenance saved,bool source)
        {
            if(saved?.Mapping==null)return null;
            // Exact-copy detail mappings are many-to-many storage evidence, not
            // anatomical review. Never feed them to a role dictionary. Conflicts
            // in historical semantic mappings require a new explicit review.
            var groups=saved.Mapping.Where(m=>m!=null&&!string.IsNullOrEmpty(m.Role)&&!m.Role.StartsWith("detail.",StringComparison.Ordinal)&&
                !m.Role.StartsWith("trajectory.",StringComparison.Ordinal)).GroupBy(m=>m.Role).ToArray();
            if(groups.Any(group=>group.Select(m=>source?m.Source:m.Target).Distinct(StringComparer.Ordinal).Count()!=1))return null;
            return groups.ToDictionary(group=>group.Key,group=>source?group.First().Source:group.First().Target);
        }
        private BrawlRig CaptureFighterRig(MDL0Node model,bool review=false,bool allowProvisionalExactCopy=false)
        {
            string identity=BrawlRetargeter.SkeletonFingerprint(model);
            var reviewed=ReviewedFighterCoordinates(model);
            SkeletonCharacterization auto=BrawlRetargeter.Characterize(model);
            _fighterCranialReviews.TryGetValue(identity,out CranialAnchorReview cranialReview);
            if(!_fighterRigMaps.TryGetValue(identity,out Dictionary<string,string> map))
            {
                var asset=_fighterProject?.Animations.LastOrDefault(a=>a.Retarget!=null && string.IsNullOrEmpty(a.Retarget.EngineReceipt) && (a.Retarget.SourceRig==identity || a.RigIdentity==identity));
                var saved=asset?.Retarget;
                if(saved!=null)map=ReusableRetargetRoles(saved,saved.SourceRig==identity);
            }
            if(reviewed?.NativeRoles!=null)map=reviewed.NativeRoles.Zip(reviewed.NativeBones,(r,b)=>new{r,b}).ToDictionary(p=>p.r,p=>p.b);
            // Explicit session review supersedes old head-as-appendage
            // provenance. Capture must still verify its exact native skin hash.
            if(cranialReview!=null)
            {
                map=map==null?auto.Roles.ToDictionary(p=>p.Key,p=>model.AllBones[p.Value].Name):new Dictionary<string,string>(map);
                map.Remove("head");
                map["neck"]=cranialReview.NeckBoneName;
            }
            if(map!=null&&cranialReview==null&&(!map.TryGetValue("head",out string mappedHead)||
                auto.ProvisionalHead.HasValue&&mappedHead==model.AllBones[auto.ProvisionalHead.Value].Name))map=null;
            bool provisionalCopy=cranialReview==null&&allowProvisionalExactCopy&&map==null&&auto.ProvisionalHead.HasValue&&
                auto.Missing.Length==1&&auto.Missing[0]=="head";
            if(review || (!provisionalCopy&&map==null && (auto.Missing.Length>0 || auto.Warnings.Count>0)))
            {
                map=ReviewFighterSkeleton(model,map,auto);
                _fighterCranialReviews.TryGetValue(identity,out cranialReview);
            }
            BrawlRig captured=BrawlRetargeter.Capture(model,map,coordinateFrame:ReviewedFighterCoordinateFrame(model),cranialReview:cranialReview);
            if(!captured.ProvisionalHeadForExactNativeCopy)
                _fighterRigMaps[identity]=captured.Rig.Roles.ToDictionary(p=>p.Key,p=>captured.Bones[p.Value].Name);
            return captured;
        }
        // Render-time capture never opens a dialog. Confirmed maps survive project
        // restart through the generated asset's source/target mapping provenance.
        private BrawlRig CaptureFighterPreviewRig(MDL0Node model)
        {
            string identity=BrawlRetargeter.SkeletonFingerprint(model);
            var reviewed=ReviewedFighterCoordinates(model);
            _fighterCranialReviews.TryGetValue(identity,out CranialAnchorReview cranialReview);
            if(reviewed?.NativeRoles!=null)
            {
                var reviewedMap=reviewed.NativeRoles.Zip(reviewed.NativeBones,(r,b)=>new{r,b}).ToDictionary(p=>p.r,p=>p.b);
                if(cranialReview!=null){reviewedMap.Remove("head");reviewedMap["neck"]=cranialReview.NeckBoneName;}
                return BrawlRetargeter.Capture(model,reviewedMap,coordinateFrame:ReviewedFighterCoordinateFrame(model),cranialReview:cranialReview);
            }
            if(_fighterRigMaps.TryGetValue(identity,out Dictionary<string,string> map))return BrawlRetargeter.Capture(model,map,coordinateFrame:ReviewedFighterCoordinateFrame(model),cranialReview:cranialReview);
            var saved=_fighterProject?.Animations.LastOrDefault(a=>a.Retarget!=null && string.IsNullOrEmpty(a.Retarget.EngineReceipt) && a.RigIdentity==identity)?.Retarget;
            var savedMap=ReusableRetargetRoles(saved,false);
            if(cranialReview!=null)
            {
                if(savedMap==null)
                {
                    var auto=BrawlRetargeter.Characterize(model);
                    savedMap=auto.Roles.ToDictionary(p=>p.Key,p=>model.AllBones[p.Value].Name);
                }
                savedMap.Remove("head");
                savedMap["neck"]=cranialReview.NeckBoneName;
            }
            if(savedMap!=null&&!savedMap.ContainsKey("head")&&cranialReview==null)savedMap=null;
            return BrawlRetargeter.Capture(model,savedMap,coordinateFrame:ReviewedFighterCoordinateFrame(model),cranialReview:cranialReview);
        }
        private Dictionary<string,string> ReviewFighterSkeleton(MDL0Node model,Dictionary<string,string> previous,SkeletonCharacterization auto)
        {
            var bones=model.AllBones.ToArray();
            var proposed=previous??auto.Roles.ToDictionary(p=>p.Key,p=>bones[p.Value].Name);
            string identity=BrawlRetargeter.SkeletonFingerprint(model);
            CranialAnchorProposal cranial=null;
            string cranialFailure=null;
            bool singleCranialJoint=false;
            try
            {
                if(!auto.Roles.ContainsKey("head")&&auto.Roles.ContainsKey("neck"))
                    cranial=BrawlRetargeter.ProposeCranialAnchor(model,auto);
                else if(!auto.Roles.ContainsKey("neck")&&auto.Roles.TryGetValue("head",out int head) &&
                    auto.Roles.TryGetValue("chest",out int chest) && head!=chest &&
                    ReferenceEquals(bones[head].Parent,bones[chest]))
                {
                    // A single central cranial joint can carry both semantic
                    // roles, but only one native transform. Require explicit
                    // skin review; never synthesize a second joint or map an
                    // appendage by its name or height.
                    var candidate=new SkeletonCharacterization();candidate.Roles.Add("neck",head);
                    cranial=BrawlRetargeter.ProposeCranialAnchor(model,candidate);
                    singleCranialJoint=cranial.Available;
                }
            }
            catch(Exception error) when(error is InvalidDataException || error is ArgumentException || error is InvalidOperationException)
            {cranialFailure=error.Message;}
            _fighterCranialReviews.TryGetValue(identity,out CranialAnchorReview priorCranial);
            bool sameCranial=cranial?.Available==true && priorCranial!=null &&
                string.Equals(priorCranial.RigAndSkinDigest,cranial.RigAndSkinDigest,StringComparison.Ordinal) &&
                string.Equals(priorCranial.NeckBoneName,cranial.NeckBoneName,StringComparison.Ordinal);
            using(var dialog=new Form{Text="Skeleton Mapping — "+model.Name,ClientSize=new Size(650,572),StartPosition=FormStartPosition.CenterParent,
                MinimizeBox=false,MaximizeBox=false,FormBorderStyle=FormBorderStyle.FixedDialog,ShowInTaskbar=false})
            using(var grid=new DataGridView{Name="skeletonMapping",Bounds=new Rectangle(12,70,626,358),AllowUserToAddRows=false,AllowUserToDeleteRows=false,
                RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,SelectionMode=DataGridViewSelectionMode.CellSelect})
            {
                grid.Columns.Add(new DataGridViewTextBoxColumn{Name="Role",HeaderText="Body part",ReadOnly=true,FillWeight=32});
                var choices=new DataGridViewComboBoxColumn{Name="Bone",HeaderText="Model bone",FillWeight=42};choices.Items.Add("");choices.Items.AddRange(bones.Select(b=>(object)b.Name).ToArray());grid.Columns.Add(choices);
                grid.Columns.Add(new DataGridViewTextBoxColumn{Name="Evidence",HeaderText="Automatic evidence",ReadOnly=true,FillWeight=46});
                foreach(string role in SkeletonCharacterizer.CoreRoles.Concat(proposed.Keys).Distinct())
                    grid.Rows.Add(role,proposed.TryGetValue(role,out string bone)?bone:"",auto.Evidence.TryGetValue(role,out string evidence)?evidence:"Required / review");
                string initialStatus=auto.Warnings.Count>0?string.Join(" ",auto.Warnings.Take(2)):"Automatic mapping from this skeleton. Review joints and left/right; original bones and files are preserved.";
                var status=new Label{Bounds=new Rectangle(12,12,626,54),Text=initialStatus};
                string cranialSummary=cranial?.Available==true
                    ? (singleCranialJoint?"One central cranial joint can supply neck and head surface with one transform. ":"Neck skin can supply a head surface without another bone. ")+
                        "Inspect the original source preview; close/reopen this dialog if needed. Inspect the measured skin here."
                    : "Neck-skin head surface unavailable: "+(cranial?.RejectionReason??cranialFailure??"No distinct structural neck/head case was found.");
                var cranialInfo=new Label{Bounds=new Rectangle(12,437,626,38),Text=cranialSummary,AutoEllipsis=true};
                var inspectCranial=new Button{Name="inspectCranialSkin",Text="Inspect skin evidence…",Bounds=new Rectangle(12,482,170,28),Enabled=cranial?.Available==true};
                var useCranial=new CheckBox{Name="useCranialSkin",Text="Use this reviewed joint once for neck and head surface",Bounds=new Rectangle(194,482,444,28),
                    Enabled=sameCranial,Checked=sameCranial};
                DataGridViewRow headRow=grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r=>string.Equals(Convert.ToString(r.Cells[0].Value),"head",StringComparison.Ordinal));
                DataGridViewRow neckRow=grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r=>string.Equals(Convert.ToString(r.Cells[0].Value),"neck",StringComparison.Ordinal));
                if(useCranial.Checked)
                {
                    if(headRow!=null){headRow.Cells[1].Value="";headRow.Cells[1].ReadOnly=true;}
                    if(singleCranialJoint&&neckRow!=null)neckRow.Cells[1].Value=cranial.NeckBoneName;
                    status.Text="One reviewed native joint supplies neck motion and the head surface. No second head transform is mapped.";
                }
                useCranial.CheckedChanged+=(s,e)=>
                {
                    if(useCranial.Checked)
                    {
                        if(headRow!=null)headRow.Cells[1].Value="";
                        if(singleCranialJoint&&neckRow!=null)neckRow.Cells[1].Value=cranial.NeckBoneName;
                    }
                    if(headRow!=null)headRow.Cells[1].ReadOnly=useCranial.Checked;
                    status.Text=useCranial.Checked
                        ?"One reviewed native joint supplies neck motion and the head surface. No second head transform is mapped."
                        :initialStatus;
                };
                inspectCranial.Click+=(s,e)=>
                {
                    if(cranial?.Available!=true)return;
                    string Format(float value)=>value.ToString("0.######",CultureInfo.InvariantCulture);
                    string details="The head surface is a measured, directly skinned patch on the selected joint, not an extra head joint. " +
                        "It follows that existing transform once. Inspect the original character in the source preview before accepting.\r\n\r\n"+
                        "Native neck: "+cranial.NeckBoneName+" (joint "+cranial.NeckIndex+")\r\n"+
                        "Decoded vertices: "+cranial.DecodedVertexCount+"; represented weights: "+cranial.RepresentedWeightCount+"\r\n"+
                        "Bind-local cranial point: ("+Format(cranial.BindLocalPoint.X)+", "+Format(cranial.BindLocalPoint.Y)+", "+Format(cranial.BindLocalPoint.Z)+")\r\n"+
                        "Measured RMS radius: "+Format(cranial.Radius)+" native units\r\n"+
                        "Exact rig/skin SHA-256: "+cranial.RigAndSkinDigest+"\r\n\r\n"+
                        "Neck and immediate-child skin regions:\r\n"+
                        string.Join("\r\n",cranial.CandidatePatches.Select(p=>
                            (p.DirectNeckInfluence?"DIRECT NECK — ":"child branch — ")+p.RootJointName+
                            "; influenced "+p.InfluencedVertices+"; majority "+p.MajorityVertices+
                            "; weighted mass "+p.WeightedVertexMass.ToString("0.###",CultureInfo.InvariantCulture)+
                            "; RMS radius "+Format(p.BindRmsRadius)));
                    using(var evidenceDialog=new Form{Text="Review native cranial skin — "+model.Name,ClientSize=new Size(640,400),
                        StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,
                        MinimizeBox=false,MaximizeBox=false,ShowInTaskbar=false})
                    {
                        var evidenceBox=new TextBox{ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Both,WordWrap=false,
                            Bounds=new Rectangle(12,12,616,340),Text=details};
                        var closeEvidence=new Button{Text="Close",DialogResult=DialogResult.OK,Bounds=new Rectangle(534,360,94,28)};
                        evidenceDialog.Controls.AddRange(new Control[]{evidenceBox,closeEvidence});evidenceDialog.AcceptButton=closeEvidence;
                        DarkTheme.Apply(evidenceDialog);evidenceDialog.ShowDialog(dialog);
                    }
                    useCranial.Enabled=true;
                };
                var apply=new Button{Name="applySkeletonMap",Text="Use Mapping",Bounds=new Rectangle(426,532,108,28)};
                var cancel=new Button{Text="Cancel",DialogResult=DialogResult.Cancel,Bounds=new Rectangle(544,532,94,28)};
                Dictionary<string,string> confirmed=null;
                apply.Click+=(s,e)=>
                {
                    try
                    {
                        grid.EndEdit();
                        var selected=grid.Rows.Cast<DataGridViewRow>().Where(r=>!string.IsNullOrWhiteSpace(Convert.ToString(r.Cells[1].Value)))
                            .ToDictionary(r=>Convert.ToString(r.Cells[0].Value),r=>Convert.ToString(r.Cells[1].Value));
                        CranialAnchorReview choice=null;
                        if(useCranial.Checked)
                        {
                            if(cranial?.Available!=true||selected.ContainsKey("head")||
                                !selected.TryGetValue("neck",out string selectedNeck)||selectedNeck!=cranial.NeckBoneName)
                                throw new InvalidDataException("Review the measured skin, map its physical joint to neck, and leave the separate head role empty.");
                            choice=sameCranial?priorCranial:new CranialAnchorReview(cranial.RigAndSkinDigest,cranial.NeckBoneName,
                                "User inspected the direct-neck and competing child-branch skin measurements in Skeleton Mapping and accepted one physical neck as both neck and cranial surface without a duplicate transform.");
                        }
                        BrawlRetargeter.Capture(model,selected,coordinateFrame:ReviewedFighterCoordinateFrame(model),cranialReview:choice);
                        if(choice==null)_fighterCranialReviews.Remove(identity);else _fighterCranialReviews[identity]=choice;
                        confirmed=selected;dialog.DialogResult=DialogResult.OK;dialog.Close();
                    }
                    catch(Exception error) when(error is InvalidDataException || error is ArgumentException){status.Text=error.Message;}
                };
                dialog.Controls.AddRange(new Control[]{status,grid,cranialInfo,inspectCranial,useCranial,apply,cancel});dialog.AcceptButton=apply;dialog.CancelButton=cancel;
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
