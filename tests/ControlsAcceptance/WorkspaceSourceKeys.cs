using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static partial class Program
{
    static async Task WorkspaceSourceKeys(string[] args,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Source-key workspace qualification requires the private desktop.");
        string donor=args[Array.IndexOf(args,"--retarget-source")+1],name=args[Array.IndexOf(args,"--clip")+1];
        string libraryBefore=Environment.GetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT");Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT",Path.Combine(output,"isolated-library"));
        var originalFiles=new[]{args[0],args[1],donor}.Distinct().ToDictionary(p=>p,FighterProject.HashFile);
        var project=FighterProject.Create("Source-key retarget workspace qualification");var form=new ModelForm();Action<Form> cancelWork=null;
        try
        {
            form.InitializeFighterProject(project);form.Show();form.ClientSize=new Size(1700,950);
            Call(form,"ImportFighterModel",args[0]);Call(form,"ImportFighterAnimations",new[]{new[]{args[1]}});
            await GameLibraryWait(() => !(bool)Field(form,"_controlBusy") && !(bool)Field(form,"_controlSetupQueued"), "Source-key native controls did not settle.", 120);
            var editor=form.FighterEditor;var clip=editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().First(c=>c.Name==name);
            foreach(ListViewItem item in editor.leftPanel.listAnims.Items)item.Selected=ReferenceEquals(item.Tag,clip);editor.SelectedCHR0=clip;
            await GameLibraryWait(() => !(bool)Field(form,"_controlBusy") && !(bool)Field(form,"_controlSetupQueued"), "Source-key native controls did not settle.", 120);await Await<ControlReply>(Call(form,"LoadControls"));
            var rig=BrawlRetargeter.Capture((MDL0Node)editor.TargetModel);editor.SelectedBone=rig.Bones[rig.Rig.Roles["pelvis"]];editor.SetFrame(Math.Min(3,clip.FrameCount));editor.UpdateModel();editor.FrameFighterModel();
            if(args.Contains("--dirty-source-keys")||Environment.GetEnvironmentVariable("FIGHTERPORT_TEST_DIRTY_SOURCE_KEYS")=="1")
            {await SourceKeysDirtySnapshot(form,clip,donor,output);return;}
            Check((RetargetKeyframeMode)Field(form,"_retargetKeyframeMode")==RetargetKeyframeMode.Dense,"Fresh workspace retains original Dense Next as its session default");
            var nativeBefore=NativeAnimationOptimization.ContentKey(clip);int count=project.Animations.Count;var witness=new GameLibraryDocument(form);
            void SelectMode(RetargetKeyframeMode mode)
            {
                using(var modal=Modal("Retarget Options",window=>
                {
                    var keys=SimpleControl<ComboBox>(window,"retargetKeyframeMode");
                    Check(keys.Items.Count==3,"Retarget Options exposes Dense, Source bone keys and Source channel keys");
                    keys.SelectedIndex=(int)mode;SimpleControl<Button>(window,"retargetOptionsApply").PerformClick();
                }))Call(form,"ShowFighterRetargetOptions");
            }
            SelectMode(RetargetKeyframeMode.SourceBoneTimes);witness.CheckPreserved("session-only retarget key choice");
            editor.ModelPanel.Focus();
            bool cancelledReview=false;
            form.RetargetKeyframeReviewReady=window=>{cancelledReview=true;SimpleControl<Button>(window,"sourceKeysCancel").PerformClick();};
            Check(form.RetargetFighterAnimation(donor)==null&&cancelledReview&&project.Animations.Count==count,"Cancelling the prepared sparse comparison imports no animation");
            Check(editor.ModelPanel.ContainsFocus,"Cancelling source-key review restores the invoking native viewport focus");
            witness.CheckPreserved("cancelled sparse result review");await GameLibraryWait(() => !(bool)Field(form,"_controlBusy") && !(bool)Field(form,"_controlSetupQueued"), "Source-key native controls did not settle.", 120);

            bool cancelledWork=false;
            cancelWork=window=>{cancelledWork=true;window.Controls.OfType<Button>().Single(b=>b.Text=="Cancel").PerformClick();};form.RetargetDialogReady+=cancelWork;
            Check(form.RetargetFighterAnimation(donor)==null&&cancelledWork,"Cancellation through the real native progress dialog discards the late source-key work");
            form.RetargetDialogReady-=cancelWork;cancelWork=null;witness.CheckPreserved("cancelled sparse worker");await GameLibraryWait(() => !(bool)Field(form,"_controlBusy") && !(bool)Field(form,"_controlSetupQueued"), "Source-key native controls did not settle.", 120);

            form.RetargetKeyframeReviewReady=window=>
            {
                typeof(ModelForm).GetField("_retargetKeyframeMode",Private).SetValue(form,RetargetKeyframeMode.SourceChannelTimes);
                SimpleControl<CheckBox>(window,"sourceKeysReviewed").Checked=true;SimpleControl<Button>(window,"sourceKeysUse").PerformClick();
            };
            bool stale=false;
            try{form.RetargetFighterAnimation(donor);}catch(InvalidOperationException error){stale=error.Message.Contains("stale result");}
            Check(stale&&project.Animations.Count==count,"A changed key policy during review rejects publication of the complete stale result");
            witness.CheckPreserved("stale source-key review");await GameLibraryWait(() => !(bool)Field(form,"_controlBusy") && !(bool)Field(form,"_controlSetupQueued"), "Source-key native controls did not settle.", 120);

            foreach(var mode in new[]{RetargetKeyframeMode.SourceBoneTimes,RetargetKeyframeMode.SourceChannelTimes})
            {
                SelectMode(mode);Exception failure=null;bool reviewed=false;
                form.RetargetKeyframeReviewReady=async raw=>
                {
                    var window=(RetargetKeyframeReviewDialog)raw;
                    try
                    {
                        Check(!SimpleControl<Button>(window,"sourceKeysUse").Enabled&&SimpleControl<TextBox>(window,"sourceKeysReport").Text.Contains("Dense solver contact statistics remain historical"),
                            mode+": actual motion review is required and dense contact claims stay historical");
                        var play=SimpleControl<Button>(window,"sourceKeysPlay");play.Focus();
                        Check((bool)Call(window,"ProcessCmdKey",default(Message),Keys.F6)&&SimpleControl<TextBox>(window,"sourceKeysReport").Focused,
                            mode+": application F6 routing enters the measured report without injected OS input");
                        Check((bool)Call(window,"ProcessCmdKey",default(Message),Keys.Shift|Keys.F6)&&play.Focused,
                            mode+": reverse F6 restores the valid focused timeline action");
                        SimpleControl<CheckBox>(window,"sourceKeysReviewed").Focus();
                        Check((bool)Call(window,"ProcessDialogKey",Keys.Tab)&&SimpleControl<Button>(window,"sourceKeysCancel").Focused,
                            mode+": ordinary Tab skips the disabled unapproved result action");
                        foreach(int text in new[]{100,125,150,175})foreach(var size in new[]{new Size(980,720),new Size(1600,960)})
                        {
                            // The authoring workspace correctly refuses to
                            // change settings during a busy retarget. Exercise
                            // the dialog's frozen presentation directly here.
                            WorkspaceAppearance.SetTextPercent(text);window.ApplyTextSize();window.ClientSize=size;window.PerformLayout();
                            Check(WorkspaceAppearance.TextPercent==text&&Math.Abs(window.Font.Size-WorkspaceAppearance.Font.Size)<.001,
                                mode+": review actually uses requested "+text+"% text rather than a blocked busy workspace setting");
                            window.DenseView.ShownFrame();window.ResultView.ShownFrame();
                            foreach(string field in new[]{"sourceKeysFrame","sourceKeysReport","sourceKeysReviewed","sourceKeysUse","sourceKeysCancel"})
                            {
                                var control=SimpleDescendants(window).Single(c=>c.Name==field);var bounds=window.RectangleToClient(control.RectangleToScreen(control.ClientRectangle));
                                Check(control.Visible&&control.Width>0&&control.Height>0&&window.ClientRectangle.Contains(bounds),mode+" compact/wide supported text layout: "+field+" / "+text+" / "+size.Width);
                            }
                            await CaptureSourceKeys(window,Path.Combine(output,"sourcekeys-"+mode+"-"+text+"-"+size.Width+".png"));
                            witness.CheckPreserved(mode+" read-only comparison / "+text+" / "+size.Width);
                        }
                        SimpleControl<CheckBox>(window,"sourceKeysReviewed").Checked=true;reviewed=true;SimpleControl<Button>(window,"sourceKeysUse").PerformClick();
                    }
                    catch(Exception error){failure=error;window.Close();}
                };
                var asset=form.RetargetFighterAnimation(donor);if(failure!=null)throw failure;
                Check(reviewed&&asset?.Retarget?.KeyframeMode==mode&&asset.Retarget.ReadKeyframeReport().ResultContent==NativeAnimationOptimization.ContentKey(editor.SelectedCHR0),
                    mode+": reviewed result becomes an independent target revision with exact recipe evidence");
                Check(FighterProject.HashFile(asset.WorkingPath)==asset.Retarget.ReadKeyframeReport().VerifiedSha256,
                    mode+": published independent revision uses the exact bytes that passed native comparison");
                Check(asset.Retarget.DenseReferenceStatistics!=null&&!asset.Retarget.FloorContactsEnabled&&!NativeTransitionContacts.HasBakedSupport(asset),
                    mode+": changed sparse curves cannot inherit dense baked-support qualification");
                await GameLibraryWait(() => !(bool)Field(form,"_controlBusy") && !(bool)Field(form,"_controlSetupQueued"), "Source-key native controls did not settle.", 120);
                string file=Path.Combine(output,"sourcekeys-"+mode+".chr0");form.ExportFighterResource(file);
                using(var decoded=NodeFactory.FromFile(null,file) as CHR0Node)Check(decoded!=null&&NativeAnimationOptimization.ContentKey(decoded)==asset.Retarget.ReadKeyframeReport().ResultContent,
                    mode+": precise export independently reopens with exact sparse content and protected control layout");
                string manifest=Path.Combine(output,"sourcekeys-"+mode+".fpstudio");form.SaveFighterProject(manifest);
                using(var reopened=FighterProject.Open(manifest))
                {
                    var saved=reopened.Animations.Single(a=>a.Id==asset.Id);saved.Retarget.ValidateKeyframeEvidence();
                    using(var savedRoot=NodeFactory.FromFile(null,saved.WorkingPath))
                        Check(saved.Retarget.KeyframeMode==mode&&NativeAnimationOptimization.ContentKey(Walk(savedRoot).OfType<CHR0Node>().Single())==saved.Retarget.ReadKeyframeReport().ResultContent,
                            mode+": project Save/reopen keeps exact curves, chosen layout and bounded historical native comparison");
                }
                // Return to the exact donor registration through the existing
                // native import Undo; no additional undo owner is introduced.
                Call(form,"UndoFighterImport");await GameLibraryWait(() => !(bool)Field(form,"_controlBusy") && !(bool)Field(form,"_controlSetupQueued"), "Source-key native controls did not settle.", 120);
                Check(project.Animations.Count==count,"Source-key import uses the existing one-transaction native project Undo");
                foreach(ListViewItem item in editor.leftPanel.listAnims.Items)item.Selected=ReferenceEquals(item.Tag,clip);editor.SelectedCHR0=clip;
                await GameLibraryWait(() => !(bool)Field(form,"_controlBusy") && !(bool)Field(form,"_controlSetupQueued"), "Source-key native controls did not settle.", 120);editor.SetFrame(Math.Min(3,clip.FrameCount));
                witness=new GameLibraryDocument(form);
            }
            Check(NativeAnimationOptimization.ContentKey(clip)==nativeBefore&&originalFiles.All(pair=>FighterProject.HashFile(pair.Key)==pair.Value),
                "Source-key workspace operations preserve exact original donor curves and every source file");
            File.WriteAllText(Path.Combine(output,"source-key-boundaries.json"),new JavaScriptSerializer().Serialize(new{PrivateDesktop,OSInputInjected=false,ScreenCopied=false,PhysicalInput=false,GameAcceptance=false}));
        }
        finally
        {
            if(cancelWork!=null)form.RetargetDialogReady-=cancelWork;form.RetargetKeyframeReviewReady=null;
            if(!form.IsDisposed){Call(form,"SetWorkspaceTextSize",100);SimpleDisposeForm(form);}project.Dispose();Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT",libraryBefore);
        }
    }
    static async Task CaptureSourceKeys(RetargetKeyframeReviewDialog window,string path)
    {
        await CaptureCheckpoint();
        using(var bitmap=new Bitmap(window.Width,window.Height))
        {
            window.DrawToBitmap(bitmap,new Rectangle(Point.Empty,window.Size));
            foreach(var view in new[]{window.DenseView,window.ResultView})
            {
                var panel=view.Viewport;panel.Capture();panel.Invalidate();panel.Update();
                using(var screenshot=panel.GetScreenshot(panel.ClientRectangle,false))using(var graphics=Graphics.FromImage(bitmap))
                {var point=window.PointToClient(panel.PointToScreen(Point.Empty));int border=(window.Width-window.ClientSize.Width)/2;graphics.DrawImageUnscaled(screenshot,point.X+border,point.Y+window.Height-window.ClientSize.Height-border);}
            }
            bitmap.Save(path);
        }
    }

    static async Task SourceKeysDirtySnapshot(ModelForm form,CHR0Node clip,string donor,string output)
    {
        var editor=form.FighterEditor;var targetRig=BrawlRetargeter.Capture((MDL0Node)editor.TargetModel);
        string targetBody=targetRig.Bones[targetRig.Rig.Roles["pelvis"]].Name;
        using(var donorRoot=NodeFactory.FromFile(null,donor))
        {
            var sourceRig=BrawlRetargeter.Capture(Walk(donorRoot).OfType<MDL0Node>().OrderByDescending(m=>m.NumVertices).First());
            var bone=sourceRig.Bones[sourceRig.Rig.Roles["pelvis"]];
            var old=(CHR0EntryNode)clip.FindChild(bone.Name,false);int index=old.Index;
            var entry=clip.CreateEntry("Temporary native source fixture");
            for(int c=0;c<9;c++)foreach(var key in old.KeyArrays[c].Entries)entry.Keyframes.SetFrameValue(c,key._index,key._value,true)._tangent=key._tangent;
            entry.UseModelScale=old.UseModelScale;entry.UseModelRotate=false;entry.UseModelTranslate=old.UseModelTranslate;
            entry.ScaleCompensateApply=old.ScaleCompensateApply;entry.ScaleCompensateParent=old.ScaleCompensateParent;entry.ClassicScaleOff=old.ClassicScaleOff;
            old.Remove();old.Dispose();entry.Remove();clip.InsertChild(entry,index);entry.Name=bone.Name;
            int[] times=Enumerable.Range(0,clip.FrameCount).Where(f=>f%2==0||f==clip.FrameCount-1).ToArray();
            for(int c=3;c<6;c++)
            {
                var curve=entry.KeyArrays[c];while(curve.KeyCount>0)curve.Remove(curve.Entries.First()._index);
                foreach(int at in times)curve.SetFrameValue(at,bone.BindState[c]+(float)Math.Sin(at*.17+c)*13.1734567f,true)._tangent=.12345678f+at*.00234567f;
            }
            entry.Keyframes.PreserveInterpolation=false;entry.SignalPropertyChange();Call(form,"MarkFighterChanged");
            await GameLibraryWait(()=>!(bool)Field(form,"_controlBusy")&&!(bool)Field(form,"_controlSetupQueued"),"Dirty source control refresh did not settle.",120);
            Check(entry.GetType()==typeof(CHR0EntryNode)&&!entry.Keyframes.PreserveInterpolation&&entry.IsDirty,
                "Dirty source fixture is an ordinary native CHR0 entry with sparse authored keys and legacy codec selection enabled");
            // Prove this fixture exercises the former failure. Export an
            // independent ordinary entry, never the live one, through legacy
            // encoding and show that its authored times are resampled.
            using(var legacy=new CHR0Node{Name="Independent legacy source snapshot",FrameCount=clip.FrameCount,Loop=clip.Loop})
            {
                var ordinary=legacy.CreateEntry(entry.Name);
                for(int c=0;c<9;c++)foreach(var key in entry.KeyArrays[c].Entries)ordinary.Keyframes.SetFrameValue(c,key._index,key._value,true)._tangent=key._tangent;
                ordinary.Keyframes.PreserveInterpolation=false;
                string path=Path.Combine(output,"legacy-codec-contradiction.chr0");legacy.ExportChecked(path);
                using(var decoded=NodeFactory.FromFile(null,path) as CHR0Node)
                    Check(!((CHR0EntryNode)decoded.FindChild(entry.Name,false)).KeyArrays[3].Entries.Select(k=>k._index).SequenceEqual(times),
                        "Independent legacy writer would replace this dirty sparse source schedule before retargeting");
            }
            string original=NativeAnimationOptimization.ContentKey(clip);bool dirty=clip.IsDirty,branch=clip.IsBranch,entryDirty=entry.IsDirty;
            var witness=new GameLibraryDocument(form);bool reviewed=false;Exception failure=null;
            typeof(ModelForm).GetField("_retargetKeyframeMode",Private).SetValue(form,RetargetKeyframeMode.SourceChannelTimes);
            form.RetargetKeyframeReviewReady=raw=>
            {
                try
                {
                    var candidate=(CHR0Node)Field(raw,"candidate");var body=(CHR0EntryNode)candidate.FindChild(targetBody,false);
                    foreach(int c in new[]{3,4,5})Check(times.All(time=>body.KeyArrays[c].GetKeyframe(time)!=null),
                        "Dirty ordinary source: target channel "+c+" retains every visible authored source seed while quality guards may add required knots");
                    var window=(RetargetKeyframeReviewDialog)raw;
                    OptimizationWithheldGeometry(window.DenseView,window.ResultView,(CHR0Node)Field(raw,"dense"),candidate,
                        Path.Combine(output,"dirty-source-withheld-native.json"));
                    reviewed=true;
                }
                catch(Exception error){failure=error;}
                finally{SimpleControl<Button>(raw,"sourceKeysCancel").PerformClick();}
            };
            bool cancelled=form.RetargetFighterAnimation(donor)==null;
            if(failure!=null)throw failure;
            Check(cancelled&&reviewed,"Dirty ordinary source reaches a verified sparse review and cancellation adds no revision");
            witness.CheckPreserved("dirty ordinary source precise snapshot and cancelled review");
            Check(NativeAnimationOptimization.ContentKey(clip)==original&&clip.IsDirty==dirty&&clip.IsBranch==branch&&entry.IsDirty==entryDirty&&!entry.Keyframes.PreserveInterpolation,
                "Dirty ordinary source snapshot preserves exact live curves, codec preference, native dirty/branch state and native Undo");
        }
    }
}
