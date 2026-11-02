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
        var first=inventory.Fighters[0];
        Check(first.Costumes.Length>0&&first.Costumes[0].AfsIndex==1423&&first.MotionMembers.Length==2&&
            first.MotionMembers[0].Length>0&&first.MotionMembers[1].Length>0,
            "Fighter model/costume packs and both motion-bank members resolve directly from ISO without extraction");
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
            dialog.Controls.Find("bt3CharacterSearch",true).OfType<TextBox>().Single().Text="Goku_0";
            Check(dialog.Controls.Find("bt3Characters",true).OfType<ListBox>().Single().Items.Count==1,
                "Search filters the disc's advisory character labels without changing numeric identities");
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
