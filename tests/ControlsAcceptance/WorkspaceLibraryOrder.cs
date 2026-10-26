using BrawlCrate;
using BrawlCrate.Studio;
using BrawlCrate.UI;
using BrawlLib.Internal.Windows.Controls.ModelViewer.MainWindowBase;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class Program
{
    static void NativeOrderMetadata(AnimationLibrary original,string output)
    {
        var data=original.Read();var template=data.Clips.First(c=>c.Channel=="CHR0 skeletal"&&c.ModelId!=null);
        var model=data.Models.Single(m=>m.Id==template.ModelId);
        var first=data.Mounts.Single(m=>m.Id==template.MountId);
        var second=data.Mounts.First(m=>m.Id!=first.Id&&m.Game==first.Game&&m.GameVersion==first.GameVersion);
        data.Models=new List<LibraryModel>{model};data.Clips.Clear();
        // Explicit independent DFS expectation, deliberately unrelated to labels,
        // XML clip order or lexicographic resource-key order. No fake row is decoded.
        string[] keys={"$/2","$/2/0","$/2/10","$/10","$/0","$/1"};
        string[] names={"Zulu","Same","Same","Alpha","Aardvark","Zero"};
        var expected=new List<LibraryAnimation>();
        for(int i=0;i<keys.Length;i++)expected.Add(new LibraryAnimation{Id=(i+1).ToString("x32"),MountId=i<4?first.Id:second.Id,
            ModelId=model.Id,Name=names[i],SourceCharacter=model.Character,ResourceKey=keys[i],ResourcePath="Procedural order witness "+i,
            ResourceSha256=template.ResourceSha256,Channel=template.Channel,NativeVersion=template.NativeVersion,FrameCount=template.FrameCount,
            AssociationEvidence="Metadata-only ordering fixture; not a playable association"});
        data.Mounts=data.Mounts.Where(m=>m.Id==model.MountId&&m.Id!=first.Id&&m.Id!=second.Id).Concat(new[]{first,second}).ToList();
        data.Clips=expected.AsEnumerable().Reverse().ToList();
        string folder=Path.Combine(output,"procedural-order");var library=new AnimationLibrary(folder);
        Directory.CreateDirectory(folder);GameLibraryPublishMetadata(library,data);
        int opens=AnimationLibrary.DiagnosticOpens,reads=BrawlDiscImage.DiagnosticNativeReads;
        Check(library.Browse("").Select(i=>i.Clip.Id).SequenceEqual(expected.Select(c=>c.Id)),"Native numeric child order keeps 2 before 10, nested descendants before siblings and each PAC together");
        var page=library.Search("",1,out int total,3);
        Check(total==6&&page.Select(i=>i.Clip.Id).SequenceEqual(expected.Skip(1).Take(3).Select(c=>c.Id)),"Native ordering precedes query counts and paging");
        library.SetFavorite(expected[1].Id,true);library.SetFavorite(expected[4].Id,true);
        Check(library.Browse(new LibraryQuery{FavoritesOnly=true}).Select(i=>i.Clip.Id).SequenceEqual(new[]{expected[1].Id,expected[4].Id}),"Favorite filtering retains native relative order across PACs");
        Check(library.Browse("Same").Select(i=>i.Clip.Id).SequenceEqual(new[]{expected[1].Id,expected[2].Id}),"Duplicate clip names retain exact independent native identities and order");
        foreach(var row in data.Clips)row.Name="Renamed "+(7-int.Parse(row.Id,System.Globalization.NumberStyles.HexNumber));
        data.Clips=data.Clips.Skip(2).Concat(data.Clips.Take(2)).ToList();GameLibraryPublishMetadata(library,data);
        Check(library.Browse("").Select(i=>i.Clip.Id).SequenceEqual(expected.Select(c=>c.Id)),"Renaming and permuting serialized clip rows cannot change native traversal order");
        Check(opens==AnimationLibrary.DiagnosticOpens&&reads==BrawlDiscImage.DiagnosticNativeReads,"Ordering/filtering procedural metadata decodes no source and reads no disc packages");
    }
    static void NativeOrderOracle(ModelForm oracle,AnimationLibrary library,LibrarySourceLease source,string label)
    {
        var keys=BrawlLibrarySource.Resources(source.MotionRoot).ToDictionary(p=>p.Item1,p=>p.Item2);
        var rows=library.Browse("").Where(i=>i.Clip.MountId==source.Reference.MotionMountId).ToArray();
        foreach(var type in new[]{NW4RAnimType.CHR,NW4RAnimType.SRT,NW4RAnimType.PAT,NW4RAnimType.VIS,NW4RAnimType.CLR,NW4RAnimType.SHP,NW4RAnimType.SCN})
        {
            var panel=oracle.FighterEditor.leftPanel;panel.listAnims.Items.Clear();
            panel.LoadAnims(source.MotionRoot,type,null,false,false,new ListViewGroup("Native motion PAC"));
            var native=panel.listAnims.Items.Cast<ListViewItem>().Select(i=>(NW4RAnimationNode)i.Tag).ToArray();
            string channel=ModelEditorBase.AnimTypeList[(int)type]==typeof(CHR0Node)?"CHR0 skeletal":ModelEditorBase.AnimTypeList[(int)type].Name;
            Check(rows.Where(i=>i.Clip.Channel==channel).Select(i=>i.Clip.ResourceKey).SequenceEqual(native.Select(n=>keys[n])),label+" "+type+" matches the actual native BrawlCrate animation list exactly ("+native.Length+")");
        }
    }
    static async Task OrderTree(ModelForm form,AnimationLibrary library,string character,string channel,string output)
    {
        var query=new LibraryQuery{Character=character,Channel=channel};var expected=library.Browse(query);
        SimpleControl<TextBox>(form,"librarySearch").Text=character;
        ((Timer)Field(form,"_librarySearchTimer")).Stop();Call(form,"RefreshLibrary",(object)null);await LibraryQuerySettled(form);
        var tree=SimpleControl<TreeView>(form,"libraryClips");
        foreach(var branch in AllNodes(tree.Nodes).Where(n=>n.Level==1).ToArray())branch.Expand();
        await LibraryQuerySettled(form);
        TreeNode more;
        while((more=AllNodes(tree.Nodes).FirstOrDefault(n=>n.Name=="libraryMore"&&n.Parent.Text==character))!=null)
        {Call(form,"PopulateLibraryGroup",more.Parent,true);await LibraryQuerySettled(form);}
        var actual=AllNodes(tree.Nodes).Select(n=>n.Tag as LibrarySearchItem).Where(i=>i?.Clip.Channel==channel&&i.Clip.SourceCharacter==character).Select(i=>i.Clip.Id).ToArray();
        Check(actual.SequenceEqual(expected.Select(i=>i.Clip.Id)),"Lazy disc character tree preserves complete native "+channel+" order for "+character);
        await CaptureSimpleWorkspace(form,Path.Combine(output,"native-order-"+character+".png"));
    }
    static async Task WorkspaceLibraryOrder(string[] args,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Native library order needs a private desktop.");
        string folder=Path.Combine(output,"library");Directory.CreateDirectory(folder);
        File.Copy(args[Array.IndexOf(args,"--library-index")+1],Path.Combine(folder,"catalogue.xml"));
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT",folder);
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_TEMP_ROOT",Path.Combine(output,"transient"));
        var library=new AnimationLibrary(folder);NativeOrderMetadata(library,output);
        using(var startup=MainForm.StartViewerApplication())
        {
            var form=(ModelForm)Field(MainForm.Instance,"_fighterForm");var oracle=new ModelForm();
            try
            {
                form.ClientSize=new Size(1700,950);await SimpleControlsReady(form);
                Call(form,"ImportFighterModel",args[0]);Call(form,"ImportFighterAnimations",new[]{new[]{args[1]}});await SimpleControlsReady(form);
                Call(form,"UseSharedSourceLibrary");await LibraryQuerySettled(form);form.FighterEditor.SetFrame(3);
                var witness=new GameLibraryDocument(form);string index=FighterProject.HashFile(library.IndexPath);
                foreach(string character in new[]{"mario","sonic"})
                {
                    var selected=library.Browse(new LibraryQuery{Character=character,Channel="CHR0 skeletal",Text="Wait1"}).Single(i=>i.Clip.Name=="Wait1"&&i.Model?.Name==(character=="mario"?"FitMario00":"FitSonic00"));
                    using(var source=library.OpenPreview(selected.Clip.Id))NativeOrderOracle(oracle,library,source,"Disc "+character);
                    await OrderTree(form,library,character,"CHR0 skeletal",output);
                    await Flow39Select(form,selected);witness.CheckPreserved("native-order "+character+" selection");
                }
                Check(FighterProject.HashFile(library.IndexPath)==index,"Native browsing leaves the old disc catalogue byte-identical and requires no reindex");
                var model=BrawlLibrarySource.InspectModels(args[0]).First(m=>!m.Shadow&&m.HasGeometry);
                typeof(ModelForm).GetMethod("LoadSessionSource",Private,null,new[]{typeof(LibraryRegistration)},null).Invoke(form,new object[]{new LibraryRegistration{ModelPath=args[0],MotionPath=args[1],ModelKey=model.ResourceKey,Character="mario",ApproveAssociation=true}});await LibraryQuerySettled(form);
                var temporary=(AnimationLibrary)Field(form,"_library");var clip=temporary.Browse("Wait1").Single(i=>i.Clip.Name=="Wait1"&&i.Clip.ModelId!=null);
                using(var source=temporary.OpenPreview(clip.Clip.Id))NativeOrderOracle(oracle,temporary,source,"Temporary source");
                await Flow39Select(form,clip);witness.CheckPreserved("temporary source native order");
                await CaptureSimpleWorkspace(form,Path.Combine(output,"native-order-temporary.png"));
                Check(FighterProject.HashFile(library.IndexPath)==index,"Temporary source order does not register or rewrite the shared library");
            }
            finally{SimpleDisposeForm(oracle);if(!form.IsDisposed)SimpleDisposeForm(form);}
        }
    }
}
