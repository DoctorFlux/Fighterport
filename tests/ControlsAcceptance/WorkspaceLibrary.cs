using BrawlCrate.Studio;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

internal static partial class Program
{
    static void FavoriteStorageCases(string output)
    {
        string folder=Path.Combine(output,"preferences-cases"),id=Guid.NewGuid().ToString("N"),other=Guid.NewGuid().ToString("N");
        var favorites=new LibraryFavorites(folder);Check(favorites.Ids.Length==0&&!Directory.Exists(folder),"Absent preferences have no read-side writes");
        favorites.Set(id,true);favorites.Set(id,true);favorites.Set(other,true);
        Check(new LibraryFavorites(folder).Ids.OrderBy(x=>x).SequenceEqual(new[]{id,other}.OrderBy(x=>x)),"Exact favorite identities deduplicate and persist across fresh stores");
        string file=Path.Combine(folder,"preferences.json"),bytes=File.ReadAllText(file);
        using(var held=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read))
        {favorites.Set(id,false);Check(favorites.LastError!=null&&!favorites.Contains(id),"Unavailable preferences retain session state and a recovery explanation");}
        Check(File.ReadAllText(file)==bytes&&!Directory.GetFiles(folder,"*.tmp").Any(),"Failed atomic replacement preserves prior preferences and cleans its candidate");
        favorites.Set(id,false);Check(favorites.LastError==null&&!new LibraryFavorites(folder).Contains(id),"A later update recovers durable preferences");
        File.WriteAllText(file,"{ broken preferences");var corrupt=new LibraryFavorites(folder);
        Check(corrupt.Ids.Length==0&&corrupt.LastError!=null&&File.ReadAllText(file)=="{ broken preferences","Corrupt preferences degrade safely without rewriting them on read");
        File.WriteAllText(file,new string('x',2097153));Check(new LibraryFavorites(folder).LastError!=null,"Preferences reads are bounded at two MiB before parsing");
        File.WriteAllText(file,"{\"Schema\":2,\"Favorites\":[]}");Check(new LibraryFavorites(folder).LastError!=null,"Unsupported preferences versions do not silently rebind favorites");
        File.WriteAllText(file,"{\"Schema\":1,\"Favorites\":[null,\"same name\",\""+id+"\"]}");
        Check(new LibraryFavorites(folder).Ids.SequenceEqual(new[]{id}),"Malformed entries cannot become name-based favorite identities");
        Check(new LibraryFavorites(Path.Combine(output,"another-library")).Ids.Length==0,"Favorite preferences are isolated by library location");
    }

    static void LargeLibraryQueryCases(AnimationLibrary source,string selectedId,string output)
    {
        string folder=Path.Combine(output,"procedural-catalogue");Directory.CreateDirectory(folder);
        var xml=XDocument.Load(source.IndexPath);var clips=xml.Root.Element("Clips");
        var template=new XElement(clips.Elements().Single(e=>e.Element("Id").Value==selectedId));clips.RemoveNodes();
        for(int i=0;i<10000;i++)
        {
            var copy=new XElement(template);copy.Element("Id").Value=(i+1).ToString("x32");
            copy.Element("Name").Value="Duplicate name";copy.Element("ResourceKey").Value="$/procedural/"+i;
            copy.Element("Channel").Value=i%2==0?"CHR0 skeletal":"VIS0 visibility";clips.Add(copy);
        }
        string file=Path.Combine(folder,"catalogue.xml");xml.Save(file);var library=new AnimationLibrary(folder);
        string hash=FighterProject.HashFile(file);long generation=library.Read().Generation;
        int opens=AnimationLibrary.DiagnosticOpens,bakes=BrawlCrate.Studio.Retargeting.BrawlRetargeter.DiagnosticBakes;
        var page=library.Search(null,0,out int total,7);
        Check(total==10000&&page.Count==7&&library.Browse((string)null).Count==10000,"Legacy null/string search APIs retain bounded paging over ten thousand procedural rows");
        foreach(int i in new[]{2,4,7})library.SetFavorite(i.ToString("x32"),true);
        var favorite=library.Search(new LibraryQuery{FavoritesOnly=true},1,out int favoriteTotal,1);
        Check(favoriteTotal==3&&favorite.Count==1,"Favorites filter precedes result counts and paging even with duplicate names");
        var query=new LibraryQuery{Text="Duplicate",Game="Brawl",Build=source.Browse("").First().Motion.GameVersion,ModelId=template.Element("ModelId").Value,Channel="CHR0 skeletal",Availability=LibrarySourceState.Available,FavoritesOnly=true};
        var combined=library.Browse(query);
        Check(combined.Count==1&&combined.Single().Clip.Id==7.ToString("x32"),"Independent combined identity/channel/availability/favorite expectation selects only procedural row six");
        var unsupported=library.Search(new LibraryQuery{Availability=LibrarySourceState.UnsupportedChannelVersion,Channel="VIS0 visibility"},4997,out int unsupportedTotal,10);
        Check(unsupportedTotal==5000&&unsupported.Count==3,"Availability/channel filters run before final-page counts and paging");
        Check(library.Browse(new LibraryQuery{GameBuildText="No such build"}).Count==0&&library.Browse(new LibraryQuery{CharacterModelText="No such model"}).Count==0,"Game/build and character/model fields combine without fallback results");
        Check(FighterProject.HashFile(file)==hash&&library.Read().Generation==generation,"Favorites and typed queries leave catalogue schema/content/generation unchanged");
        bool bounded=false;try{library.Browse(new LibraryQuery{Text=new string('x',513)});}catch(ArgumentException){bounded=true;}
        Check(bounded,"Typed queries retain the existing text bound");
        Check(AnimationLibrary.DiagnosticOpens==opens&&BrawlCrate.Studio.Retargeting.BrawlRetargeter.DiagnosticBakes==bakes,"Large metadata filtering opens no native source and performs no retarget bake");
    }

    static async Task LibraryQuerySettled(ModelForm form) => await Wait(()=>!(bool)Field(form,"_libraryReplacingTree")&&!((Timer)Field(form,"_librarySearchTimer")).Enabled&&!((Timer)Field(form,"_libraryPopulateTimer")).Enabled);

    static async Task WorkspaceLibrary(string[] args,string output)
    {
        if(!PrivateDesktop||!args.Contains("--retarget-source"))throw new InvalidOperationException("Private desktop and authentic source required.");
        FavoriteStorageCases(output);
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT",Path.Combine(output,"library"));
        string donor=args[Array.IndexOf(args,"--retarget-source")+1],motion=Path.Combine(output,"original-motion.pac");File.Copy(args[1],motion);
        using(var startup=MainForm.StartViewerApplication())
        {
            var form=(ModelForm)Field(MainForm.Instance,"_fighterForm");
            try
            {
                form.ClientSize=new Size(1700,950);await Task.Delay(150);await SimpleControlsReady(form);
                Call(form,"ImportFighterModel",args[0]);await SimpleControlsReady(form);
                SimpleToolbarItem(form,"workspaceSourceLibrary").PerformClick();
                var library=(AnimationLibrary)Field(form,"_library");var model=BrawlLibrarySource.InspectModels(donor).First(m=>!m.Shadow);
                library.Register(BrawlLibrarySource.Prepare(new LibraryRegistration{ModelPath=donor,ModelKey=model.ResourceKey,MotionPath=motion,Character=model.Name,GameVersion="Local fixture build",ApproveAssociation=true}));
                var selected=await FlowSelectSource(form,library,"Wait1");
                LargeLibraryQueryCases(library,selected.Clip.Id,output);
                string catalogue=FighterProject.HashFile(library.IndexPath),document=form.FighterProject.DocumentIdentity();
                var favorite=SimpleControl<CheckBox>(form,"libraryFavorite");favorite.Checked=true;await LibraryQuerySettled(form);
                Check(new AnimationLibrary(library.DirectoryPath).IsFavorite(selected.Clip.Id)&&FighterProject.HashFile(library.IndexPath)==catalogue&&form.FighterProject.DocumentIdentity()==document,"Actual favorite toggle persists exact ID without changing catalogue or donor-free target document");
                string replacement=Path.Combine(output,"repaired-motion.pac");File.Move(motion,replacement);
                library.Repair(selected.Motion.Id,replacement);
                Check(library.IsFavorite(selected.Clip.Id)&&new AnimationLibrary(library.DirectoryPath).IsFavorite(selected.Clip.Id),"Exact source repair retains the favorite clip identity");
                await ComparisonRevision(form,library,selected.Clip.Id,null);
                var editor=form.FighterEditor;editor.SetFrame(3);editor.FrameFighterModel();
                var setup=await Await<ControlReply>(Call(form,"LoadControls"));editor.SetFrame(3);
                editor.SelectedBone=((MDL0Node)editor.TargetModel)._linker.BoneCache.Single(b=>b.Name==setup.BoneNames[setup.Roles.Single(r=>r.Role=="Hips").Bone.Value]);
                string originalContent=LifecycleContent(editor.SelectedCHR0);
                var edit=editor.CHR0Editor._transBoxes[3];edit.Text=(editor.SelectedBone.FrameState._rotate._x+2).ToString();Call(edit,"Apply");await SimpleControlsReady(form);
                string content=LifecycleContent(editor.SelectedCHR0);int undo=editor._undoSaves.Count;
                Check(content!=originalContent&&undo>0,"Library navigation starts with a real native body edit and nonempty Undo");
                Call(form,"OpenLinkedComparison");var comparison=Field(form,"_comparisonLease");Call(form,"ToggleAnimationLibrary");
                foreach(int text in new[]{100,200})foreach(int width in new[]{1050,1700})
                {
                    form.ClientSize=new Size(width,950);Call(form,"SetWorkspaceTextSize",text);Call(form,"ClearLibraryFilters");await LibraryQuerySettled(form);
                    var filters=SimpleControl<Control>(form,"libraryFilters");
                    var toggle=SimpleControl<Button>(form,"libraryToggleFilters");
                    if(filters.Visible)toggle.PerformClick();
                    Check(!filters.Visible&&SimpleControl<CheckBox>(form,"libraryFavoritesOnly").Visible&&SimpleControl<Button>(form,"libraryClearFilters").Visible,
                        "Advanced filters collapse while the common query controls stay available at "+width+"/"+text);
                    toggle.PerformClick();
                    SimpleControl<TextBox>(form,"librarySearch").Text="Wait1";
                    SimpleControl<TextBox>(form,"libraryGameFilter").Text="Brawl Local fixture";
                    SimpleControl<TextBox>(form,"libraryCharacterFilter").Text=model.Name;
                    SimpleControl<ComboBox>(form,"libraryAvailabilityFilter").SelectedIndex=1;
                    SimpleControl<ComboBox>(form,"libraryChannelFilter").SelectedItem="CHR0 skeletal";
                    SimpleControl<CheckBox>(form,"libraryFavoritesOnly").Checked=true;
                    await LibraryQuerySettled(form);
                    var tree=SimpleControl<TreeView>(form,"libraryClips");var rows=AllNodes(tree.Nodes).Select(n=>n.Tag as LibrarySearchItem).Where(i=>i!=null).ToArray();
                    Check(rows.Length==1&&rows[0].Clip.Id==selected.Clip.Id&&SimpleControl<Label>(form,"libraryPage").Text.StartsWith("1 matches"),"Combined UI filters precede outline and count at "+width+"/"+text);
                    Check(toggle.Text.Contains("4 active"),"Advanced filter disclosure reports all four active criteria at "+width+"/"+text);
                    int expandedTreeHeight=tree.Height;
                    await CaptureSimpleWorkspace(form,Path.Combine(output,"library-filters-"+width+"-"+text+".png"));
                    toggle.PerformClick();
                    var collapsedRows=AllNodes(tree.Nodes).Select(n=>n.Tag as LibrarySearchItem).Where(i=>i!=null).ToArray();
                    Check(!filters.Visible&&toggle.Text.Contains("4 active")&&collapsedRows.Length==1&&collapsedRows[0].Clip.Id==selected.Clip.Id&&
                        SimpleControl<TextBox>(form,"libraryGameFilter").Text=="Brawl Local fixture"&&tree.Height>=expandedTreeHeight,
                        "Hiding advanced filters retains exact criteria/results and restores tree space at "+width+"/"+text);
                    await CaptureSimpleWorkspace(form,Path.Combine(output,"library-collapsed-"+width+"-"+text+".png"));
                    Check(ReferenceEquals(comparison,Field(form,"_comparisonLease"))&&LifecycleContent(editor.SelectedCHR0)==content&&editor.CurrentFrame==3&&editor._undoSaves.Count==undo,"Filtering preserves exact target/frame/Undo and retained comparison at "+width+"/"+text);
                }
                typeof(ModelForm).GetMethod("FocusWorkspaceTarget",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{favorite});
                Check(favorite.Focused&&favorite.Checked&&favorite.Text=="Favorited","Favorite state is explicit and keyboard-focusable at 200% text");
                await CaptureSimpleWorkspace(form,Path.Combine(output,"library-favorite-200.png"));
                Call(form,"ClearLibraryFilters");await LibraryQuerySettled(form);await FlowSelectSource(form,library,"Wait1");
                SimpleControl<CheckBox>(form,"libraryFavoritesOnly").Checked=true;await LibraryQuerySettled(form);
                SimpleControl<CheckBox>(form,"libraryFavorite").Checked=false;await LibraryQuerySettled(form);
                Check(SimpleControl<TreeView>(form,"libraryClips").SelectedNode==null&&!SimpleControl<Button>(form,"libraryUseOnTarget").Enabled&&!SimpleControl<Button>(form,"libraryPreviewOriginal").Enabled&&!SimpleControl<CheckBox>(form,"libraryFavorite").Enabled,"A favorite hidden by filtering clears actionable selection and transfer commands");
                Check(ReferenceEquals(comparison,Field(form,"_comparisonLease"))&&LifecycleContent(editor.SelectedCHR0)==content,"Hiding the selected source never clears comparison or alters the target");
                string otherFavorite=library.Browse("Wait2").Single(i=>i.Clip.Name=="Wait2"&&i.Model!=null).Clip.Id;library.SetFavorite(otherFavorite,true);
                Call(form,"ClearLibraryFilters");await LibraryQuerySettled(form);
                Check(library.IsFavorite(otherFavorite)&&library.Browse(new LibraryQuery()).Count==library.ClipCount&&SimpleControl<TextBox>(form,"librarySearch").Text==""&&!SimpleControl<CheckBox>(form,"libraryFavoritesOnly").Checked,"Clear filters resets all criteria without clearing stored favorites");
                var search=SimpleControl<TextBox>(form,"librarySearch");search.Text="Wait1";Call(form,"RefreshLibrary",new object[]{null});search.Text="No matching source";search.Text="Wait2";
                await LibraryQuerySettled(form);
                var newest=AllNodes(SimpleControl<TreeView>(form,"libraryClips").Nodes).Where(n=>n.Tag is LibrarySearchItem).ToArray();
                Check(newest.Length>0&&newest.All(n=>((LibrarySearchItem)n.Tag).Clip.Name.Contains("Wait2")),"Stale asynchronous search results cannot overwrite the newest combined query");
                Call(form,"OpenLinkedComparison");Check(ReferenceEquals(comparison,Field(form,"_comparisonLease")),"Comparison resumes the retained recorded source after library organization");
                await CaptureSimpleWorkspace(form,Path.Combine(output,"library-comparison-retained.png"));
                Check(LifecycleContent(editor.SelectedCHR0)==content&&editor._undoSaves.Count==undo,"All library organization actions preserve native authored content and Undo");
                Call(form,"ToggleAnimationLibrary");
                var procedural=new AnimationLibrary(Path.Combine(output,"procedural-catalogue"));
                typeof(ModelForm).GetField("_library",Private).SetValue(form,procedural);
                Call(form,"ClearLibraryFilters");await LibraryQuerySettled(form);
                SimpleControl<TextBox>(form,"librarySearch").Text="Duplicate";await LibraryQuerySettled(form);
                var largeTree=SimpleControl<TreeView>(form,"libraryClips");
                foreach(var root in largeTree.Nodes.Cast<TreeNode>())root.Expand();
                await LibraryQuerySettled(form);
                foreach(var branch in AllNodes(largeTree.Nodes).Where(n=>n.Level==1).ToArray())branch.Expand();
                await LibraryQuerySettled(form);
                foreach(var branch in AllNodes(largeTree.Nodes).Where(n=>n.Level==2).ToArray())branch.Expand();
                await LibraryQuerySettled(form);
                int leaves=AllNodes(largeTree.Nodes).Count(n=>n.Tag is LibrarySearchItem);
                Check(leaves>0&&leaves<=1000&&AllNodes(largeTree.Nodes).Any(n=>n.Name=="libraryMore")&&SimpleControl<Label>(form,"libraryPage").Text.StartsWith("10000 matches"),"Large procedural catalogue keeps incremental first-page leaf population and correct full result counts");
                Check(ReferenceEquals(comparison,Field(form,"_comparisonLease"))&&LifecycleContent(editor.SelectedCHR0)==content,"Large-catalogue browsing retains the original source comparison and target");
                await CaptureSimpleWorkspace(form,Path.Combine(output,"library-large-catalogue.png"));
                typeof(ModelForm).GetField("_library",Private).SetValue(form,library);
                editor.Undo();await SimpleControlsReady(form);Check(LifecycleContent(editor.SelectedCHR0)==originalContent,"Exact native body Undo survives favorites and all filters");
                editor.Redo();await SimpleControlsReady(form);Check(LifecycleContent(editor.SelectedCHR0)==content,"Exact native body Redo survives favorites and all filters");
            }
            finally { if(!form.IsDisposed)SimpleDisposeForm(form); }
        }
    }
}
