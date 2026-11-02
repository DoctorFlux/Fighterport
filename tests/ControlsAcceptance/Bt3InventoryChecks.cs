using BrawlCrate.Studio;
using BrawlCrate.UI;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class Program
{
    static async Task Bt3InventoryChecks(string[] args,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("BT3 acceptance requires the private desktop.");
        var image=args[0];var inventory=Bt3DiscInventory.Inspect(image,CancellationToken.None);
        Check(inventory.Volume=="SLUS_21678"&&inventory.Afs0Files==1&&inventory.Afs1Files==3399&&inventory.Afs2Files==65201,
            "Original USA disc identifies its three bounded native AFS partitions");
        Check(inventory.Fighters.Length==161&&inventory.Fighters[0].Slot==0&&inventory.Fighters[1].Slot==1,
            "Every fighter slot retains its exact numeric disc identity");
        Check(Bt3CharacterNames.Count==161&&Bt3CharacterNames.Get(1)=="Goku (Med)"&&
            Bt3CharacterNames.Get(2)=="Goku (Med) Super Saiyan",
            "Readable form labels preserve exact distinct native slots");
        var first=inventory.Fighters[0];
        Check(first.Costumes.Length>0&&first.Costumes[0].AfsIndex==1423&&first.MotionMembers.Length==2&&
            first.MotionMembers[0].Length>0&&first.MotionMembers[1].Length>0,
            "Fighter model/costume packs and both motion-bank members resolve directly from ISO without extraction");
        var mid=inventory.Fighters[1];
        var pmdl=inventory.ModelMember(mid,mid.Costumes[0]);
        var skeleton=Bt3NativeAssetReader.ReadSkeleton(inventory.ReadOwned(pmdl,4*1024*1024));
        var idleBytes=inventory.ReadOwned(mid.MotionMembers[0][1],4*1024*1024);
        var idle=Bt3NativeAssetReader.ReadMotion(idleBytes,0);
        Check(mid.DiscLabel=="Goku_1"&&skeleton.Bones.Length>30&&idle.Length>0&&idle.Tracks.Length>0&&
            idle.Tracks.All(track=>skeleton.Bones.Any(bone=>bone.Id==track.Bone)),
            "Goku (Mid) slot 001 has a validated native pmdl skeleton and decoded action-0 idle tracks");
        var restBone=skeleton.Bones.First(b=>idle.Tracks.All(t=>t.Bone!=b.Id));
        var atZero=Bt3NativeAssetReader.Sample(skeleton,idle,0);
        int restIndex=Array.IndexOf(skeleton.Bones,restBone);
        Check(atZero.Translation[restIndex]==restBone.Rest&&atZero.Rotation[restIndex]==System.Numerics.Quaternion.Identity,
            "Native pose sampling keeps unauthored bones at their exact model rest transforms");
        var varying=idle.Tracks.First(t=>t.Keys.Length>=2&&t.Keys[1].Frame>t.Keys[0].Frame);
        float midway=(varying.Keys[0].Frame+varying.Keys[1].Frame)*0.5f;
        var between=Bt3NativeAssetReader.Sample(skeleton,idle,midway);
        int varyingIndex=Array.FindIndex(skeleton.Bones,b=>b.Id==varying.Bone);
        var expected=System.Numerics.Quaternion.Slerp(varying.Keys[0].Rotation,varying.Keys[1].Rotation,0.5f);
        Check(Math.Abs(System.Numerics.Quaternion.Dot(expected,between.Rotation[varyingIndex]))>0.99999f&&
            (varying.RotationOnly||System.Numerics.Vector3.Distance(
                System.Numerics.Vector3.Lerp(varying.Keys[0].Translation,varying.Keys[1].Translation,0.5f),
                between.Translation[varyingIndex])<0.000001f),
            "Native fractional pose sample uses authored-key interpolation, not dense baked frames");
        Console.WriteLine("TRACE BT3 Goku Mid skeleton="+skeleton.Bones.Length+" idle last frame="+idle.Length+
            " tracks="+idle.Tracks.Length+" keys="+idle.Tracks.Sum(t=>t.Keys.Length)+
            " pmdl sha256="+skeleton.SourceSha256+" idle packed sha256="+idle.SourceSha256);
        if(args.Contains("--bt3-sweep"))
        {
            var failures=new System.Collections.Generic.List<string>();int inspected=0,costumesChecked=0,actionsChecked=0;
            foreach(var fighter in inventory.Fighters.Where(f=>f.Costumes.Length>0&&f.MotionMembers[0].Length>1))
            {
                try
                {
                    var clip=Bt3NativeAssetReader.ReadMotion(inventory.ReadOwned(fighter.MotionMembers[0][1],4*1024*1024),0);
                    foreach(var costume in fighter.Costumes)
                    {
                        var model=inventory.ModelMember(fighter,costume);
                        var rig=Bt3NativeAssetReader.ReadSkeleton(inventory.ReadOwned(model,4*1024*1024));
                        if(!clip.Tracks.All(t=>rig.Bones.Any(b=>b.Id==t.Bone)))
                            throw new InvalidDataException("Idle track references a missing node in costume "+costume.Identity);
                        costumesChecked++;
                    }
                    foreach(int action in new[]{1,10,50,100,200,300,400})
                    {
                        int child=action+1;
                        if(child>=fighter.MotionMembers[0].Length||fighter.MotionMembers[0][child].Bytes<10)continue;
                        try
                        {
                            var sample=Bt3NativeAssetReader.ReadMotion(inventory.ReadOwned(fighter.MotionMembers[0][child],4*1024*1024),action);
                            if(sample.Tracks.Length>0)actionsChecked++;
                        }
                        catch(InvalidDataException error)
                        { Console.WriteLine("OPEN BT3 action "+fighter.Slot.ToString("D3")+"/"+action+": "+error.Message); }
                    }
                    inspected++;
                }
                catch(Exception error)when(error is InvalidDataException||error is IOException)
                {failures.Add(fighter.Display+": "+error.Message);}
            }
            foreach(var failure in failures.Take(20))Console.WriteLine("OPEN BT3 native reader: "+failure);
            Console.WriteLine("TRACE BT3 native sweep slots="+inspected+" costumes="+costumesChecked+" sample actions="+actionsChecked+" failures="+failures.Count);
            Check(inspected==161&&costumesChecked>=161&&failures.Count==0,
                "Every available costume and its own slot's native action-0 motion decode against the costume skeleton");
        }
        var native=first.Costumes[0];string destination=Path.Combine(output,"native-costume-pack.bin");
        string digest=inventory.ExportOriginal(native,destination);
        Check(new FileInfo(destination).Length==native.Bytes&&FighterProject.HashFile(destination)==digest,
            "Original native pack exports byte-exactly without touching the disc");
        bool rejected=false;try{inventory.ExportOriginal(native,destination);}catch(IOException){rejected=true;}
        Check(rejected&&FighterProject.HashFile(destination)==digest,"Existing destinations are preserved");
        string invalid=Path.Combine(output,"not-bt3.iso");File.WriteAllBytes(invalid,new byte[32768+2048]);
        rejected=false;try{Bt3DiscInventory.Inspect(invalid);}catch(InvalidDataException){rejected=true;}
        Check(rejected,"Malformed or unrelated ISO images never become BT3 resources");
        using(var dialog=new Bt3DiscInventoryDialog(inventory))
        {
            dialog.Show();Application.DoEvents();
            Check(dialog.Controls.Find("bt3Characters",true).OfType<ListBox>().Single().Items.Count==161&&
                dialog.Controls.Find("bt3Costumes",true).OfType<ListBox>().Single().Items.Count==8&&
                dialog.Controls.Find("bt3MotionBanks",true).OfType<ListBox>().Single().Items.Count==446,
                "Compact inventory displays all character slots, the selected costume packs and exact raw bank members");
            var searchBox=dialog.Controls.Find("bt3CharacterSearch",true).OfType<TextBox>().Single();
            searchBox.Text="Goku (Med)";
            var formList=dialog.Controls.Find("bt3Characters",true).OfType<ListBox>().Single();
            Check(formList.Items.Count==2&&((Bt3DiscInventory.Bt3FighterSlot)formList.Items[0]).Slot==1&&
                ((Bt3DiscInventory.Bt3FighterSlot)formList.Items[1]).Slot==2,
                "Goku Mid base and Super Saiyan are separate searchable forms");
            var costumeList=dialog.Controls.Find("bt3Costumes",true).OfType<ListBox>().Single();
            var actionList=dialog.Controls.Find("bt3MotionBanks",true).OfType<ListBox>().Single();
            var inspect=dialog.Controls.Find("bt3InspectMotion",true).OfType<Button>().Single();
            Check(costumeList.Items.Count==8&&actionList.SelectedIndex==1&&inspect.Enabled,
                "A form's costume and exact native action remain independently selected");
            inspect.PerformClick();
            var summary=dialog.Controls.Find("bt3NativeStatus",true).OfType<Label>().Single();
            Check(summary.Text.Contains("69 bones")&&summary.Text.Contains("last native frame 108"),
                "Goku Mid default costume and idle inspect verified native pose data");
            formList.SelectedIndex=1;costumeList.SelectedIndex=Math.Min(1,costumeList.Items.Count-1);
            inspect.PerformClick();
            Check(summary.Text.Contains("Goku (Med) Super Saiyan")&&summary.Text.Contains("Pose data verified"),
                "Transformation chooses its own motion bank while alternate costume chooses its own model");
            await CaptureWindow(dialog,Path.Combine(output,"bt3-form-costume-action.png"));
            searchBox.Text="Goku_0";
            Check(dialog.Controls.Find("bt3Characters",true).OfType<ListBox>().Single().Items.Count==1,
                "Search filters the disc's advisory character labels without changing numeric identities");
            Check(summary.Text.Contains("Selection changed"),"Changing form clears stale pose verification");
            await CaptureWindow(dialog,Path.Combine(output,"bt3-original-inventory.png"));dialog.Close();
        }
        using(var source=new GameSourceDialog(new LibraryGameLocation[0],new AnimationLibrary(Path.Combine(output,"isolated-library"))))
        {
            source.Show();Application.DoEvents();
            source.Controls.Find("gameSourceGame",true).OfType<ComboBox>().Single().Text=Bt3DiscInventory.Game;
            source.Controls.Find("discFilePath",true).OfType<TextBox>().Single().Text=image;
            Check(source.Controls.Find("bt3InspectDisc",true).OfType<Button>().Single().Enabled&&
                source.Controls.Find("discContinue",true).OfType<Button>().Single().Text=="Save source",
                "BT3 appears in Add game source with inspection available and no false indexed-library claim");
            CaptureDialog(source,output,"bt3-add-source.png");
            source.Controls.Find("discContinue",true).OfType<Button>().Single().PerformClick();
            Check(source.DialogResult==DialogResult.OK&&source.PathOnly&&source.Request.Game==Bt3DiscInventory.Game,
                "Verified BT3 ISO saves as a source location without claiming decoded models or animations");
        }
        Console.WriteLine("TRACE BT3 slots="+inventory.Fighters.Length+" first="+first.Display+
            " costumes="+first.Costumes.Length+" bank members="+first.MotionMembers[0].Length+"/"+first.MotionMembers[1].Length+
            " first pack bytes="+native.Bytes+" sha256="+digest);
    }
}
