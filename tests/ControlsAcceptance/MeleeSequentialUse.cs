using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class Program
{
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wparam,IntPtr lparam);
    static int ViewerMetric(Control panel,int message)=>SendMessage(panel.Handle,message,IntPtr.Zero,IntPtr.Zero).ToInt32();
    static async Task VerifyViewerMotion(ModelForm form,MDL0Node target,CHR0Node clip,string label)
    {
        var editor=form.FighterEditor;var panel=editor.ModelPanel;
        editor.SetFrame(1);panel.Update();int first=ViewerMetric(panel,0x84F9);float[] firstPose=Flow39Pose(target);
        int sample=Math.Min(30,clip.FrameCount);editor.SetFrame(sample);panel.Update();int later=ViewerMetric(panel,0x84F9);float[] laterPose=Flow39Pose(target);
        float maximum=firstPose.Zip(laterPose,(a,b)=>Math.Abs(a-b)).Max();
        Console.WriteLine("VIEWER "+label+" explicit first="+first+" frame"+sample+"="+later+" poseDelta="+maximum.ToString("R"));
        Check(maximum>0.0001f,"The selected native result changes the target pose between explicit frames: "+label);
        Check(first!=later,"The main native viewport displays distinct target poses between explicit frames: "+label);
        editor.SetFrame(1);panel.Update();int before=ViewerMetric(panel,0x84F0);int start=ViewerMetric(panel,0x84F9);
        editor.PlayAnim();await Task.Delay(250);int middle=ViewerMetric(panel,0x84F9);int middleFrame=editor.CurrentFrame;
        await Task.Delay(250);int end=ViewerMetric(panel,0x84F9);int endFrame=editor.CurrentFrame;
        int rendered=ViewerMetric(panel,0x84F0)-before;int accelerated=ViewerMetric(panel,0x84F8);bool occluded=panel.PresentationOccluded;
        editor.StopAnim();
        Console.WriteLine("VIEWER "+label+" playback frames="+middleFrame+","+endFrame+" hashes="+start+","+middle+","+end+" rendered="+rendered+" accelerated="+accelerated+" occluded="+occluded);
        Check(endFrame>middleFrame&&rendered>=2,"The main native viewport renders advancing playback frames: "+label);
        Check(!occluded,"The native viewport does not retain an occluded accelerated presenter: "+label);
        Check(start!=middle||start!=end,"The main native viewport presents changed pixels during playback: "+label);
    }
    static async Task MeleeSequentialUse(string output,AnimationLibrary melee,AnimationLibrary brawl)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Sequential native Use requires a private desktop.");
        using(var startup=MainForm.StartViewerApplication())
        {
            var form=(ModelForm)Field(MainForm.Instance,"_fighterForm");
            string phase="startup";long lastPulse=Stopwatch.GetTimestamp();
            using(var heartbeat=new Timer{Interval=50})
            try
            {
                heartbeat.Tick+=(s,e)=>{long now=Stopwatch.GetTimestamp();double gap=(now-lastPulse)*1000.0/Stopwatch.Frequency;if(gap>500)Console.WriteLine("UISTALL "+phase+" "+gap.ToString("0")+" ms");lastPulse=now;};heartbeat.Start();
                form.ClientSize=new Size(1600,900);
                var targetItem=brawl.Browse("Wait1").First(i=>i.Model?.Name=="FitFox00"&&i.Clip.Name=="Wait1");
                Call(form,"OpenSavedGameLibrary",brawl,brawl.Read().GameLocations.First(l=>l.Game=="Brawl"));
                Call(form,"SetSimpleWorkspaceLibraryVisible",true);
                await Flow39Select(form,targetItem);Call(form,"PauseSourceOperation");
                using(FlowModal("unsavedProjectDiscard",w=>SimpleControl<Button>(w,"unsavedProjectDiscard").PerformClick()))Call(form,"OpenLibraryAsTarget");
                await SimpleControlsReady(form);
                var editor=form.FighterEditor;
                var target=(MDL0Node)editor.TargetModel;
                Check(target.Name=="FitFox00"&&editor.SelectedCHR0?.Name=="Wait1","Sequential Use starts with the original editable Brawl target and Wait1");
                string targetHash=(string)Call(form,"NativeControlHash",target,".mdl0");
                Call(form,"OpenSavedGameLibrary",melee,melee.Read().GameLocations.First(l=>l.Game=="Melee"));
                SonicReviewNativeMapping(form,target,BrawlRetargeter.SkeletonFingerprint(target),output,"target");
                var rig=BrawlRetargeter.Capture(target);var roles=rig.Rig.Roles.ToDictionary(p=>p.Key,p=>rig.Bones[p.Value].Name);
                using(var axes=Modal("Rig Coordinates",window=>window.Controls.OfType<Button>().Single(b=>b.Text=="Use Coordinates").PerformClick()))
                    Check((bool)Call(form,"ReviewEngineCoordinates",target,roles),"Sequential Use confirms exact target coordinates");
                CHR0Node first=null;
                foreach(string actor in new[]{"Fox","Falco"})
                {
                    var sourceItem=melee.Browse("Wait1").First(i=>i.Model?.Character==actor&&i.Clip.Name=="Wait1");
                    if(actor=="Falco"&&Environment.GetEnvironmentVariable("FIGHTERPORT_TEST_QUICK_SECOND")=="1")
                    {
                        using(var donor=melee.Open(sourceItem.Clip.Id,sourceItem.Model.Id,System.Threading.CancellationToken.None))
                            SonicReviewNativeMapping(form,donor.Model,BrawlRetargeter.SkeletonFingerprint(donor.Model),output,actor+"-source");
                        SimpleControl<TextBox>(form,"librarySearch").Text=sourceItem.Clip.SourceCharacter+" "+sourceItem.Clip.Name;
                        ((Timer)Field(form,"_librarySearchTimer")).Stop();Call(form,"RefreshLibrary",sourceItem.Clip.Id);
                        await LibraryQuerySettled(form);
                        var tree=SimpleControl<TreeView>(form,"libraryClips");
                        tree.SelectedNode=AllNodes(tree.Nodes).Single(n=>(n.Tag as LibrarySearchItem)?.Clip.Id==sourceItem.Clip.Id);
                        Call(form,"StartLibraryAutoPreview");
                        Check(Field(form,"_libraryAutoCancel")!=null,"Second donor's automatic source preview was in flight before Use");
                    }
                    else
                    {
                        await Flow39Select(form,sourceItem);
                        var donor=(LibrarySourceLease)Field(form,"_libraryLease");
                        SonicReviewNativeMapping(form,donor.Model,BrawlRetargeter.SkeletonFingerprint(donor.Model),output,actor+"-source");
                    }
                    Call(form,"PauseSourceOperation");
                    Check(SimpleControl<Button>(form,"libraryUseOnTarget").Enabled,"Sequential Use enables the exact "+actor+" donor");
                    int autoEpochBefore=(int)Field(form,"_libraryAutoEpoch");
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
                        phase=actor+" Use";
                        SimpleControl<Button>(form,"libraryUseOnTarget").PerformClick();
                        errors.Stop();
                    }
                    form.RetargetKeyframeReviewReady=null;
                    Console.WriteLine("SEQUENTIAL "+actor+" Use returned after "+timer.Elapsed.TotalSeconds.ToString("0.0")+" s");
                    if(failure!=null)throw failure;
                    if(actor=="Falco"&&Environment.GetEnvironmentVariable("FIGHTERPORT_TEST_QUICK_SECOND")=="1")
                        Check((int)Field(form,"_libraryAutoEpoch")>autoEpochBefore,"Use cancels the in-flight source preview before publishing the target result");
                    phase=actor+" automatic controls";var setup=Stopwatch.StartNew();await SimpleControlsReady(form);
                    Console.WriteLine("SEQUENTIAL "+actor+" automatic controls ready after "+setup.Elapsed.TotalSeconds.ToString("0.0")+" s");
                    if(actor=="Falco"&&Environment.GetEnvironmentVariable("FIGHTERPORT_TEST_QUICK_SECOND")=="1")
                    {await GameLibraryWait(()=>Field(form,"_libraryLease") is LibrarySourceLease preview&&preview.Reference.ClipId==sourceItem.Clip.Id,"The selected original Falco preview did not resume after Use.");Check(true,"Original source preview resumes after the target import");}
                    phase=actor+" viewer";
                    var selected=editor.SelectedCHR0;
                    Check(reviewed&&form.FighterProject.Animations.Count==before+1&&selected!=null&&ReferenceEquals(editor.TargetModel,target),"Sequential "+actor+" Use imports into the existing native viewer");
                    editor.SetFrame(Math.Min(5,selected.FrameCount));
                    Check(ReferenceEquals(editor.SelectedCHR0,selected)&&editor.CurrentFrame==Math.Min(5,selected.FrameCount),"Viewer playhead responds after "+actor+" Use");
                    await VerifyViewerMotion(form,target,selected,actor+" after Use");
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
            finally{heartbeat.Stop();if(!form.IsDisposed)SimpleDisposeForm(form);}
        }
    }
}
