using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlCrate.UI;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class Program
{
    static async Task WorkspaceGameSources(string[] args,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Saved game sources require a private desktop.");
        string catalogue=args[Array.IndexOf(args,"--library-index")+1],directory=Path.Combine(output,"library");Directory.CreateDirectory(directory);
        File.Copy(catalogue,Path.Combine(directory,"catalogue.xml"));Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT",directory);
        string fixtures=Path.Combine(output,"source-fixtures");Directory.CreateDirectory(fixtures);
        string image=Path.Combine(fixtures,"future-game.iso"),second=Path.Combine(fixtures,"second-disc.iso"),folder=Path.Combine(fixtures,"installed-game"),rvz=Path.Combine(fixtures,"future-brawl.rvz");
        File.WriteAllBytes(image,new byte[]{1,2,3,4});File.WriteAllBytes(second,new byte[]{5,6,7,8});File.WriteAllBytes(rvz,new byte[]{9,10,11,12});Directory.CreateDirectory(folder);
        const string future="Future game fixture",build="Fixture build; adapter unavailable";
        using(var startup=MainForm.StartViewerApplication())
        {
            var form=(ModelForm)Field(MainForm.Instance,"_fighterForm");
            try
            {
                await SimpleControlsReady(form);var library=new AnimationLibrary();var project=form.FighterProject;string document=project.DocumentIdentity();bool dirty=(bool)Field(form,"_fighterDirty");
                int clips=library.ClipCount,opens=AnimationLibrary.DiagnosticOpens,bakes=BrawlRetargeter.DiagnosticBakes,discs=BrawlDiscImage.DiagnosticOpens;
                void Save(string source,bool isFolder,string gameName=future)
                {
                    using(FlowModal("discFilePath",dialog=>{
                        SimpleControl<ComboBox>(dialog,"gameSourceGame").Text=gameName;
                        SimpleControl<TextBox>(dialog,"discBuild").Text=build;
                        SimpleControl<ComboBox>(dialog,"gameSourceKind").SelectedIndex=isFolder?1:0;
                        SimpleControl<TextBox>(dialog,"discFilePath").Text=source;
                        Check(SimpleControl<Button>(dialog,"discContinue").Text=="Save source"&&SimpleControl<Label>(dialog,"discValidation").Text.Contains("not available"),"Unsupported game clearly saves a location without claiming a working reader");
                        SimpleControl<Button>(dialog,"discContinue").PerformClick();Check(dialog.DialogResult==DialogResult.OK,"Valid disc/file or install-folder location can be remembered");
                    }))NextItem(form,"nextAddBrawlDisc").PerformClick();
                }
                Save(image,false);Save(folder,true);Save(second,false);
                var saved=new AnimationLibrary().Read().GameLocations.Where(l=>l.Game==future).ToArray();
                Check(saved.Length==3&&saved.Count(l=>l.IsDirectory)==1&&saved.All(l=>!l.Indexed&&l.Provider=="path-only")&&saved.Select(l=>l.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count()==3,"Fresh library reopen retains multiple discs and an install folder for the same future game/build as separate exact unindexed sources");
                string index=NextLibraryFiles(library);Save(image,false);Check(NextLibraryFiles(library)==index,"Remembering the same exact saved source is idempotent and preserves catalogue bytes");
                foreach(int text in new[]{100,175})
                {
                    Call(form,"SetWorkspaceTextSize",text);
                    foreach(int width in new[]{1000,640})
                    using(FlowModal("discFilePath",dialog=>{
                        dialog.ClientSize=new Size(width,text==175?820:560);dialog.PerformLayout();
                        var choices=SimpleControl<ComboBox>(dialog,"gameSourceSaved");
                        foreach(string location in new[]{image,folder,second})
                        {
                            choices.SelectedItem=choices.Items.Cast<object>().Single(o=>o.ToString().Contains(location));
                            Check(SimpleControl<TextBox>(dialog,"discFilePath").Text==location&&SimpleControl<ComboBox>(dialog,"gameSourceGame").Text==future&&SimpleControl<TextBox>(dialog,"discBuild").Text==build&&SimpleControl<ComboBox>(dialog,"gameSourceKind").SelectedIndex==(location==folder?1:0),"Saved source picker recalls exact game, build, path and file/folder kind at "+width+"/"+text);
                        }
                        Check(InClient(SimpleControl<Button>(dialog,"discContinue"),dialog)&&InClient(SimpleControl<Button>(dialog,"discCancel"),dialog),"Saved-source actions remain reachable at "+width+"/"+text);
                        CaptureDialog(dialog,output,"saved-sources-"+width+"-"+text+".png");SimpleControl<Button>(dialog,"discCancel").PerformClick();
                    }))NextItem(form,"nextAddBrawlDisc").PerformClick();
                }
                Call(form,"SetWorkspaceTextSize",100);
                Check(NextLibraryFiles(library)==index,"Recalling or cancelling saved source choices never rewrites locations or native catalogue entries");
                var indexed=library.Read().GameLocations.Single(l=>l.Indexed);
                using(FlowModal("discFilePath",dialog=>{
                    var choices=SimpleControl<ComboBox>(dialog,"gameSourceSaved");choices.SelectedItem=choices.Items.Cast<object>().Single(o=>o.ToString().Contains(indexed.Path));
                    Check(SimpleControl<Button>(dialog,"discContinue").Text=="Open saved library","An indexed Brawl disc is recalled as an existing library rather than a request to extract it again");
                    SimpleControl<Button>(dialog,"discContinue").PerformClick();
                }))NextItem(form,"nextAddBrawlDisc").PerformClick();
                await GameLibraryWait(()=>!(bool)Field(form,"_libraryReplacingTree")&&!((Timer)Field(form,"_librarySearchTimer")).Enabled,"Saved library did not open");
                Check(((Control)Field(form,"_libraryPane")).Visible&&SimpleControl<TextBox>(form,"libraryGameFilter").Text.Contains(indexed.Game)&&NextLibraryFiles(library)==index,"Opening the saved Brawl source shows its library/build without re-indexing or changing metadata");
                using(FlowModal("discFilePath",dialog=>{
                    SimpleControl<ComboBox>(dialog,"gameSourceSaved").SelectedIndex=0;SimpleControl<ComboBox>(dialog,"gameSourceGame").Text="Brawl";SimpleControl<ComboBox>(dialog,"gameSourceKind").SelectedIndex=0;SimpleControl<TextBox>(dialog,"discFilePath").Text=rvz;SimpleControl<TextBox>(dialog,"discBuild").Text="Unsupported format fixture";
                    Check(SimpleControl<Button>(dialog,"discContinue").Text=="Save source"&&SimpleControl<Label>(dialog,"discValidation").Text.Contains("format cannot be read"),"An unsupported Brawl image format may be remembered without falsely enabling its reader");
                    SimpleControl<Button>(dialog,"discContinue").PerformClick();
                }))NextItem(form,"nextAddBrawlDisc").PerformClick();
                Check(library.Read().GameLocations.Any(l=>l.Path==rvz&&!l.Indexed),"Unsupported Brawl format remains a path-only location");
                index=NextLibraryFiles(library);
                using(FlowModal("discFilePath",dialog=>{
                    SimpleControl<ComboBox>(dialog,"gameSourceGame").Text=future;SimpleControl<ComboBox>(dialog,"gameSourceKind").SelectedIndex=0;SimpleControl<TextBox>(dialog,"discFilePath").Text=Path.Combine(fixtures,"missing.iso");
                    SimpleControl<Button>(dialog,"discContinue").PerformClick();Check(dialog.Visible&&dialog.DialogResult!=DialogResult.OK,"Missing saved-source input rejects inline without a partial registration");SimpleControl<Button>(dialog,"discCancel").PerformClick();
                }))NextItem(form,"nextAddBrawlDisc").PerformClick();
                bool protectedFile=false,protectedFolder=false;try{library.ProtectOutputPath(image);}catch(InvalidOperationException){protectedFile=true;}try{library.ProtectOutputPath(Path.Combine(folder,"animation.chr0"));}catch(InvalidOperationException){protectedFolder=true;}
                Check(protectedFile&&protectedFolder,"Remembered discs and install folders retain original-output protections");
                Check(NextLibraryFiles(library)==index&&library.ClipCount==clips&&AnimationLibrary.DiagnosticOpens==opens&&BrawlRetargeter.DiagnosticBakes==bakes&&BrawlDiscImage.DiagnosticOpens==discs,"Source configuration, recall and saved-library opening decode no native assets, read no discs, bake no motion and change no animation inventory");
                Check(project.DocumentIdentity()==document&&(bool)Field(form,"_fighterDirty")==dirty,"Saving and recalling game sources creates no target document edits or competing project state");
                await CaptureSimpleWorkspace(form,Path.Combine(output,"saved-game-library.png"));
                await GameInstancesAndManagement(form,library,args,output);
            }
            finally{SimpleDisposeForm(form);}
        }
    }
}
