using BrawlCrate.Studio;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        internal Action<Form> TransitionDetailsReady;
        private void ShowTransitionFitDetails()
        {
            var asset=SelectedFighterAsset();var recipe=asset?.TransitionFit??throw new InvalidOperationException("Select an animation made by Fit / Transitions first.");recipe.Validate();
            var options=new JavaScriptSerializer().Deserialize<TransitionFitOptions>(recipe.Options);
            int originalFrames=recipe.Inputs.Single(input=>input.Role=="source").Samples;
            bool edited=!string.Equals(recipe.InitialResultSha256,NativeControlHash(modelEditControl1.SelectedCHR0,".chr0"),StringComparison.OrdinalIgnoreCase);
            using(var window=new Form{Text="Transition Fit Details / Frozen References",ClientSize=new Size(830,520),MinimumSize=new Size(650,400),ShowInTaskbar=false,StartPosition=FormStartPosition.CenterParent})
            {
                var report=new TextBox{Name="transitionDetailsReport",Dock=DockStyle.Fill,ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Both,WordWrap=false};
                report.Text=(edited?"Edited after fitting; the saved receipt describes the initial fit.":"Current native animation matches the saved initial fit.")+"\r\nDuration: "+modelEditControl1.SelectedCHR0.FrameCount+" samples. Loop: "+modelEditControl1.SelectedCHR0.Loop+".\r\nEntry / exit fit samples: "+options.EntrySamples+" / "+options.ExitSamples+".\r\nSelected native bones: "+string.Join(", ",options.Bones)+"\r\nProtected frames: "+string.Join("; ",options.Protected.Select(p=>(p.Start+1)+"–"+(p.End+1)))+"\r\n\r\nFrozen native inputs (portable in this fighter project):\r\n"+string.Join("\r\n",recipe.Inputs.Select(i=>i.Role+": "+i.Name+" · "+i.Samples+" samples · SHA256 "+i.Sha256))+"\r\n\r\nFK component fitting; contact, clearance, joint-limit and game-state behavior remain unvalidated. Refit uses the current animation and live references, retaining later edits outside its new fit ranges.\r\n";
                report.AppendText("\r\nContact policy: "+(options.ExitContactRecovery?"Explicit hip / leg recovery after display frame "+(originalFrames-options.ExitSamples+1)+" through "+originalFrames+", using the authentic frozen first reference body pose. Earlier contact curves and source-authored motion controls stay protected. Fixed or overlapping pins cannot be silently released.\r\n"+(recipe.Algorithm=="contact-exit-recovery-1"?"Historical root-compatible endpoint policy.":"The reference body is evaluated beneath the source-authored protected local controls at each time. Source root keys and travel remain unchanged; no reference roots or yaw are substituted.")+"\r\n"+(options.PreventFootDipping?"Reviewed floor Y="+options.FloorHeight.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+". Extra penetration is bounded against the minimum of zero, original motion and authentic reference body under the same source-root placement. Authored endpoint penetration is retained.\r\n":"")+"Saved recovery receipt (exact released paths, span and floor evidence):\r\n"+recipe.Receipt:options.PreserveContacts?"Preserve complete existing contact dependency paths for the whole clip.":"Ordinary FK fit without identified contact trajectories.")+"\r\n");
                var row=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true,Padding=new Padding(6)};var choice=new ComboBox{Name="frozenTransitionInput",DropDownStyle=ComboBoxStyle.DropDownList,Width=300};foreach(var input in recipe.Inputs)choice.Items.Add(input.Role+": "+input.Name);choice.SelectedIndex=0;row.Controls.Add(choice);
                var export=new Button{Text="Export Frozen CHR0...",AutoSize=true};export.Click+=(s,e)=>RunFighterAction(()=>{var input=recipe.Inputs[choice.SelectedIndex];using(var dialog=new SaveFileDialog{Title="Export Exact Frozen Native Reference",Filter="Brawl bone animation (*.chr0)|*.chr0",FileName=input.Name+" (frozen).chr0"})if(dialog.ShowDialog(window)==DialogResult.OK)ExportFrozenTransitionInput(recipe,input,dialog.FileName);});row.Controls.Add(export);var close=new Button{Text="Close",AutoSize=true};close.Click+=(s,e)=>window.Close();row.Controls.Add(close);window.Controls.Add(report);window.Controls.Add(row);DarkTheme.Apply(window);window.Shown+=(s,e)=>TransitionDetailsReady?.Invoke(window);window.ShowDialog(this);
            }
        }
        private void ExportFrozenTransitionInput(NativeTransitionFit recipe,TransitionNativeInput input,string path)
        {
            recipe.Validate();if(!recipe.Inputs.Contains(input))throw new InvalidOperationException("Select an input from this exact saved recipe.");
            path=Path.GetFullPath(path);_fighterProject.ProtectOutputPath(path);if(Path.GetExtension(path).ToLowerInvariant()!=".chr0")throw new InvalidOperationException("Choose a new .chr0 output for the exact frozen native bytes.");using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){byte[] bytes=Convert.FromBase64String(input.Snapshot);stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
        }
    }
}
