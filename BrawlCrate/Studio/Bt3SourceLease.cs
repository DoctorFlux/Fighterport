using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace BrawlCrate.Studio
{
    // Exact, read-only native source selection. Preparing this lease may read
    // the disc and decode geometry; sampling the prepared rig never does.
    internal sealed class Bt3SourceLease
    {
        internal Bt3DiscInventory Inventory { get; }
        internal Bt3DiscInventory.Bt3FighterSlot Form { get; }
        internal Bt3DiscInventory.Bt3Resource Costume { get; }
        internal Bt3DiscInventory.Bt3Resource ActionMember { get; }
        internal int Action { get; }
        internal string CostumeSha256 { get; }
        internal string ModelSha256 { get; }
        internal string MotionSha256 { get; }
        internal Bt3SourceRig Source { get; }
        internal string Identity => Inventory.Volume+"/form/"+Form.Slot.ToString("D3")+
            "/"+Costume.Identity+"/action/"+Action.ToString("D3")+"/"+ActionMember.Identity;

        private Bt3SourceLease(Bt3DiscInventory inventory,Bt3DiscInventory.Bt3FighterSlot form,
            Bt3DiscInventory.Bt3Resource costume,Bt3DiscInventory.Bt3Resource actionMember,
            Bt3NativeMesh.Scene scene,Bt3NativeAssetReader.Motion motion,string costumeSha256)
        {
            Inventory=inventory;Form=form;Costume=costume;ActionMember=actionMember;
            Action=actionMember.PackIndex;CostumeSha256=costumeSha256;ModelSha256=scene.Skeleton.SourceSha256;
            MotionSha256=motion.SourceSha256;Source=new Bt3SourceRig(scene,motion);
        }

        internal static Bt3SourceLease Open(Bt3DiscInventory inventory,Bt3DiscInventory.Bt3FighterSlot form,
            Bt3DiscInventory.Bt3Resource costume,Bt3DiscInventory.Bt3Resource actionMember,
            Bt3SourceLease reusable=null,CancellationToken cancellation=default)
        {
            if(inventory?.Fighters==null||form==null||costume==null||actionMember==null||
                !inventory.Fighters.Any(f=>ReferenceEquals(f,form))||
                !form.Costumes.Any(c=>ReferenceEquals(c,costume))||
                !form.MotionMembers[0].Any(m=>ReferenceEquals(m,actionMember))||
                actionMember.PackIndex<0)
                throw new InvalidDataException("Choose an exact form, costume and primary-bank action from one inspected BT3 disc.");
            cancellation.ThrowIfCancellationRequested();
            byte[] model=inventory.ReadOwned(costume,4*1024*1024);
            string costumeHash=Hash(model);
            cancellation.ThrowIfCancellationRequested();
            if(reusable!=null&&(!ReferenceEquals(reusable.Inventory,inventory)||
                !ReferenceEquals(reusable.Costume,costume)||
                !string.Equals(reusable.CostumeSha256,costumeHash,StringComparison.Ordinal)))
                throw new InvalidDataException("The prepared BT3 costume scene does not match its native source bytes.");
            var scene=reusable?.Source.Scene??Bt3NativeMesh.Read(model);
            byte[] encoded=inventory.ReadOwned(actionMember,4*1024*1024);
            cancellation.ThrowIfCancellationRequested();
            var motion=Bt3NativeAssetReader.ReadMotion(encoded,actionMember.PackIndex);
            if(!string.Equals(motion.SourceSha256,Hash(encoded),StringComparison.Ordinal))
                throw new InvalidDataException("The BT3 source decoder did not retain exact member hashes.");
            return new Bt3SourceLease(inventory,form,costume,actionMember,scene,motion,costumeHash);
        }

        internal void VerifyUnchanged(CancellationToken cancellation=default)
        {
            cancellation.ThrowIfCancellationRequested();
            if(!string.Equals(Hash(Inventory.ReadOwned(Costume,4*1024*1024)),CostumeSha256,StringComparison.Ordinal))
                throw new InvalidDataException("The exact BT3 source costume changed after preparation.");
            cancellation.ThrowIfCancellationRequested();
            if(!string.Equals(Hash(Inventory.ReadOwned(ActionMember,4*1024*1024)),MotionSha256,StringComparison.Ordinal))
                throw new InvalidDataException("The exact BT3 source action changed after preparation.");
            cancellation.ThrowIfCancellationRequested();
        }

        private static string Hash(byte[] bytes)
        {
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
        }
    }
}
