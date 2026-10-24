using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class Program
{
    static async Task ReviewedFittingAxes(ModelForm form,MDL0Node model,string output,string prefix)
    {
        string path=Path.Combine(output,prefix+".mdl0");model.Export(path);
        using(var client=new AnimationControlClient())
        {
            var setup=await client.Send(new{Operation="load",Model=path});var roles=setup.Roles.Where(r=>r.Bone.HasValue).ToDictionary(r=>r.Role,r=>setup.BoneNames[r.Bone.Value]);
            foreach(var chain in setup.Chains){string side=chain.Role.Substring(chain.Role.LastIndexOf('.')+1);var labels=chain.Role.StartsWith("Hand")?new[]{"UpperArm.","Elbow.","Wrist."}:new[]{"UpperLeg.","Knee.","Ankle."};var bones=new[]{chain.Root,chain.Middle,chain.Effector};for(int i=0;i<3;i++)roles[labels[i]+side]=setup.BoneNames[bones[i]];}
            form.FighterProject.RigReviews.RemoveAll(r=>r.Fingerprint==EngineRigReview.FingerprintOf(model));
            var native=BrawlRetargeter.Characterize(model).Roles;var nativeBones=model.AllBones.ToArray();form.FighterProject.RigReviews.Add(new EngineRigReview{Fingerprint=EngineRigReview.FingerprintOf(model),Roles=roles.Keys.ToArray(),Bones=roles.Values.ToArray(),NativeRoles=native.Keys.ToArray(),NativeBones=native.Values.Select(i=>nativeBones[i].Name).ToArray(),Right=new[]{1f,0,0},Up=new[]{0f,1,0},Forward=new[]{0f,0,1},Evidence="Acceptance: explicit review of supplied native axes and existing chain/role bindings"});
        }
    }
    static async Task SceneFitting(string[] args,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Scene fitting acceptance requires the private desktop.");
        string donor=args.Contains("--retarget-source")?args[Array.IndexOf(args,"--retarget-source")+1]:args[0],motion=args[1];
        if(Path.GetExtension(motion).Equals(".chr0",StringComparison.OrdinalIgnoreCase))
        {
            // The minimal original curve has one root track. Author independent
            // rest constants on two existing body joints so the ordinary library
            // association gate has actual native body-channel evidence.
            using(var model=NodeFactory.FromFile(null,donor) as MDL0Node)using(var clip=NodeFactory.FromFile(null,motion) as CHR0Node)
            {
                var rig=BrawlRetargeter.Characterize(model);var bones=model.AllBones.ToArray();foreach(var role in new[]{"pelvis","spine"}){var bone=bones[rig.Roles[role]];if(clip.FindChild(bone.Name,false)!=null)continue;var entry=new CHR0EntryNode{Name=bone.Name};clip.AddChild(entry);for(int channel=0;channel<9;channel++)entry.SetKeyframe(channel,0,bone.BindState[channel],true,true);}
                motion=Path.Combine(output,"original-body-evidence.chr0");clip.Export(motion);
            }
        }
        string donorHash=FighterProject.HashFile(donor),motionHash=FighterProject.HashFile(motion),targetHash=FighterProject.HashFile(args[0]);
        var library=new AnimationLibrary(Path.Combine(output,"library"));var inspected=BrawlLibrarySource.InspectModels(donor).First();library.Register(BrawlLibrarySource.Prepare(new LibraryRegistration{ModelPath=donor,ModelKey=inspected.ResourceKey,MotionPath=motion,Character="Supplied source",GameVersion="Local fixture",BuildEvidence="User supplied native workflow fixture; game quality unverified",ApproveAssociation=true}));
        var item=library.Browse("").FirstOrDefault(c=>c.State==LibrarySourceState.Available&&c.Clip.Name=="Wait1")??library.Browse("").First(c=>c.State==LibrarySourceState.Available);
        var project=FighterProject.Create("Advanced native fitting");var form=new ModelForm();form.InitializeFighterProject(project);form.Show();Call(form,"ImportFighterModel",args[0]);await Task.Delay(150);await Wait(()=>!(bool)Field(form,"_controlBusy")&&!(bool)Field(form,"_controlSetupQueued"));
        Call(form,"BuildLibraryPane");form.GetType().GetField("_library",Private).SetValue(form,library);
        using(var source=library.Open(item.Clip.Id))await ReviewedFittingAxes(form,source.Model,output,"review-source");await ReviewedFittingAxes(form,(MDL0Node)form.FighterEditor.TargetModel,output,"review-target");
        string companion=null,companionJson=null;
        if(Path.GetExtension(args[1]).Equals(".chr0",StringComparison.OrdinalIgnoreCase))
        {
            string contextModel=Path.Combine(output,"context-model.mdl0"),contextMotion=Path.Combine(output,"context-motion.chr0");companion=Path.Combine(output,"source-context.fighterport");using(var lease=library.Open(item.Clip.Id)){lease.Model.Export(contextModel);lease.Animation.Export(contextMotion);}
            var start=new ProcessStartInfo(Environment.GetEnvironmentVariable("FIGHTERPORT_ENGINE_HOST"),"cli import-brawl \""+contextModel+"\" \""+contextMotion+"\" \""+companion+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            start.EnvironmentVariables["FIGHTERPORT_BRAWL_WORKER"]=Path.Combine(Path.GetDirectoryName(start.FileName),"worker","FighterPort.BrawlWorker.exe");using(var process=Process.Start(start)){var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();await Task.Run(()=>process.WaitForExit());if(process.ExitCode!=0)throw new Exception(await stderr);await stdout;}
            // Preserve the authentic primary clip's exact float JSON (including
            // signed zero); the legacy UI serializer rounds parsed doubles.
            var serializer=new JavaScriptSerializer{MaxJsonLength=64*1024*1024,RecursionLimit=200};string raw=File.ReadAllText(companion);var context=serializer.Deserialize<Dictionary<string,object>>(raw);var clips=((System.Collections.IEnumerable)context["Clips"]).Cast<object>().ToArray();var extra=serializer.Deserialize<Dictionary<string,object>>(serializer.Serialize(clips[0]));extra["Id"]=Guid.NewGuid().ToString();extra["Name"]="Unselected context clip retained";
            var match=System.Text.RegularExpressions.Regex.Match(raw,"(\"Clips\"\\s*:\\s*\\[)(.*?)(\\]\\s*,\\s*\"AuthoringPose\")",System.Text.RegularExpressions.RegexOptions.Singleline);if(!match.Success)throw new Exception("Expected single-clip context fixture layout.");companionJson=raw.Substring(0,match.Index)+match.Groups[1].Value+match.Groups[2].Value+","+serializer.Serialize(extra)+match.Groups[3].Value+raw.Substring(match.Index+match.Length);File.WriteAllText(companion,companionJson);
        }
        int animations=project.Animations.Count;string nativeHash=(string)Call(form,"NativeControlHash",form.FighterEditor.TargetModel,".mdl0");Exception failure=null;bool completed=false;
        form.SceneFittingReady=async window=>
        {
            try
            {
                await Wait(()=>!window.Busy);Check(window.Reply!=null&&!window.Reply.SourceNeedsReview&&!window.Reply.TargetNeedsReview,"Advanced review loads exact native source and reviewed source/target axes");
                Check(window.SourceView.Model!=form.FighterEditor.TargetModel&&window.TargetView.Model!=form.FighterEditor.TargetModel,"Both advanced viewports own native copies rather than posing live authoring resources");
                var viewer=form.FighterEditor.ModelPanel.CurrentViewport;foreach(var view in new[]{window.SourceView,window.TargetView})
                {
                    var actual=view.Viewport.CurrentViewport;
                    Check(actual._ambient==viewer._ambient&&actual._diffuse==viewer._diffuse&&actual._emission==viewer._emission&&actual._specular==viewer._specular&&actual.LightPosition==viewer.LightPosition&&actual._renderAttrib._renderShaders==viewer._renderAttrib._renderShaders&&!ReferenceEquals(actual._renderAttrib,viewer._renderAttrib),"Advanced scene uses the model previewer's native lighting and shader settings with independent viewport attributes");
                    Check(view.Model.MaterialList.OfType<MDL0MaterialNode>().All(m=>m.clr1==(BrawlLib.Internal.Vector4)m.C1MaterialColor&&m.amb1==(BrawlLib.Internal.Vector4)m.C1AmbientColor&&m.k1==(BrawlLib.Internal.Vector4)m.ConstantColor0),"Native preview materials initialize their actual material, ambient and shader constant colors");
                }
                Check(!window.Controls.Find("fitting_arm",true).Single().Enabled&&!window.Controls.Find("applyFitting",true).Single().Enabled,"Uncalibrated body preferences and advanced Apply remain disabled");
                if(companion!=null){await window.LoadContext(companion);File.WriteAllText(Path.Combine(output,"context-report.txt"),((TextBox)window.Controls.Find("fittingReport",true).Single()).Text);Check(window.Reply!=null&&window.Reply.Context.Contains("2 clips")&&window.Controls.Find("fittingContextClip",true).OfType<ComboBox>().Single().Items.Count==2,"Exact companion loading retains the full source project, including unselected clips");}
                ((ComboBox)window.Controls.Find("fitting_scene",true).Single()).SelectedIndex=2;((CheckBox)window.Controls.Find("fitting_reverse",true).Single()).Checked=true;
                ((NumericUpDown)window.Controls.Find("fitting_sourceEdge",true).Single()).Value=.25m;((NumericUpDown)window.Controls.Find("fitting_targetEdge",true).Single()).Value=-.5m;
                ((CheckBox)window.Controls.Find("fitting_endAuto",true).Single()).Checked=false;((NumericUpDown)window.Controls.Find("fitting_end",true).Single()).Value=.15m;
                Check(window.Reply==null,"Editing scene parameters invalidates the previous review receipt");
                ((Button)window.Controls.Find("prepareFitting",true).Single()).PerformClick();await Wait(()=>!window.Busy);
                var reply=window.Reply;File.WriteAllText(Path.Combine(output,"advanced-receipt.json"),reply?.Receipt??((TextBox)window.Controls.Find("fittingReport",true).Single()).Text);
                Check(reply!=null&&reply.PreviewOnly&&!reply.ApplyAllowed&&!reply.MeasuredReady,"Existing advanced motion/contact gates remain unresolved rather than becoming accepted through the native UI");
                Check(reply.MissingSourceBones.Length>0?reply.Diagnostics.Any(d=>d.Message.Contains("absent")):reply.Diagnostics.Any(d=>d.Code=="RETARGET_POSE_REWRITE_REVIEW"),"Advanced review reports incompatible native source channels or the retained pose-transfer review gate precisely");
                Check(project.Animations.Count==animations,"Advanced preparation publishes no animation to the authoring document");
                await CaptureWindow(window,Path.Combine(output,"advanced-scene-review.png"));
                if(reply.PreviewTracks.Length>0){var slider=(TrackBar)window.Controls.Find("fittingFrame",true).Single();slider.Value=slider.Maximum;Check(window.TargetView.Heading.Text.Contains("sampled review proxy"),"Prepared advanced candidate is visibly identified as a sampled read-only proxy");await CaptureWindow(window,Path.Combine(output,"advanced-candidate-last.png"));}
                ((Button)window.Controls.Find("saveFittingReview",true).Single()).PerformClick();await Wait(()=>!window.Busy);Check(project.SceneFittingReviews.Count==1&&project.SceneFittingReviews[0].Receipt==reply.Receipt,"Saving an advanced review retains settings, exact source ownership and unresolved receipt separately from animations");
                var reviewed=project.RigReviews.Single(r=>r.Fingerprint==EngineRigReview.FingerprintOf((MDL0Node)form.FighterEditor.TargetModel));string evidence=reviewed.Evidence;reviewed.Evidence+=" changed";bool rejected=false;try{await window.SaveReview(window.ReadOptions(),reply,window.ContextPath,window.ContextClip);}catch(InvalidOperationException error){rejected=error.Message.Contains("binding changed");}finally{reviewed.Evidence=evidence;}Check(rejected&&project.SceneFittingReviews.Count==1,"Saving rejects changed reviewed rig dependencies and leaves the prior review intact");
                if(companion!=null){File.WriteAllText(companion,companionJson.Replace("Unselected context clip retained","Changed unselected context clip"));rejected=false;try{await window.SaveReview(window.ReadOptions(),reply,window.ContextPath,window.ContextClip);}catch(InvalidOperationException error){rejected=error.Message.Contains("context changed");}finally{File.WriteAllText(companion,companionJson);}Check(rejected&&project.SceneFittingReviews.Count==1,"Changed companion context rejects saved stale receipts without overwriting prior review metadata");}
                if(companion!=null)
                {
                    string input=Path.Combine(output,"context-motion.chr0");byte[] bytes=File.ReadAllBytes(input),changed=(byte[])bytes.Clone();changed[changed.Length-1]^=1;File.WriteAllBytes(input,changed);rejected=false;try{await window.SaveReview(window.ReadOptions(),reply,window.ContextPath,window.ContextClip);}catch(InvalidOperationException error){rejected=error.Message.Contains("preserved native inputs changed");}finally{File.WriteAllBytes(input,bytes);}Check(rejected&&project.SceneFittingReviews.Count==1,"Host rejects a changed preserved native input even when the companion project JSON is unchanged");
                    using(var guard=new AnimationControlClient()){await guard.Send(new{Operation="fitting-load",SourceModel=Path.Combine(output,"context-model.mdl0"),Motion=input,Model=Path.Combine(output,"review-target.mdl0"),Path=companion,ClipKey=window.ContextClip});rejected=false;try{await guard.Send(new{Operation="fitting-apply"});}catch(InvalidOperationException error){rejected=error.Message.Contains("review workflow");}Check(rejected,"Engine rejects advanced Apply independently of the disabled UI button");}
                }
                string saved=project.SceneFittingReviews[0].Options;form.FighterEditor.Undo();Check(project.SceneFittingReviews.Count==0,"One native Undo removes the advanced review metadata");form.FighterEditor.Redo();Check(project.SceneFittingReviews.Single().Options==saved,"Native Redo restores exact saved scene/body review settings");
                await Wait(()=>!(bool)Field(form,"_controlBusy")&&!(bool)Field(form,"_controlSetupQueued"));((Button)window.Controls.Find("previewOriginalNext",true).Single()).PerformClick();var baselineReport=((TextBox)window.Controls.Find("fittingReport",true).Single()).Text;File.WriteAllText(Path.Combine(output,"baseline-report.txt"),baselineReport);Check(window.TargetView.Heading.Text.Contains("original Next baseline")||baselineReport.Contains("Skeleton mapping needs:"),"Original Next comparison uses its existing mapping gate or produces the original baseline without replacing the default solver");
                Check(project.Animations.Count==animations,"Original Next comparison also keeps authoring animations unchanged");await CaptureWindow(window,Path.Combine(output,"advanced-next-comparison.png"));
                ((CheckBox)window.Controls.Find("fitting_overlap",true).Single()).Checked=false;Check(window.Reply==null&&!window.Controls.Find("applyFitting",true).Single().Enabled,"Changing body-volume policy discards review acceptance and prepared comparison");
                ((Button)window.Controls.Find("prepareFitting",true).Single()).PerformClick();((Button)window.Controls.Find("cancelFitting",true).Single()).PerformClick();await Task.Delay(100);Check(!window.Busy&&window.Reply==null&&project.Animations.Count==animations,"Cancelling the isolated fitting process prevents late publication and keeps native animations intact");
                window.Close();
            }
            catch(Exception e){failure=e;window.Close();}
            finally{completed=true;}
        };
        Call(form,"ShowAdvancedSceneFitting",item.Clip.Id);await Wait(()=>completed);if(failure!=null)throw failure;
        Check((string)Call(form,"NativeControlHash",form.FighterEditor.TargetModel,".mdl0")==nativeHash,"Advanced scene review, comparison and cancellation preserve all live native model bytes");
        string projectPath=Path.Combine(output,"scene-fitting.fpstudio");form.SaveFighterProject(projectPath);using(var fresh=FighterProject.Open(projectPath)){Check(fresh.SceneFittingReviews.Count==1&&fresh.SceneFittingReviews[0].Source.ClipId==item.Clip.Id,"Fresh project reopen preserves the exact source-bound advanced review");Check(fresh.SceneFittingReviews[0].Receipt.Contains("RETARGET_POSE_REWRITE_REVIEW")||fresh.SceneFittingReviews[0].Receipt.Contains("absent"),"Reopening saved fitting metadata retains its unresolved result rather than promoting acceptance");}
        completed=false;form.SceneFittingReady=async window=>{try{await Wait(()=>!window.Busy);var options=window.ReadOptions();Check(options.Scene==2&&options.ReverseEdge&&options.SourceEdge==.25f&&options.TargetEdge==-.5f&&options.TrimEndSeconds==.15,"Reopening the fitting window restores exact saved scene and timing settings");Check(window.Reply!=null&&window.Reply.TargetFrames==0&&!window.Controls.Find("applyFitting",true).Single().Enabled,"Saved receipts reopen as context without publishing a candidate or enabling Apply");if(companion!=null)Check(((ComboBox)window.Controls.Find("fittingContextClip",true).Single()).Items.Count==2&&window.ContextClip==project.SceneFittingReviews[0].ContextClip,"Saved companion selection repopulates the native clip selector on reopen");await CaptureWindow(window,Path.Combine(output,"advanced-settings-reopened.png"));}catch(Exception error){failure=error;}finally{window.Close();completed=true;}};Call(form,"ShowAdvancedSceneFitting",item.Clip.Id);await Wait(()=>completed);if(failure!=null)throw failure;
        Check(FighterProject.HashFile(donor)==donorHash&&FighterProject.HashFile(motion)==motionHash&&FighterProject.HashFile(args[0])==targetHash,"Advanced review leaves supplied model and motion containers immutable");
        ((AnimationControlClient)Field(form,"_controlEngine")).Dispose();project.Dispose();BrawlCrate.UI.Model_Previewer.ModelEditControl.ModelEditControl.Instances.Remove(form.FighterEditor);form.Dispose();
    }
}
