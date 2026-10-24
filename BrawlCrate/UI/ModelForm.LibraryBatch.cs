using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Web.Script.Serialization;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        internal Action<Form> LibraryBatchReady;
        private sealed class LibraryBatchDraft:IDisposable
        {
            internal LibraryClipReference Source;internal string SourceName,SourceModelPath;internal RetargetResult Result;internal NativeTimingFit Timing;
            public void Dispose(){Result?.Dispose();}
        }
        private void ShowLibraryBatch()
        {
            if(!(modelEditControl1.TargetModel is MDL0Node target)||_controlBusy||_retargetBusy||_workspaceBusy)return;
            modelEditControl1.StopAnim();_libraryTimer.Stop();_libraryPlay.Text="Play";
            var query=CurrentLibraryQuery();var items=LibraryWork.Run(this,"Loading Batch Catalogue",(cancel,progress)=>{cancel.ThrowIfCancellationRequested();return _library.Search(query,0,out _,200);});
            if(items==null)return;
            var project=_fighterProject;string targetHash=NativeControlHash(target,".mdl0");var mode=project.GroundMode;
            var drafts=new List<LibraryBatchDraft>();bool working=false;
            using(var window=new Form{Text="Batch Retarget and Timing Fit",ClientSize=new Size(1040,590),MinimumSize=new Size(820,420),ShowInTaskbar=false,StartPosition=FormStartPosition.CenterParent})
            {
                var grid=new DataGridView{Name="batchClips",Dock=DockStyle.Fill,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
                grid.Columns.Add(new DataGridViewCheckBoxColumn{Name="Use",HeaderText="Use",FillWeight=28});
                grid.Columns.Add(new DataGridViewTextBoxColumn{Name="Clip",HeaderText="Original clip / character",ReadOnly=true,FillWeight=180});
                foreach(string name in new[]{"Start","End","Frames"})grid.Columns.Add(new DataGridViewTextBoxColumn{Name=name,HeaderText=name=="Frames"?"Output frames*":name+" frame",FillWeight=60});
                grid.Columns.Add(new DataGridViewTextBoxColumn{Name="Result",HeaderText="Review / channel losses",ReadOnly=true,FillWeight=260});
                foreach(var item in items){int row=grid.Rows.Add(false,item.Clip.Name+" / "+(item.Model?.Character??"Unbound"),1,item.Clip.FrameCount,0,LibraryStateText(item.State));grid.Rows[row].Tag=item;}
                var heading=new Label{Dock=DockStyle.Top,Height=68,Padding=new Padding(8),Text="Original Next retargeter · "+mode+" contacts · target "+target.Name+"\nFrames are numbered from 1. Trim and timing fits resample the source before retargeting; fitted results are non-looping.\n*0 uses the shared frame count; shared 0 keeps the selected source range. Originals remain unchanged. Showing up to 200 matches; filter in the library to narrow the list."};
                var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true,Padding=new Padding(6)};
                var details=new TextBox{Name="batchDetails",Dock=DockStyle.Bottom,Height=76,ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Vertical};
                Action showDetails=()=>details.Text=grid.CurrentRow==null?"Select a result to read its full report.":Convert.ToString(grid.CurrentRow.Cells["Clip"].Value)+Environment.NewLine+Convert.ToString(grid.CurrentRow.Cells["Result"].Value);
                grid.SelectionChanged+=(s,e)=>showDetails();grid.CurrentCellChanged+=(s,e)=>showDetails();grid.CellValueChanged+=(s,e)=>showDetails();
                var shared=new NumericUpDown{Name="sharedFrames",Minimum=0,Maximum=4096,Width=70};
                var review=new CheckBox{Name="reviewBatch",Text="I reviewed results and listed channel losses",AutoSize=true};
                var prepare=new Button{Name="prepareBatch",Text="Retarget Selected",AutoSize=true};var apply=new Button{Name="applyBatch",Text="Add Reviewed Results",AutoSize=true,Enabled=false};
                var select=new Button{Text="Select Available",AutoSize=true};select.Click+=(s,e)=>{foreach(DataGridViewRow row in grid.Rows)row.Cells["Use"].Value=((LibrarySearchItem)row.Tag).State==LibrarySourceState.Available;};
                actions.Controls.Add(select);actions.Controls.Add(new Label{Text="Shared frames",AutoSize=true,Padding=new Padding(0,6,0,0)});actions.Controls.Add(shared);actions.Controls.Add(prepare);actions.Controls.Add(review);actions.Controls.Add(apply);
                Action clear=()=>{foreach(var draft in drafts)draft.Dispose();drafts.Clear();review.Checked=false;apply.Enabled=false;};
                grid.CellValueChanged+=(s,e)=>{if(!working&&e.RowIndex>=0&&e.ColumnIndex>=0&&!grid.Columns[e.ColumnIndex].ReadOnly)clear();};
                grid.CurrentCellDirtyStateChanged+=(s,e)=>{if(grid.IsCurrentCellDirty)grid.CommitEdit(DataGridViewDataErrorContexts.Commit);};
                grid.DataError+=(s,e)=>{e.ThrowException=false;};shared.ValueChanged+=(s,e)=>clear();
                review.CheckedChanged+=(s,e)=>apply.Enabled=review.Checked&&drafts.Count>0&&!working;
                Action current=()=>{if(IsDisposed||_fighterProject!=project||modelEditControl1.TargetModel!=target||project.GroundMode!=mode||NativeControlHash(target,".mdl0")!=targetHash)throw new InvalidOperationException("The target or contact options changed. Retarget the batch again.");};
                prepare.Click+=(s,e)=>
                {
                    grid.EndEdit();clear();working=true;grid.Enabled=false;shared.Enabled=false;prepare.Enabled=false;
                    try
                    {
                        current();var rows=grid.Rows.Cast<DataGridViewRow>().Where(r=>Equals(r.Cells["Use"].Value,true)).ToArray();
                        if(rows.Length>64)throw new InvalidOperationException("Select at most 64 clips per review batch.");
                        long preparedKeys=0;
                        foreach(var row in rows)
                        {
                            LibrarySourceLease source=null;RetargetResult result=null;
                            try
                            {
                                var item=(LibrarySearchItem)row.Tag;source=LoadLibrarySource(item.Clip.Id);if(source==null)break;
                                int start=Convert.ToInt32(row.Cells["Start"].Value)-1,end=Convert.ToInt32(row.Cells["End"].Value)-1,count=Convert.ToInt32(row.Cells["Frames"].Value);
                                if(count==0)count=(int)shared.Value;if(count==0)count=end-start+1;
                                var timing=new NativeTimingFit{Start=start,End=end,Frames=count};timing.Validate(source.Animation.FrameCount);
                                if(timing.Identity(source.Animation.FrameCount)){timing=null;result=BakeWithProgress(source.Model,target,source.Animation);}
                                else using(var fitted=timing.Resample(source.Animation))result=BakeWithProgress(source.Model,target,fitted);
                                if(result==null){row.Cells["Result"].Value="Cancelled; no result added";break;}
                                long keys=result.Animation.Children.OfType<CHR0EntryNode>().Sum(entry=>entry.KeyArrays.Sum(channel=>(long)channel.KeyCount));
                                if(preparedKeys+keys>2000000)throw new InvalidOperationException("This review exceeds two million native keys. Use a smaller batch.");preparedKeys+=keys;
                                current();row.Cells["Result"].Value="Review: limb "+result.MaximumLimbResidual.ToString("G4")+", contact "+result.MaximumContactResidual.ToString("G4")+"; omitted: "+(result.IgnoredSourceBones.Length==0?"none":string.Join(", ",result.IgnoredSourceBones));
                                row.Cells["Result"].ToolTipText=Convert.ToString(row.Cells["Result"].Value);
                                drafts.Add(new LibraryBatchDraft{Source=source.Reference.Copy(),SourceName=source.Animation.Name,SourceModelPath=source.ModelPath,Result=result,Timing=timing});result=null;
                            }
                            catch(Exception error){row.Cells["Result"].Value="Failed: "+error.Message;}
                            finally{source?.Dispose();result?.Dispose();}
                        }
                    }
                    catch(Exception error){_fighterStatus.Text="Batch: "+error.Message;clear();}
                    finally{working=false;grid.Enabled=true;shared.Enabled=true;prepare.Enabled=true;}
                };
                apply.Click+=(s,e)=>
                {
                    try
                    {
                        current();if(!review.Checked||drafts.Count==0)return;
                        if(_controlBusy||_retargetBusy||_workspaceBusy)throw new InvalidOperationException("Finish the current edit before adding results.");
                        var json=new JavaScriptSerializer();
                        foreach(var draft in drafts)using(var verified=_library.Open(draft.Source.ClipId))
                            if(json.Serialize(verified.Reference)!=json.Serialize(draft.Source))throw new InvalidOperationException("A library source changed. Retarget the batch again.");
                        var root=FighterRoot(project.Model);if(root.IsDirty){root.Merge();root.ExportChecked(project.Model.WorkingPath);}
                        var assets=drafts.Select(d=>project.CreateLibraryRevision(d.Source,d.SourceName,d.SourceModelPath,d.Result,d.Timing)).ToArray();
                        foreach(var asset in assets)FighterRoot(asset);current();
                        RememberFighterImport();project.Animations.AddRange(assets);project.SelectedAnimation=FighterAnimationKey(assets[0],FighterRoot(assets[0]));project.Frame=1;
                        RebuildFighterScene();if(drafts[0].Result.Contacts.FloorEnabled)modelEditControl1.QueueGroundContactView();else modelEditControl1.QueueFrameFighterModel();
                        MarkFighterChanged();UpdateFighterStatus();_fighterStatus.Text="Added "+assets.Length+" reviewed independent Next results. Save Fighter to keep the batch.";
                        window.Close();
                    }
                    catch(Exception error){_fighterStatus.Text="Batch: "+error.Message;clear();}
                };
                window.FormClosing+=(s,e)=>e.Cancel=working;
                window.Controls.Add(grid);window.Controls.Add(heading);window.Controls.Add(details);window.Controls.Add(actions);DarkTheme.Apply(window);
                window.Shown+=(s,e)=>LibraryBatchReady?.Invoke(window);
                try{window.ShowDialog(this);}finally{clear();}
            }
        }
    }
}
