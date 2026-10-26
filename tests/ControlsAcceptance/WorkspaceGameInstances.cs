using BrawlCrate.Studio;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class Program
{
    static void SourceManagerSelect(Form dialog,string id)
    {
        var grid=SimpleControl<DataGridView>(dialog,"savedGameSources");
        grid.CurrentCell=null;
        grid.CurrentCell=grid.Rows.Cast<DataGridViewRow>().Single(r=>Convert.ToString(r.Tag)==id).Cells[0];
    }
    static void SourceManager(ModelForm form,Action<Form> action)
    {
        using(FlowModal("savedGameSources",dialog=>{action(dialog);SimpleControl<Button>(dialog,"savedSourceClose").PerformClick();}))
            NextItem(form,"nextSavedGameSources").PerformClick();
    }
    static void GameInstanceFolder(string root,string layout,string model,string motion,bool change)
    {
        string character=Path.Combine(root,layout,"fighter","mario");Directory.CreateDirectory(character);
        File.Copy(model,Path.Combine(character,"FitMario00.pac"));string path=Path.Combine(character,"FitMarioMotionEtc.pac");File.Copy(motion,path);
        if(change)
        {
            string candidate=path+".candidate";
            using(var owner=NodeFactory.FromFile(null,path))
            {var clip=Walk(owner).OfType<CHR0Node>().Single(c=>c.Name=="Wait1");clip.Loop=!clip.Loop;owner.Merge();owner.ExportChecked(candidate);}
            File.Copy(candidate,path,true);
        }
    }
    static async Task GameInstancesAndManagement(ModelForm form,AnimationLibrary library,string[] args,string output)
    {
        string fixtures=Path.Combine(output,"source-fixtures");var future=library.Read().GameLocations.First(l=>l.Game=="Future game fixture"&&!l.IsDirectory);
        string edited=Path.Combine(fixtures,"edited-future.iso");File.WriteAllBytes(edited,new byte[]{1,2,3,4});
        SourceManager(form,dialog=>{
            var grid=SimpleControl<DataGridView>(dialog,"savedGameSources");Check(grid.Rows.Count==library.Read().GameLocations.Count,"Compact manager lists every saved disc, install folder and indexed source");
            SourceManagerSelect(dialog,future.Id);SimpleControl<TextBox>(dialog,"savedSourceName").Text="Future disc A";SimpleControl<TextBox>(dialog,"savedSourcePath").Text=edited;
            Check(Convert.ToString(Field(dialog,"selected"))==future.Id&&SimpleControl<Button>(dialog,"savedSourceSave").Enabled,"Compact row selection enables editing its exact saved source");
            SimpleControl<Button>(dialog,"savedSourceSave").PerformClick();var saved=library.Read().GameLocations.Single(l=>l.Id==future.Id);
            Check(saved.Label=="Future disc A"&&saved.Path==edited&&!saved.Indexed,"Compact editor changes name and path while retaining the exact saved source ID: "+SimpleControl<Label>(dialog,"savedSourceStatus").Text);
            string before=NextLibraryFiles(library);SimpleControl<TextBox>(dialog,"savedSourcePath").Text=Path.Combine(fixtures,"absent.iso");SimpleControl<Button>(dialog,"savedSourceSave").PerformClick();
            Check(NextLibraryFiles(library)==before,"An invalid path edit preserves the previous source, name and catalogue bytes");
            SourceManagerSelect(dialog,future.Id);SimpleControl<Button>(dialog,"savedSourceRemove").PerformClick();
            Check(library.Read().GameLocations.All(l=>l.Id!=future.Id)&&File.Exists(edited),"Removing a saved path unregisters its entry and keeps the actual file");
            SimpleControl<Button>(dialog,"savedSourceUndo").PerformClick();Check(library.Read().GameLocations.Any(l=>l.Id==future.Id&&l.Label=="Future disc A"),"Existing library Undo restores a removed source with its exact identity and edited name");
            CaptureDialog(dialog,output,"saved-source-manager.png");
        });
        foreach(int text in new[]{100,175})
        {
            Call(form,"SetWorkspaceTextSize",text);
            SourceManager(form,dialog=>{
                dialog.ClientSize=text==100?new Size(760,480):new Size(1000,800);dialog.PerformLayout();
                Check(SimpleControl<DataGridView>(dialog,"savedGameSources").Height>=80&&new[]{"savedSourceSave","savedSourceRemove","savedSourceUndo","savedSourceClose"}.All(n=>InClient(SimpleControl<Button>(dialog,n),dialog)),"Compact saved-source list/editor/actions stay visible at text "+text);
                Check(new[]{"savedSourceSave","savedSourceRemove","savedSourceUndo","savedSourceClose","savedSourceFile","savedSourceFolder"}.Select(n=>SimpleControl<Button>(dialog,n)).All(b=>b.Width-12>=TextRenderer.MeasureText(b.Text,b.Font).Width),"Saved-source action and path-picker captions fit completely at text "+text);
                CaptureDialog(dialog,output,"saved-source-manager-"+text+".png");
            });
        }
        Call(form,"SetWorkspaceTextSize",100);
        string one=Path.Combine(fixtures,"PM-one"),two=Path.Combine(fixtures,"PM-two"),relocated=Path.Combine(fixtures,"PM-relocated");
        GameInstanceFolder(one,Path.Combine("projectm","pf"),args[0],args[1],false);
        GameInstanceFolder(two,Path.Combine("private","wii","app","rsbe","pf"),args[0],args[1],true);
        GameInstanceFolder(relocated,Path.Combine("projectm","pf"),args[0],args[1],true);
        const string build="Same explicit PM fixture build";
        foreach(string path in new[]{one,two})
        {
            using(FlowModal("discFilePath",dialog=>{
                SimpleControl<ComboBox>(dialog,"gameSourceSaved").SelectedIndex=0;SimpleControl<ComboBox>(dialog,"gameSourceGame").Text="Project M";
                SimpleControl<TextBox>(dialog,"discBuild").Text=build;SimpleControl<ComboBox>(dialog,"gameSourceKind").SelectedIndex=1;SimpleControl<TextBox>(dialog,"discFilePath").Text=path;
                SimpleControl<Button>(dialog,"discContinue").PerformClick();Check(dialog.DialogResult==DialogResult.OK,"Actual Add game accepts the Project M SD layout without a character picker");
            }))NextItem(form,"nextAddBrawlDisc").PerformClick();
            await GameLibraryWait(()=>!(bool)Field(form,"_libraryReplacingTree"),"Project M source did not publish its outline");
        }
        var data=library.Read();var pm=data.GameLocations.Where(l=>l.Game=="Project M"&&l.Indexed).ToArray();
        Check(pm.Length==2&&pm.Select(l=>l.Id).Distinct().Count()==2&&pm.Select(l=>l.InventorySha256).Distinct().Count()==2,"Two Project M installs with the same build label and character names remain distinct source instances/content inventories");
        var first=pm.Single(l=>l.Path==one);var second=pm.Single(l=>l.Path==two);
        var firstRows=library.Browse(new LibraryQuery{GameSourceId=first.Id,Text="Wait1",Channel="CHR0 skeletal"});
        var secondRows=library.Browse(new LibraryQuery{GameSourceId=second.Id,Text="Wait1",Channel="CHR0 skeletal"});
        var a=firstRows.Single(r=>r.State==LibrarySourceState.Available);var b=secondRows.Single(r=>r.State==LibrarySourceState.Available);
        Check(a.Clip.Id!=b.Clip.Id&&a.Model.Id!=b.Model.Id&&a.Clip.ResourceSha256!=b.Clip.ResourceSha256&&a.Clip.Loop!=b.Clip.Loop,"Duplicate character/Wait1 labels retain separate exact model/animation IDs and the independently changed native content");
        Check(library.GetCostumes(b.Clip.Id).All(c=>data.Mounts.Single(m=>m.Id==data.Models.Single(model=>model.Id==c.ModelId).MountId).GameSourceId==second.Id),"Project M costume options never borrow same-name models from Brawl or another install");
        SourceManager(form,dialog=>{
            SourceManagerSelect(dialog,first.Id);SimpleControl<TextBox>(dialog,"savedSourceName").Text="Legacy build";SimpleControl<Button>(dialog,"savedSourceSave").PerformClick();
            SourceManagerSelect(dialog,second.Id);SimpleControl<TextBox>(dialog,"savedSourceName").Text="My modpack";SimpleControl<Button>(dialog,"savedSourceSave").PerformClick();
            string before=NextLibraryFiles(library);SimpleControl<TextBox>(dialog,"savedSourceName").Text="Legacy build";SimpleControl<Button>(dialog,"savedSourceSave").PerformClick();
            Check(NextLibraryFiles(library)==before,"Conflicting instance names reject without changing verified source metadata");
            SourceManagerSelect(dialog,second.Id);SimpleControl<TextBox>(dialog,"savedSourcePath").Text=one;SimpleControl<Button>(dialog,"savedSourceSave").PerformClick();
            Check(NextLibraryFiles(library)==before,"Changing an indexed PM path to different same-name native content fails verification atomically");
            SourceManagerSelect(dialog,second.Id);SimpleControl<TextBox>(dialog,"savedSourcePath").Text=relocated;SimpleControl<Button>(dialog,"savedSourceSave").PerformClick();
            var moved=library.Read();Check(moved.GameLocations.Single(l=>l.Id==second.Id).Path==relocated&&moved.GameLocations.Single(l=>l.Id==second.Id).Label=="My modpack"&&moved.Mounts.Where(m=>m.GameSourceId==second.Id).All(m=>m.LocalPath.StartsWith(relocated,StringComparison.OrdinalIgnoreCase)),"Verified folder relocation retains the instance name, exact source/model/clip IDs and changes all package paths in one transaction");
            CaptureDialog(dialog,output,"project-m-instances-manager.png");
        });
        Call(form,"ClearLibraryFilters");((Timer)Field(form,"_librarySearchTimer")).Stop();Call(form,"RefreshLibrary",new object[]{null});
        await GameLibraryWait(()=>!(bool)Field(form,"_libraryReplacingTree"),"Combined source categories did not settle");
        var tree=SimpleControl<TreeView>(form,"libraryClips");
        Check(tree.Nodes.Cast<TreeNode>().Any(n=>n.Text.StartsWith("Brawl ·"))&&tree.Nodes.Cast<TreeNode>().Any(n=>n.Text=="Project M · Legacy build")&&tree.Nodes.Cast<TreeNode>().Any(n=>n.Text=="Project M · My modpack"),"Brawl and both renamed Project M installs appear as separate library categories");
        await SourceFitSelect(form,b.Clip.Id);SimpleControl<Button>(form,"libraryPreviewOriginal").PerformClick();
        var lease=(LibrarySourceLease)Field(form,"_libraryLease");Check(lease.Reference.GameSourceId==second.Id&&lease.Reference.ClipId==b.Clip.Id&&lease.Animation.Loop==b.Clip.Loop,"Small source preview resolves the selected renamed/relocated PM instance's exact animation, not another same-name Wait1");
        await CaptureSimpleWorkspace(form,Path.Combine(output,"project-m-source-categories.png"));
        Call(form,"ReleaseLibraryPreview");
        using(FlowModal("discFilePath",dialog=>{
            var choices=SimpleControl<ComboBox>(dialog,"gameSourceSaved");choices.SelectedItem=choices.Items.Cast<object>().Single(o=>o.ToString().Contains("My modpack"));
            SimpleControl<Button>(dialog,"discContinue").PerformClick();
        }))NextItem(form,"nextAddBrawlDisc").PerformClick();
        await GameLibraryWait(()=>!(bool)Field(form,"_libraryReplacingTree"),"Named PM library scope did not open");
        var scoped=(LibraryQuery)Call(form,"CurrentLibraryQuery");
        Check(scoped.GameSourceId==second.Id&&library.Browse(scoped).All(i=>i.Motion.GameSourceId==second.Id),"Opening a named PM instance filters by exact source ID, even when multiple installations share game/build and character labels");
        string oldPath=library.Read().GameLocations.Single(l=>l.Id==second.Id).Path;
        using(var cancel=new System.Threading.CancellationTokenSource())
        {
            bool cancelled=false;try{library.EditSavedGameSource(second.Id,"Project M",build,two,cancel.Token,message=>cancel.Cancel());}catch(OperationCanceledException){cancelled=true;}
            Check(cancelled&&library.Read().GameLocations.Single(l=>l.Id==second.Id).Path==oldPath,"Cancelled folder verification publishes no partial path or alias change");
        }
        var beforeRemove=library.Read();var firstMounts=beforeRemove.Mounts.Where(m=>m.GameSourceId==first.Id).Select(m=>m.Id).ToArray();
        string[] ids=beforeRemove.Clips.Where(c=>firstMounts.Contains(c.MountId)).Select(c=>c.Id).OrderBy(x=>x).ToArray();
        SourceManager(form,dialog=>{
            SourceManagerSelect(dialog,first.Id);SimpleControl<Button>(dialog,"savedSourceRemove").PerformClick();var removed=library.Read();
            Check(removed.GameLocations.All(l=>l.Id!=first.Id)&&removed.Mounts.All(m=>m.GameSourceId!=first.Id)&&removed.GameLocations.Any(l=>l.Id==second.Id)&&Directory.Exists(one),"Removing one indexed PM instance unregisters only its library entries, keeping other installs and all physical files");
            SimpleControl<Button>(dialog,"savedSourceUndo").PerformClick();var restored=library.Read();var mounts=restored.Mounts.Where(m=>m.GameSourceId==first.Id).Select(m=>m.Id).ToArray();
            Check(restored.Clips.Where(c=>mounts.Contains(c.MountId)).Select(c=>c.Id).OrderBy(x=>x).SequenceEqual(ids),"Library Undo restores every exact animation identity after removing an indexed instance");
        });
        Check(library.Read().GameLocations.Count(l=>l.Game=="Project M"&&l.Indexed)==2,"Both independent Project M instances survive source management and fresh catalogue reads");
        string identical=Path.Combine(fixtures,"PM-identical-copy");GameInstanceFolder(identical,Path.Combine("projectm","pf"),args[0],args[1],false);
        var plan=BrawlGameSource.Prepare(new LibraryGameRequest{Game="Project M",GameVersion=build,Path=identical,ApproveBuild=true},Path.Combine(output,"identity-cache"));library.RegisterGame(plan);
        var sameBytes=library.Read().GameLocations.Single(l=>l.Indexed&&l.Path==identical);
        Check(sameBytes.Id!=first.Id&&sameBytes.InventorySha256==first.InventorySha256&&library.Read().GameLocations.Count(l=>l.Game=="Project M"&&l.Indexed)==3,"Even byte-identical PM installs at different saved roots retain independent instance IDs and can be renamed separately");
        library.RemoveSavedGameSource(sameBytes.Id);
    }
}
