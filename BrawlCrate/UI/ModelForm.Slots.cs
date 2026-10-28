using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        internal Action<Form> AnimationSlotsReady;
        private int LoadGameSlotProfile(FighterAsset source)
        {
            var project=_fighterProject;var profile=NativeGameSlotProfile.Capture(source);if(project.GameSlotProfiles.Any(p=>p.SourceAsset==profile.SourceAsset&&p.SourceSha256==profile.SourceSha256))throw new InvalidOperationException("This exact native motion profile is already loaded.");
            var before=project.AnimationSlots.Select(s=>s.Copy()).ToArray();var profiles=project.GameSlotProfiles.ToArray();var after=before.Select(s=>s.Copy()).ToList();
            foreach(var entry in profile.Entries)
            {
                string id=entry.Name;if(id.Length>80||after.Any(s=>string.Equals(s.Id,id,StringComparison.OrdinalIgnoreCase)))continue;
                after.Add(new NativeAnimationSlot{Id=id,ProfileId=profile.Id,NativeEntry=entry.Entry,Required=false});
            }
            NativeAnimationSlot.Validate(after);if(project.GameSlotProfiles.Count>=16)throw new InvalidOperationException("The project already has 16 native motion profiles.");
            void Restore(bool added){project.AnimationSlots=(added?after:before.ToList()).Select(s=>s.Copy()).ToList();project.GameSlotProfiles=profiles.ToList();if(added)project.GameSlotProfiles.Add(profile);MarkFighterChanged();}
            Restore(true);modelEditControl1.RecordEditorCommand(()=>Restore(false),()=>Restore(true));return after.Count-before.Length;
        }
        private string GameSlotTemplate(NativeGameSlotProfile profile)
        {
            var asset=_fighterProject.Animations.Single(a=>a.Id==profile.SourceAsset);foreach(string path in new[]{asset.WorkingPath,asset.SourcePath})if(File.Exists(path)&&string.Equals(FighterProject.HashFile(path),profile.SourceSha256,StringComparison.OrdinalIgnoreCase))return path;
            throw new InvalidOperationException("The exact motion template is unavailable or changed. Re-register its authentic original before exporting a profiled slot.");
        }
        private void ExportGameSlot(string id,string destination)
        {
            RequireSavedAnimationExportTarget(modelEditControl1.TargetModel as MDL0Node);
            var slot=_fighterProject.AnimationSlots.Single(s=>s.Id==id);if(slot.ProfileId==null)throw new InvalidOperationException("Choose a source-bound native game slot.");var profile=_fighterProject.GameSlotProfiles.Single(p=>p.Id==slot.ProfileId);profile.Validate();GameSlotTemplate(profile);
            if(AnimationSlotStatus(slot)!="Assigned · exact native revision")throw new InvalidOperationException("Review this slot's exact animation/model revision before export.");var expected=profile.Entries.Single(e=>e.Entry==slot.NativeEntry);var clip=NativeSlotClips().Single(c=>c.Key==slot.AnimationKey).Clip;
            using(var template=NodeFactory.FromFile(null,GameSlotTemplate(profile)))
            {
                ResourceNode entry=template;var parts=expected.Entry.Split('/');if(parts[0]!="root")throw new InvalidDataException("Invalid native template entry.");foreach(string part in parts.Skip(1)){if(!int.TryParse(part,out int index)||index<0||index>=entry.Children.Count)throw new InvalidDataException("The original native slot entry is unavailable.");entry=entry.Children[index];}
                if(!(entry is CHR0Node authentic)||authentic.Name!=expected.Name||authentic.Version!=expected.Version||authentic.FrameCount!=expected.Samples||authentic.Loop!=expected.Loop)throw new InvalidDataException("The saved slot inventory differs from its exact authentic template entry.");
            }
            var model=modelEditControl1.TargetModel as MDL0Node??throw new InvalidOperationException("Open this slot's target model.");if(clip.Children.Any(c=>!model._linker.BoneCache.Any(b=>b.Name==c.Name)))throw new InvalidOperationException("This clip contains foreign native tracks. Retarget it and review losses first.");
            destination=Path.GetFullPath(destination);_fighterProject.ProtectOutputPath(destination);if(Path.GetExtension(destination).ToLowerInvariant()!=".chr0"||File.Exists(destination))throw new IOException("Choose a new .chr0 path; existing files remain protected.");
            modelEditControl1.StopAnim();FinishControlCurveEdit();
            var choice=NativeSlotClips().Single(c=>c.Key==slot.AnimationKey);
            using(var frozen=FreezeOwnedWorkspaceExport(choice.Asset,clip,false))
            {
                string frozenProfile=new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(profile),frozenSlot=new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(slot);
                var validate=frozen.Validate;
                frozen.Validate=()=>{validate();profile.Validate();GameSlotTemplate(profile);if(AnimationSlotStatus(slot)!="Assigned · exact native revision"||slot.AnimationKey!=choice.Key||new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(profile)!=frozenProfile||new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(slot)!=frozenSlot)throw new InvalidOperationException("The exact slot assignment or template changed. Review it before exporting.");};
                ProtectVerifiedExport(destination);
                CaptureWorkspaceExportDestination(frozen,destination);
                PrepareWorkspaceExportWithProgress(frozen,new NativeExportOptimizationPreferences().Read(),this);
                PrepareSlotMetadataExport(frozen,expected.Name,expected.Version);
                ExportWorkspaceSnapshot(frozen,destination);
            }
        }
        private void EditAnimationSlots(Action<List<NativeAnimationSlot>> change)
        {
            if(_controlBusy||_controlSetupQueued||_retargetBusy||_workspaceBusy||_pinGesture!=null)throw new InvalidOperationException("Finish the current native edit before changing slot assignments.");
            var project=_fighterProject;var before=project.AnimationSlots.Select(s=>s.Copy()).ToList();var after=before.Select(s=>s.Copy()).ToList();change(after);NativeAnimationSlot.Validate(after);
            void Restore(List<NativeAnimationSlot> value){project.AnimationSlots=value.Select(s=>s.Copy()).ToList();MarkFighterChanged();UpdateFighterStatus();}
            Restore(after);modelEditControl1.RecordEditorCommand(()=>Restore(before),()=>Restore(after));
        }
        private void AssignAnimationSlot(string id,string key)
        {
            if(!(modelEditControl1.TargetModel is MDL0Node model))throw new InvalidOperationException("Open the slot's native target model first.");
            RequireSavedAnimationExportTarget(model);
            var choice=NativeSlotClips().SingleOrDefault(c=>c.Key==key)??throw new InvalidOperationException("Choose an exact registered native animation.");
            EditAnimationSlots(slots=>{var slot=slots.Single(s=>s.Id==id);if(slot.Protected)throw new InvalidOperationException("Unlock this slot explicitly before assigning or clearing it.");slot.AnimationKey=key;slot.ModelSha256=NativeControlHash(model,".mdl0");slot.AnimationSha256=NativeControlHash(choice.Clip,".chr0");});
        }
        private TransitionClipChoice[] NativeSlotClips()=>RegisteredFighterAssets().SelectMany(a=>DiscoverFighterAnimations(FighterRoot(a)).OfType<CHR0Node>().Select(c=>new TransitionClipChoice{Asset=a,Clip=c,Key=FighterAnimationKey(a,c)})).ToArray();
        private string AnimationSlotStatus(NativeAnimationSlot slot)
        {
            if(slot.AnimationKey==null)return slot.Required?"Required · unassigned":"Optional · unassigned";
            var clip=NativeSlotClips().SingleOrDefault(c=>c.Key==slot.AnimationKey);if(clip==null)return "Missing animation";
            if(!(modelEditControl1.TargetModel is MDL0Node model))return "Model changed · review assignment";
            if(model.IsDirty)return "Save target model changes · review assignment";
            if(NativeControlHash(model,".mdl0")!=slot.ModelSha256)return "Model changed · review assignment";
            if(NativeControlHash(clip.Clip,".chr0")!=slot.AnimationSha256)return "Animation changed · review assignment";
            return "Assigned · exact native revision";
        }
        private void ShowAnimationSlots()
        {
            if(_fighterProject.Model==null)throw new InvalidOperationException("Open a fighter model before assigning animation slots.");
            modelEditControl1.StopAnim();var project=_fighterProject;
            using(var window=new Form{Text="Animation Slots / Assign to State",ClientSize=new Size(990,570),MinimumSize=new Size(850,480),StartPosition=FormStartPosition.CenterParent,ShowInTaskbar=false})
            {
                var heading=new Label{Dock=DockStyle.Top,Height=66,Padding=new Padding(8),Text="Animation slots · Custom intentions and source-bound Brawl motion profiles\r\nLoad a profile from the selected clip's original native motion file. Names/entries are verified against those exact bytes; game build, required/fallback rules and runtime action IDs remain unverified."};
                var grid=new DataGridView{Name="animationSlots",Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,RowHeadersVisible=false};
                foreach(string title in new[]{"Slot","Required","Protected","Assignment status","Native animation","Profile / native entry"})grid.Columns.Add(title,title);
                var row=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true,Padding=new Padding(8),WrapContents=true};var name=new TextBox{Name="slotName",Width=165};var required=new CheckBox{Name="slotRequired",Text="Required",AutoSize=true};var clips=new ComboBox{Name="slotClip",Width=300,DropDownStyle=ComboBoxStyle.DropDownList};clips.Items.AddRange(NativeSlotClips());if(clips.Items.Count>0)clips.SelectedIndex=0;
                var report=new Label{Name="slotReport",Dock=DockStyle.Bottom,Height=40,Padding=new Padding(8)};
                string Selected()=>grid.SelectedRows.Count==1?Convert.ToString(grid.SelectedRows[0].Cells[0].Value):null;
                void Refresh(string id=null){id=id??Selected();var natives=NativeSlotClips().ToDictionary(c=>c.Key);grid.Rows.Clear();foreach(var slot in project.AnimationSlots){TransitionClipChoice native=null;if(slot.AnimationKey!=null)natives.TryGetValue(slot.AnimationKey,out native);int i=grid.Rows.Add(slot.Id,slot.Required,slot.Protected,AnimationSlotStatus(slot),native?.Clip.Name??"—",slot.ProfileId==null?"Custom":"Brawl · "+slot.NativeEntry);if(slot.Id==id){grid.ClearSelection();grid.Rows[i].Selected=true;}}}
                void Run(Action action){try{if(_fighterProject!=project)throw new InvalidOperationException("The fighter project changed. Reopen slots.");action();report.Text="Saved in editor metadata. Save Fighter or commit the native workspace revision to persist.";}catch(Exception error){report.Text=error.Message;}Refresh();}
                Button Button(string key,string text,Action action){var button=new Button{Name=key,Text=text,AutoSize=true};button.Click+=(s,e)=>Run(action);return button;}
                row.Controls.AddRange(new Control[]{name,required,Button("addSlot","Add Custom Slot",()=>{string id=name.Text.Trim();EditAnimationSlots(list=>list.Add(new NativeAnimationSlot{Id=id,Required=required.Checked}));Refresh(id);}),clips,
                    Button("assignSlot","Assign / Review",()=>AssignAnimationSlot(Selected()??throw new InvalidOperationException("Select a slot."),(clips.SelectedItem as TransitionClipChoice)?.Key)),
                    Button("clearSlot","Clear",()=>EditAnimationSlots(list=>{var slot=list.Single(s=>s.Id==Selected());if(slot.Protected)throw new InvalidOperationException("Unlock this slot explicitly before clearing it.");slot.AnimationKey=slot.AnimationSha256=slot.ModelSha256=null;})),
                    Button("protectSlot","Lock / Unlock",()=>EditAnimationSlots(list=>{var slot=list.Single(s=>s.Id==Selected());slot.Protected=!slot.Protected;})),
                    Button("requireSlot","Required / Optional",()=>EditAnimationSlots(list=>{var slot=list.Single(s=>s.Id==Selected());if(slot.Protected)throw new InvalidOperationException("Unlock the slot before changing its reviewed requirement.");slot.Required=!slot.Required;})),
                    Button("removeSlot","Remove Slot",()=>EditAnimationSlots(list=>{var slot=list.Single(s=>s.Id==Selected());if(slot.Protected)throw new InvalidOperationException("Unlock this slot explicitly before removing it.");list.Remove(slot);})),
                    Button("openSlot","Open Assigned in Viewer",()=>{var slot=project.AnimationSlots.Single(s=>s.Id==Selected());if(slot.AnimationKey==null||!NativeSlotClips().Any(c=>c.Key==slot.AnimationKey))throw new InvalidOperationException("The slot has no available animation.");project.SelectedAnimation=slot.AnimationKey;project.Frame=1;RebuildTransitionScene();window.Close();})});
                row.Controls.Add(Button("loadGameSlots","Load Motion File Profile",()=>{var selectedClip=clips.SelectedItem as TransitionClipChoice??throw new InvalidOperationException("Choose a clip from the registered original motion template first.");int count=LoadGameSlotProfile(selectedClip.Asset);heading.Text="Loaded "+count+" optional native slots from the exact Brawl motion template. Existing/duplicate names were retained without substitution. Build and runtime requirements remain unverified.";}));row.Controls.Add(Button("exportGameSlot","Export Native Slot CHR0...",()=>{using(var file=new SaveFileDialog{Title="Export Assigned Native Slot",Filter="Brawl bone animation (*.chr0)|*.chr0",FileName="slot.chr0"})if(file.ShowDialog(window)==DialogResult.OK)ExportGameSlot(Selected()??throw new InvalidOperationException("Select an assigned native slot."),file.FileName);}));var close=Button("closeSlots","Close",()=>window.Close());row.Controls.Add(close);window.Controls.Add(grid);window.Controls.Add(report);window.Controls.Add(row);window.Controls.Add(heading);Refresh();DarkTheme.Apply(window);window.Shown+=(s,e)=>AnimationSlotsReady?.Invoke(window);window.ShowDialog(this);
            }
        }
    }
}
