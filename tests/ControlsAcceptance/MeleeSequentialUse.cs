using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class Program
{
    static async Task MeleeSequentialUse(string output,AnimationLibrary melee,AnimationLibrary brawl)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Sequential native Use requires a private desktop.");
        using(var startup=MainForm.StartViewerApplication())
        {
            var form=(ModelForm)Field(MainForm.Instance,"_fighterForm");
            try
            {
                form.ClientSize=new Size(1600,900);
                var targetItem=brawl.Browse("Wait1").First(i=>i.Model?.Name=="FitFox00"&&i.Clip.Name=="Wait1");
                Call(form,"OpenSavedGameLibrary",brawl,brawl.Read().GameLocations.Single(l=>l.Game=="Brawl"));
                Call(form,"SetSimpleWorkspaceLibraryVisible",true);
                await Flow39Select(form,targetItem);Call(form,"PauseSourceOperation");
                using(FlowModal("unsavedProjectDiscard",w=>SimpleControl<Button>(w,"unsavedProjectDiscard").PerformClick()))Call(form,"OpenLibraryAsTarget");
                await SimpleControlsReady(form);
                var editor=form.FighterEditor;
                var target=(MDL0Node)editor.TargetModel;
                Check(target.Name=="FitFox00"&&editor.SelectedCHR0?.Name=="Wait1","Sequential Use starts with the original editable Brawl target and Wait1");
                string targetHash=(string)Call(form,"NativeControlHash",target,".mdl0");
                Call(form,"OpenSavedGameLibrary",melee,melee.Read().GameLocations.Single(l=>l.Game=="Melee"));
                SonicReviewNativeMapping(form,target,BrawlRetargeter.SkeletonFingerprint(target),output,"target");
                var rig=BrawlRetargeter.Capture(target);var roles=rig.Rig.Roles.ToDictionary(p=>p.Key,p=>rig.Bones[p.Value].Name);
                using(var axes=Modal("Rig Coordinates",window=>window.Controls.OfType<Button>().Single(b=>b.Text=="Use Coordinates").PerformClick()))
                    Check((bool)Call(form,"ReviewEngineCoordinates",target,roles),"Sequential Use confirms exact target coordinates");
                CHR0Node first=null;
                foreach(string actor in new[]{"Fox","Falco"})
                {
                    var sourceItem=melee.Browse("Wait1").First(i=>i.Model?.Character==actor&&i.Clip.Name=="Wait1");
                    await Flow39Select(form,sourceItem);Call(form,"PauseSourceOperation");
                    var donor=(LibrarySourceLease)Field(form,"_libraryLease");
                    SonicReviewNativeMapping(form,donor.Model,BrawlRetargeter.SkeletonFingerprint(donor.Model),output,actor+"-source");
                    Check(SimpleControl<Button>(form,"libraryUseOnTarget").Enabled,"Sequential Use enables the exact "+actor+" donor");
                    int before=form.FighterProject.Animations.Count;
                    Exception failure=null;bool reviewed=false;var timer=Stopwatch.StartNew();
                    form.RetargetKeyframeReviewReady=window=>
                    {
                        try{Console.WriteLine("SEQUENTIAL "+actor+" review shown after "+timer.Elapsed.TotalSeconds.ToString("0.0")+" s");reviewed=true;SimpleControl<CheckBox>(window,"sourceKeysReviewed").Checked=true;SimpleControl<Button>(window,"sourceKeysUse").PerformClick();}
                        catch(Exception error){failure=error;window.Close();}
                    };
                    using(var errors=new Timer{Interval=100})
                    {
                        errors.Tick+=(s,e)=>
                        {
                            var message=Application.OpenForms.Cast<Form>().FirstOrDefault(w=>w!=form&&w.Text=="FighterPort Studio V2");
                            if(message==null)return;
                            failure=new InvalidOperationException("Sequential Use reported: "+string.Join(" ",message.Controls.Cast<Control>().Select(c=>c.Text)));
                            message.Close();
                        };
                        errors.Start();
                        SimpleControl<Button>(form,"libraryUseOnTarget").PerformClick();
                        errors.Stop();
                    }
                    form.RetargetKeyframeReviewReady=null;
                    Console.WriteLine("SEQUENTIAL "+actor+" Use returned after "+timer.Elapsed.TotalSeconds.ToString("0.0")+" s");
                    if(failure!=null)throw failure;
                    await SimpleControlsReady(form);
                    var selected=editor.SelectedCHR0;
                    Check(reviewed&&form.FighterProject.Animations.Count==before+1&&selected!=null&&ReferenceEquals(editor.TargetModel,target),"Sequential "+actor+" Use imports into the existing native viewer");
                    editor.SetFrame(Math.Min(5,selected.FrameCount));
                    Check(ReferenceEquals(editor.SelectedCHR0,selected)&&editor.CurrentFrame==Math.Min(5,selected.FrameCount),"Viewer playhead responds after "+actor+" Use");
                    editor.SetFrame(1);editor.PlayAnim();await Task.Delay(350);int playingFrame=editor.CurrentFrame;editor.StopAnim();
                    Check(playingFrame>1,"Native target playback advances after "+actor+" Use");
                    await CaptureSimpleWorkspace(form,Path.Combine(output,actor+"-after-use.png"));
                    if(first==null)first=selected;
                    else
                    {
                        // A fresh native control setup must not silently eat the
                        // viewer's Play command after a second result is added.
                        Call(form,"InvalidateControls");
                        await Wait(()=>(bool)Field(form,"_controlBusy"));
                        editor.SetFrame(1);editor.PlayAnim();await Task.Delay(350);
                        int setupPlaybackFrame=editor.CurrentFrame;editor.StopAnim();
                        Check(setupPlaybackFrame>1,"Target playback remains available while read-only control setup is pending");
                        await SimpleControlsReady(form);
                        var firstRow=editor.leftPanel.listAnims.Items.Cast<ListViewItem>().Single(i=>ReferenceEquals(i.Tag,first));
                        firstRow.Selected=true;firstRow.Focused=true;
                        editor.SetFrame(Math.Min(7,first.FrameCount));
                        Check(ReferenceEquals(editor.SelectedCHR0,first)&&editor.CurrentFrame==Math.Min(7,first.FrameCount),"Viewer returns to the first result after the second Use");
                        await CaptureSimpleWorkspace(form,Path.Combine(output,"first-result-reselected.png"));
                    }
                }
                Check(targetHash==(string)Call(form,"NativeControlHash",target,".mdl0"),"Sequential Use preserves target binds and all weights");
            }
            finally{if(!form.IsDisposed)SimpleDisposeForm(form);}
        }
    }
}
