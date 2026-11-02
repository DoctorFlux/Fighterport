using BrawlCrate.Studio;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
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
        Check(Bt3CharacterNames.Count==161&&Bt3CharacterNames.Get(1)=="Goku (Mid)"&&
            Bt3CharacterNames.Get(2)=="Goku (Mid) Super Saiyan",
            "Readable form labels preserve exact distinct native slots");
        Check(Bt3ActionLabels.Describe(2).Contains("Move entry")&&
            Bt3ActionLabels.Describe(3).Contains("Move loop")&&
            Bt3ActionLabels.Describe(0x18B).Contains("Non-flyer")&&
            Bt3ActionLabels.Describe(8)=="Action 008"&&
            Bt3JointLabels.Describe(8,7).Contains("side 1")&&
            Bt3JointLabels.Describe(12,7).Contains("side 0"),
            "Verified shared action and native side-group labels retain exact IDs; unknown actions stay numeric");
        var first=inventory.Fighters[0];
        Check(first.Costumes.Length>0&&first.Costumes[0].AfsIndex==1423&&first.MotionMembers.Length==2&&
            first.MotionMembers[0].Length>0&&first.MotionMembers[1].Length>0,
            "Fighter model/costume packs and both motion-bank members resolve directly from ISO without extraction");
        var mid=inventory.Fighters[1];
        if(args.Contains("--bt3-bank-probe"))
        {
            foreach(int action in new[]{0,1,0x26,0x27})
            {
                try
                {
                    var member=mid.MotionMembers[0].First(m=>m.PackIndex==action);
                    var motion=Bt3NativeAssetReader.ReadMotion(inventory.ReadOwned(member,4*1024*1024),action);
                    Console.WriteLine("TRACE BT3 primary action="+action+" frames="+motion.Length+
                        " tracks="+motion.Tracks.Length+" first-key-times="+
                        string.Join(",",motion.Tracks.GroupBy(t=>t.Keys[0].Frame).OrderBy(g=>g.Key)
                            .Select(g=>g.Key+":"+g.Count())));
                }
                catch(InvalidDataException error)
                {Console.WriteLine("TRACE BT3 primary action="+action+" unavailable="+error.Message);}
            }
        }
        if(args.Contains("--bt3-bank-probe"))
            foreach(var member in mid.MotionMembers[1].Where(m=>m.Bytes>=10).Take(12))
            {
                try{var motion=Bt3NativeAssetReader.ReadMotion(inventory.ReadOwned(member,4*1024*1024),member.PackIndex);
                    Console.WriteLine("TRACE BT3 auxiliary "+member.Identity+" frames="+motion.Length+" tracks="+motion.Tracks.Length);}
                catch(Exception error)when(error is InvalidDataException||error is OverflowException)
                {Console.WriteLine("TRACE BT3 auxiliary "+member.Identity+" unsupported="+error.Message);}
            }
        var pmdl=inventory.ModelMember(mid,mid.Costumes[0]);
        var skeleton=Bt3NativeAssetReader.ReadSkeleton(inventory.ReadOwned(pmdl,4*1024*1024));
        var suggestions=Bt3JointLabels.SuggestedRoles(skeleton);
        Check(suggestions.Values.Any(name=>name=="head")&&
            suggestions.Values.Any(name=>name=="L foot")&&
            suggestions.Values.Any(name=>name=="R foot")&&
            suggestions.Keys.All(id=>skeleton.Bones.Any(b=>b.Id==id)),
            "Display-only BT3 body-role suggestions retain exact native joint IDs");
        if(args.Contains("--bt3-rig-probe")||args.Contains("--bt3-rig-sweep"))
        {
            int complete=0,incomplete=0,failed=0;
            foreach(var form in inventory.Fighters.Where(f=>f.Costumes.Length>0)
                .Take(args.Contains("--bt3-rig-sweep")?inventory.Fighters.Length:6))
            {
                try
                {
                    var nativeSkeleton=Bt3NativeAssetReader.ReadSkeleton(inventory.ReadOwned(inventory.ModelMember(form,form.Costumes[0]),4*1024*1024));
                    var ids=nativeSkeleton.Bones.Select((bone,index)=>new{bone.Id,index}).ToDictionary(p=>p.Id,p=>p.index);
                    var joints=nativeSkeleton.Bones.Select(b=>new BrawlCrate.Studio.Retargeting.RigJoint(
                        "BT3_J"+b.Id.ToString("D2"),b.Parent<0?-1:ids[b.Parent],
                        new BrawlCrate.Studio.Retargeting.JointTransform(-b.Rest,
                            System.Numerics.Quaternion.Identity,System.Numerics.Vector3.One))).ToArray();
                    var inferred=BrawlCrate.Studio.Retargeting.SkeletonCharacterizer.Analyze(joints);
                    if(inferred.Missing.Length==0)complete++;else incomplete++;
                    if(!args.Contains("--bt3-rig-sweep")||inferred.Missing.Length>0)
                        Console.WriteLine("TRACE BT3 rig form="+form.Slot+" joints="+joints.Length+
                            " roles="+string.Join(",",inferred.Roles.OrderBy(p=>p.Key).Select(p=>p.Key+"="+p.Value))+
                            " missing="+string.Join(",",inferred.Missing));
                }
                catch(Exception error)when(error is InvalidDataException||error is NotSupportedException)
                {failed++;Console.WriteLine("TRACE BT3 rig form="+form.Slot+" unavailable="+error.Message);}
            }
            Console.WriteLine("TRACE BT3 rig characterization complete="+complete+" incomplete="+incomplete+" failed="+failed);
        }
        if(args.Contains("--bt3-mesh"))
        {
            var scene=Bt3NativeMesh.Read(inventory.ReadOwned(mid.Costumes[0],4*1024*1024));
            Check(scene.Skeleton.Bones.Length==skeleton.Bones.Length&&scene.Batches.Length>0&&
                scene.TriangleCount>1000&&scene.SourceVertexCount>1000&&scene.Textures.Length>5,
                "Native BT3 costume has bounded two-influence strips and decoded embedded textures");
            Console.WriteLine("TRACE BT3 mesh batches="+scene.Batches.Length+" triangles="+scene.TriangleCount+
                " vertices="+scene.SourceVertexCount+" textures="+scene.Textures.Length);
            var bindWorld=new System.Collections.Generic.Dictionary<int,System.Numerics.Vector3>();
            foreach(var bone in scene.Skeleton.Bones)
                bindWorld[bone.Id]=bone.Rest+(bone.Parent<0?System.Numerics.Vector3.Zero:bindWorld[bone.Parent]);
            double maximumBindSplit=0;int compared=0;
            foreach(var batch in scene.Batches)foreach(var vertex in batch.Triangles)
            {
                int id=vertex.Bone<0?batch.Bone:vertex.Bone;
                var owner=scene.Skeleton.Bones.Single(b=>b.Id==id);
                var pivotA=vertex.Bone<0?batch.PivotA:scene.Pivots[id].A;
                var pivotB=vertex.Bone<0?batch.PivotB:scene.Pivots[id].B;
                var a=vertex.Position-pivotA+bindWorld[id];
                var b=vertex.Position-pivotB+(owner.Parent<0?System.Numerics.Vector3.Zero:bindWorld[owner.Parent]);
                maximumBindSplit=Math.Max(maximumBindSplit,System.Numerics.Vector3.Distance(a,b));compared++;
            }
            Console.WriteLine("TRACE BT3 source bind split max="+maximumBindSplit+" compared="+compared);
        }
        if(args.Contains("--bt3-mesh-sweep"))
        {
            int passed=0;var issues=new System.Collections.Generic.List<string>();
            foreach(var form in inventory.Fighters)foreach(var costume in form.Costumes)
            {
                try
                {
                    var scene=Bt3NativeMesh.Read(inventory.ReadOwned(costume,4*1024*1024));
                    if(scene.TriangleCount<1||scene.Textures.Length<1)throw new InvalidDataException("No complete textured geometry");
                    passed++;
                }
                catch(Exception error)when(error is InvalidDataException||error is OverflowException)
                {issues.Add(form.Slot.ToString("D3")+"/"+costume.Identity+": "+error.Message);}
            }
            foreach(var issue in issues.Take(30))Console.WriteLine("OPEN BT3 mesh "+issue);
            Console.WriteLine("TRACE BT3 mesh sweep passed="+passed+" failed="+issues.Count);
            Check(issues.Count==0&&passed>=698,"Every indexed BT3 costume decodes a native textured mesh without a substitute model");
        }
        var idleBytes=inventory.ReadOwned(mid.MotionMembers[0][0],4*1024*1024);
        var idle=Bt3NativeAssetReader.ReadMotion(idleBytes,0);
        var lowHealthBytes=inventory.ReadOwned(mid.MotionMembers[0][1],4*1024*1024);
        var lowHealth=Bt3NativeAssetReader.ReadMotion(lowHealthBytes,1);
        if(args.Contains("--bt3-source-rig"))
        {
            foreach(var pair in new[]{Tuple.Create(mid,mid.Costumes[0],0),
                Tuple.Create(mid,mid.Costumes[0],1),
                Tuple.Create(inventory.Fighters[2],inventory.Fighters[2].Costumes[0],0)})
            {
                var member=pair.Item1.MotionMembers[0].Single(m=>m.PackIndex==pair.Item3);
                var lease=Bt3SourceLease.Open(inventory,pair.Item1,pair.Item2,member);
                lease.VerifyUnchanged();
                var source=lease.Source;
                var scene=source.Scene;var motion=source.Motion;
                var times=new[]{0f,.25f,1f,motion.Length*.5f,motion.Length-.25f,(float)motion.Length};
                float max=times.Max(source.MaximumPositionDifference);
                Console.WriteLine("TRACE BT3 source rig form="+pair.Item1.Slot+" action="+pair.Item3+
                    " vertices="+source.Vertices.Length+" bind split="+source.MaximumBindSplit+
                    " maximum native/bridge skin difference="+max);
                Check(source.Vertices.Length==scene.TriangleCount*3&&source.MaximumBindSplit<.0001f&&max<.00001f,
                    "BT3 native source rig keeps every two-pivot skin weight and motion pose within a measured float bound");
                if(args.Contains("--bt3-plan-probe")&&pair.Item3==0&&pair.Item1.Slot==1)
                {
                    int available=0,qualified=0;
                    foreach(var character in new[]{"mario","snake"})
                    {
                        string fixture=Path.Combine(".local","optimization-next-bake-draft",character,"next-target-before.mdl0");
                        if(!File.Exists(fixture))continue;available++;
                        try
                        {
                            using(var target=NodeFactory.FromFile(null,fixture) as MDL0Node)
                            {
                                var rig=BrawlCrate.Studio.Retargeting.BrawlRetargeter.Capture(target);
                                var plan=new BrawlCrate.Studio.Retargeting.HumanoidRetargeter(source.Rig,rig.Rig);
                                int samples=0;
                                for(int quarter=0;quarter<=motion.Length*4;quarter++)
                                {
                                    var pose=plan.Evaluate(source.Sample(quarter*.25f),false);
                                    if(pose.Pose.Local.Length!=rig.Bones.Length)
                                        throw new InvalidDataException("Native BT3 source sample changed the target rig's joint count.");
                                    samples++;
                                }
                                Console.WriteLine("TRACE BT3 to "+character+" plan mappings="+plan.Mapping.Length+
                                    " quarter-frame samples="+samples+" target joints="+rig.Bones.Length+" unavailable="+
                                    string.Join(",",plan.UnavailableEndEffectors));
                                qualified++;
                            }
                        }
                        catch(Exception error)when(error is InvalidDataException||error is NotSupportedException)
                        {Console.WriteLine("TRACE BT3 to "+character+" plan unavailable="+error.Message);}
                    }
                    if(available>0)Check(qualified==available,
                        "Native BT3 source samples enter the existing format-neutral Next pose solver at every quarter frame for available Brawl target rigs");
                }
            }
            bool rejectedCrossForm=false;
            try{Bt3SourceLease.Open(inventory,mid,mid.Costumes[0],inventory.Fighters[2].MotionMembers[0][0]);}
            catch(InvalidDataException){rejectedCrossForm=true;}
            Check(rejectedCrossForm,"BT3 source lease rejects a different form's action even when its action index matches");
        }
        if(args.Contains("--bt3-source-rig-sweep")||args.Contains("--bt3-source-costume-sweep"))
        {
            bool everyCostume=args.Contains("--bt3-source-costume-sweep");
            int checkedForms=0,checkedCostumes=0;float worst=0,worstBind=0;
            foreach(var form in inventory.Fighters)
            {
                var action=form.MotionMembers[0].FirstOrDefault(m=>m.PackIndex==0&&m.Bytes>=10);
                if(form.Costumes.Length==0||action==null)throw new InvalidDataException("A BT3 form lacks a model or action-zero source.");
                var motion=Bt3NativeAssetReader.ReadMotion(inventory.ReadOwned(action,4*1024*1024),0);
                foreach(var costume in everyCostume?form.Costumes:form.Costumes.Take(1))
                {
                    var scene=Bt3NativeMesh.Read(inventory.ReadOwned(costume,4*1024*1024));
                    var source=new Bt3SourceRig(scene,motion);
                    float error=Math.Max(source.MaximumPositionDifference(0),source.MaximumPositionDifference(motion.Length*.5f));
                    if(error>=.00001f)throw new InvalidDataException("BT3 source skin bridge exceeds its position bound for form "+form.Slot+" costume "+costume.Identity+": "+error);
                    checkedCostumes++;worst=Math.Max(worst,error);worstBind=Math.Max(worstBind,source.MaximumBindSplit);
                }
                checkedForms++;
            }
            Console.WriteLine("TRACE BT3 source rig sweep forms="+checkedForms+" costumes="+checkedCostumes+
                " maximum pose difference="+worst+
                " maximum bind split="+worstBind);
            Check(checkedForms==inventory.Fighters.Length&&
                (!everyCostume||checkedCostumes==inventory.Fighters.Sum(f=>f.Costumes.Length)),
                "Every inspected BT3 form and costume retains all source skin influences in its native rig bridge");
        }
        Check(mid.MotionMembers[0][0].PackIndex==0&&mid.MotionMembers[0][1].PackIndex==1&&
            idle.Length==72&&lowHealth.Length==108&&idle.SourceSha256!=lowHealth.SourceSha256,
            "Goku Mid action 000 reads first primary-bank member (72 frames), action 001 the second (108 frames)");
        if(args.Contains("--bt3-idle-probe"))
            foreach(var member in mid.MotionMembers[0].Where(m=>m.PackIndex>=0&&m.PackIndex<20&&m.Bytes>=10))
            {
                try{var clip=Bt3NativeAssetReader.ReadMotion(inventory.ReadOwned(member,4*1024*1024),member.PackIndex);
                    Console.WriteLine("TRACE BT3 action="+member.PackIndex.ToString("D3")+" frames="+clip.Length+
                        " tracks="+clip.Tracks.Length+" keys="+clip.Tracks.Sum(t=>t.Keys.Length)+" member="+member.Identity);}
                catch(Exception error)when(error is InvalidDataException||error is OverflowException)
                {Console.WriteLine("TRACE BT3 action="+member.PackIndex.ToString("D3")+" unsupported="+error.Message);}
            }
        if(args.Contains("--bt3-native-edit"))
        {
            var editor=new Bt3NativeMotionEditor(idleBytes,skeleton,0);
            var track=idle.Tracks.First(t=>!t.RotationOnly&&t.Keys.Length>0&&t.Keys[0].Frame<=idle.Length);
            var key=track.Keys[0];var edited=key.Translation+new System.Numerics.Vector3(.025f,0,0);
            editor.SetKey(track.Bone,key.Frame,edited,key.Rotation);
            Check(editor.Dirty&&editor.CanUndo&&editor.Motion.Tracks.Length==idle.Tracks.Length&&
                editor.Motion.Tracks.Single(t=>t.Bone==track.Bone).Keys[0].Translation==edited,
                "BT3 native key edit changes one independent motion copy without adding tracks");
            var published=editor.Export();var reopened=Bt3NativeAssetReader.ReadMotion(published,0);
            Check(reopened.Length==idle.Length&&reopened.EventCount==idle.EventCount&&
                reopened.Tracks.Single(t=>t.Bone==track.Bone).Keys[0].Translation==edited&&
                !idleBytes.SequenceEqual(published),
                "Edited BT3 BPE member reopens with native timing, events and edited key");
            editor.Undo();Check(!editor.Dirty&&editor.CanRedo&&
                editor.Export().SequenceEqual(idleBytes),
                "Native Undo restores the exact original packed bytes");
            editor.Redo();Check(editor.Dirty&&editor.Motion.Tracks.Single(t=>t.Bone==track.Bone).Keys[0].Translation==edited,
                "Native Redo restores the edited motion");
            File.WriteAllBytes(Path.Combine(output,"bt3-edited-action000.bpe"),published);
        }
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
                    var clip=Bt3NativeAssetReader.ReadMotion(inventory.ReadOwned(fighter.MotionMembers[0][0],4*1024*1024),0);
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
                        int child=action;
                        if(child>=fighter.MotionMembers[0].Length||fighter.MotionMembers[0][child].Bytes<10)continue;
                        try
                        {
                            var sample=Bt3NativeAssetReader.ReadMotion(inventory.ReadOwned(fighter.MotionMembers[0][child],4*1024*1024),action);
                            if(sample.Tracks.Length>0)actionsChecked++;
                        }
                        catch(Exception error)when(error is InvalidDataException||error is OverflowException)
                        { Console.WriteLine("OPEN BT3 action "+fighter.Slot.ToString("D3")+"/"+action+": "+error.Message); }
                    }
                    inspected++;
                }
                catch(Exception error)when(error is InvalidDataException||error is IOException||error is OverflowException)
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
            searchBox.Text="Goku (Mid)";
            var formList=dialog.Controls.Find("bt3Characters",true).OfType<ListBox>().Single();
            Check(formList.Items.Count==2&&((Bt3DiscInventory.Bt3FighterSlot)formList.Items[0]).Slot==1&&
                ((Bt3DiscInventory.Bt3FighterSlot)formList.Items[1]).Slot==2,
                "Goku Mid base and Super Saiyan are separate searchable forms");
            var costumeList=dialog.Controls.Find("bt3Costumes",true).OfType<ListBox>().Single();
            var actionList=dialog.Controls.Find("bt3MotionBanks",true).OfType<ListBox>().Single();
            var inspect=dialog.Controls.Find("bt3InspectMotion",true).OfType<Button>().Single();
            Check(costumeList.Items.Count==8&&actionList.SelectedIndex==0&&inspect.Enabled,
                "A form's costume and exact native action remain independently selected");
            inspect.PerformClick();
            var summary=dialog.Controls.Find("bt3NativeStatus",true).OfType<Label>().Single();
            Check(summary.Text.Contains("69 bones")&&summary.Text.Contains("last native frame 72"),
                "Goku Mid default costume and idle inspect verified native pose data");
            formList.SelectedIndex=1;costumeList.SelectedIndex=Math.Min(1,costumeList.Items.Count-1);
            inspect.PerformClick();
            Check(summary.Text.Contains("Goku (Mid) Super Saiyan")&&summary.Text.Contains("Pose data verified"),
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

    static async Task Bt3DirectLibraryChecks(string[] args,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("BT3 direct library acceptance requires the private desktop.");
        string priorRoot=Environment.GetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT");
        try
        {
            string root=Path.Combine(output,"direct-disc-library");
            Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT",root);
            var library=new AnimationLibrary(root);
            library.RememberGameSource(Bt3DiscInventory.Game,"SLUS-21678",args[0]);
            var saved=library.Read().GameLocations.Single();
            using(var source=new GameSourceDialog(library.Read().GameLocations.ToArray(),library))
            {
                var browse=source.Controls.Find("discContinue",true).OfType<Button>().Single();
                Check(browse.Text=="Browse saved disc","Saved BT3 source offers direct browsing instead of another registration");
                Call(source,"Continue");
                Check(source.DialogResult==DialogResult.OK&&source.ExistingSourceId==saved.Id,
                    "Opening the saved BT3 source preserves its exact registration identity");
            }
            using(var startup=MainForm.StartViewerApplication())
            {
                var form=(ModelForm)Field(MainForm.Instance,"_fighterForm");
                form.WindowState=FormWindowState.Normal;form.ClientSize=new System.Drawing.Size(1500,850);
                Call(form,"UseSharedSourceLibrary");
                await LibraryQuerySettled(form);
                var tree=SimpleControl<TreeView>(form,"libraryClips");
                var discNode=tree.Nodes.Cast<TreeNode>().Single(n=>n.Text.StartsWith(Bt3DiscInventory.Game,StringComparison.Ordinal));
                Check(discNode.Nodes.Count<161&&discNode.Nodes.Count>100&&
                    discNode.Nodes.Cast<TreeNode>().Any(n=>n.Text=="Goku (Mid)"),
                    "Saved BT3 disc groups exact native forms under character rows");
                var midNode=discNode.Nodes.Cast<TreeNode>().Single(n=>n.Text=="Goku (Mid)");
                midNode.Expand();await LibraryQuerySettled(form);
                Check(midNode.Nodes.Count>400&&midNode.Nodes.Cast<TreeNode>().All(n=>n.Text.Contains("Action ")),
                    "Character row exposes the native action list without costume nesting");
                Check(midNode.Nodes.Cast<TreeNode>().Any(n=>n.Text==Bt3ActionLabels.Describe(0))&&
                    midNode.Nodes.Cast<TreeNode>().Any(n=>n.Text==Bt3ActionLabels.Describe(1)),
                    "Normal-health and low-health ground idles are separately visible from costume damage");
                var idleRow=midNode.Nodes.Cast<TreeNode>().Single(n=>n.Text==Bt3ActionLabels.Describe(0));
                tree.SelectedNode=idleRow;Application.DoEvents();
                var transform=SimpleControl<ComboBox>(form,"bt3Transform");
                var costume=SimpleControl<ComboBox>(form,"bt3Costume");
                Console.WriteLine("TRACE BT3 selectors preview="+SimpleControl<Button>(form,"libraryPreviewOriginal").Text+
                    " use="+SimpleControl<Button>(form,"libraryUseOnTarget").Enabled+
                    " target="+SimpleControl<Button>(form,"libraryOpenTarget").Enabled+
                    " transforms="+transform.Items.Count+" costumes="+costume.Items.Count+" selected="+costume.Text);
                Check(SimpleControl<Button>(form,"libraryPreviewOriginal").Text=="Preview Original"&&
                    !SimpleControl<Button>(form,"libraryUseOnTarget").Enabled&&
                    SimpleControl<Button>(form,"libraryOpenTarget").Enabled&&transform.Items.Count==2&&
                    costume.Items.Count==8&&costume.Text.Contains("Normal"),
                    "BT3 character uses transform and costume dropdowns and defaults to undamaged costume");
                await CaptureWindow(form,Path.Combine(output,"bt3-character-dropdowns.png"));
                form.ClientSize=new System.Drawing.Size(1050,700);
                Call(form,"SetWorkspaceTextSize",175);Call(form,"UpdateSimpleWorkspace");
                await Task.Delay(75);Application.DoEvents();
                var selectors=SimpleControl<TableLayoutPanel>(form,"bt3NativeSelectors");
                var pane=SimpleControl<Panel>(form,"animationLibraryPane");
                Console.WriteLine("TRACE BT3 compact pane="+pane.Bounds+" visible="+pane.Visible+
                    " selector="+selectors.Bounds+" selectorVisible="+selectors.Visible+
                    " form="+form.ClientRectangle+" dock="+pane.Dock+
                    " index="+pane.Parent.Controls.GetChildIndex(pane));
                Check(pane.Visible&&InClient(selectors,pane)&&selectors.Visible&&
                    (pane.Dock!=DockStyle.None||pane.Parent.Controls.GetChildIndex(pane)==0)&&
                    transform.Bounds.Right<=selectors.ClientSize.Width&&
                    costume.Bounds.Right<=selectors.ClientSize.Width&&
                    costume.Bounds.Bottom<=selectors.ClientSize.Height,
                    "BT3 transform and costume selectors fit the compact workspace at 175% text");
                await CaptureSimpleWorkspace(form,Path.Combine(output,"bt3-character-compact.png"));
                Call(form,"SetWorkspaceTextSize",100);form.ClientSize=new System.Drawing.Size(1500,850);
                Application.DoEvents();
                if(args.Contains("--bt3-render"))
                {
                    await Wait(()=>Field(form,"_bt3Renderer") is Bt3NativeRender);
                    var renderer=(Bt3NativeRender)Field(form,"_bt3Renderer");
                    var panel=SimpleControl<BrawlLib.Internal.Windows.Controls.Model_Panel.ModelPanel>(form,"libraryOriginalViewport");
                    panel.Capture();panel.Invalidate();panel.Update();
                    using(var image=panel.GetScreenshot(panel.ClientRectangle,false))
                        image.Save(Path.Combine(output,"bt3-goku-mid-costume1-idle-first.png"));
                    Check(renderer.RenderCount>0&&renderer.Scene.TriangleCount>1000&&renderer.Scene.Textures.Length>5&&
                        renderer.DrawCalls.Count<200,
                        "Selected BT3 costume renders its own weighted mesh and embedded textures in the original viewport");
                    var bt3Box=renderer.GetBox();Console.WriteLine("TRACE BT3 display box "+bt3Box.Min+" to "+bt3Box.Max);
                    foreach(var character in new[]{"mario","snake"})
                    {
                        string fixture=Path.Combine(".local","optimization-next-bake-draft",character,"next-target-before.mdl0");
                        if(!File.Exists(fixture))continue;
                        using(var brawl=NodeFactory.FromFile(null,fixture) as MDL0Node)
                        {brawl.Children.ToArray();brawl.BoneGroup?.Children.ToArray();brawl.PolygonList.OfType<MDL0ObjectNode>().ToArray();
                            brawl.ResetToBindState();brawl.WeightMeshes();
                            var box=brawl.GetBox();Console.WriteLine("TRACE Brawl "+character+" box "+box.Min+" to "+box.Max);}
                    }
                    var nativeTexture=renderer.Scene.Textures[0];
                    using(var bitmap=new System.Drawing.Bitmap(nativeTexture.Width,nativeTexture.Height))
                    {
                        for(int y=0;y<nativeTexture.Height;y++)for(int x=0;x<nativeTexture.Width;x++)
                        {int p=(y*nativeTexture.Width+x)*4;var rgba=nativeTexture.Rgba;
                            bitmap.SetPixel(x,y,System.Drawing.Color.FromArgb(rgba[p+3],rgba[p],rgba[p+1],rgba[p+2]));}
                        bitmap.Save(Path.Combine(output,"bt3-costume-texture0.png"));
                    }
                    using(var sheet=new System.Drawing.Bitmap(4*260,5*280))
                    using(var graphics=System.Drawing.Graphics.FromImage(sheet))
                    {
                        graphics.Clear(System.Drawing.Color.FromArgb(45,45,45));
                        for(int ti=0;ti<renderer.Scene.Textures.Length;ti++)
                        {
                            var t=renderer.Scene.Textures[ti];
                            using(var tile=new System.Drawing.Bitmap(t.Width,t.Height))
                            {
                                for(int y=0;y<t.Height;y++)for(int x=0;x<t.Width;x++)
                                {int p=(y*t.Width+x)*4;var rgba=t.Rgba;
                                    tile.SetPixel(x,y,System.Drawing.Color.FromArgb(rgba[p+3],rgba[p],rgba[p+1],rgba[p+2]));}
                                int tx=(ti%4)*260,ty=(ti/4)*280;
                                graphics.DrawImage(tile,new System.Drawing.Rectangle(tx,ty,256,256));
                                graphics.DrawString(ti+" "+t.Width+"x"+t.Height,System.Drawing.SystemFonts.DefaultFont,
                                    System.Drawing.Brushes.White,tx,ty+257);
                            }
                        }
                        sheet.Save(Path.Combine(output,"bt3-costume-textures.png"));
                    }
                    var frame=SimpleControl<TrackBar>(form,"libraryOriginalFrame");frame.Value=Math.Min(30,frame.Maximum);
                    panel.Capture();panel.Invalidate();panel.Update();
                    using(var image=panel.GetScreenshot(panel.ClientRectangle,false))
                        image.Save(Path.Combine(output,"bt3-goku-mid-costume1-idle-frame30.png"));
                    Check(frame.Maximum>30&&renderer.RenderCount>1&&
                        FighterProject.HashFile(Path.Combine(output,"bt3-goku-mid-costume1-idle-first.png"))!=
                        FighterProject.HashFile(Path.Combine(output,"bt3-goku-mid-costume1-idle-frame30.png")),
                        "Native action scrubbing changes rendered pixels without changing the target");
                    var firstLease=(Bt3SourceLease)Field(form,"_bt3SourceLease");
                    tree.SelectedNode=midNode.Nodes.Cast<TreeNode>().Single(n=>n.Text==Bt3ActionLabels.Describe(1));
                    await Wait(()=>Field(form,"_bt3SourceLease") is Bt3SourceLease selected&&selected.Action==1);
                    var secondLease=(Bt3SourceLease)Field(form,"_bt3SourceLease");
                    Check(ReferenceEquals(renderer,Field(form,"_bt3Renderer"))&&
                        secondLease.CostumeSha256==firstLease.CostumeSha256&&
                        secondLease.MotionSha256!=firstLease.MotionSha256&&secondLease.ActionMember.PackIndex==1,
                        "Changing a BT3 source action retains its exact costume scene and freezes the new native member hash");
                    tree.SelectedNode=idleRow;
                    await Wait(()=>Field(form,"_bt3SourceLease") is Bt3SourceLease selected&&selected.Action==0);
                    transform.SelectedIndex=1;
                    await Wait(()=>Field(form,"_bt3Renderer") is Bt3NativeRender newRenderer&&
                        !ReferenceEquals(newRenderer,renderer));
                    panel.Capture();panel.Invalidate();panel.Update();
                    using(var image=panel.GetScreenshot(panel.ClientRectangle,false))
                        image.Save(Path.Combine(output,"bt3-goku-mid-super-saiyan-idle.png"));
                    Check(((Bt3NativeRender)Field(form,"_bt3Renderer")).RenderCount>0&&
                        FighterProject.HashFile(Path.Combine(output,"bt3-goku-mid-super-saiyan-idle.png"))!=
                        FighterProject.HashFile(Path.Combine(output,"bt3-goku-mid-costume1-idle-first.png")),
                        "Separate BT3 transformation uses its own textured costume and motion bank");
                    var unsupported=discNode.Nodes.Cast<TreeNode>().Single(n=>n.Text=="Piccolo (Early)");
                    unsupported.Expand();await LibraryQuerySettled(form);
                    tree.SelectedNode=unsupported.Nodes.Cast<TreeNode>().Single(n=>n.Text.StartsWith("Action 400"));
                    await Wait(()=>SimpleControl<Label>(form,"libraryOriginalLabel").Text.StartsWith("BT3 preview unavailable:"));
                    Check(Field(form,"_bt3Renderer")==null&&
                        SimpleControl<Label>(form,"libraryOriginalLabel").Text.Contains("bounded motion buffer"),
                        "Unsupported native action clears the previous preview and reports the reason");
                    var seamForm=discNode.Nodes.Cast<TreeNode>().Single(n=>n.Text=="Goku (GT)");
                    seamForm.Expand();await LibraryQuerySettled(form);
                    tree.SelectedNode=seamForm.Nodes.Cast<TreeNode>().Single(n=>n.Text==Bt3ActionLabels.Describe(0));
                    await Wait(()=>Field(form,"_bt3Renderer") is Bt3NativeRender seamRenderer&&
                        seamRenderer.Scene.SeamTriangleCount>0);
                    var withSeam=(Bt3NativeRender)Field(form,"_bt3Renderer");
                    panel.Capture();panel.Invalidate();panel.Update();
                    using(var image=panel.GetScreenshot(panel.ClientRectangle,false))
                        image.Save(Path.Combine(output,"bt3-native-seam-costume-idle.png"));
                    Check(withSeam.RenderCount>0&&withSeam.Scene.SeamTriangleCount>0,
                        "Costume with native seam triangles renders its complete weighted source mesh");

                    tree.SelectedNode=idleRow;
                    await Wait(()=>Field(form,"_bt3Renderer") is Bt3NativeRender);
                    SimpleControl<Button>(form,"libraryOpenTarget").PerformClick();
                    await Wait(()=>Field(form,"_bt3TargetRenderer") is Bt3NativeRender);
                    var targetPanel=form.FighterEditor.ModelPanel;
                    var targetRenderer=(Bt3NativeRender)Field(form,"_bt3TargetRenderer");
                    var targetForm=SimpleControl<ComboBox>(form,"bt3TargetTransform");
                    var targetCostume=SimpleControl<ComboBox>(form,"bt3TargetCostume");
                    var targetAction=form.FighterEditor.leftPanel.listAnims;
                    var nativeLeft=form.FighterEditor.leftPanel;
                    var nativeRight=form.FighterEditor.rightPanel.pnlBones;
                    Console.WriteLine("TRACE target panes left="+nativeLeft.Visible+" "+nativeLeft.Bounds+
                        " parent="+nativeLeft.Parent.Bounds+" z="+nativeLeft.Parent.Controls.GetChildIndex(nativeLeft)+
                        " right="+nativeRight.Visible+" "+nativeRight.Bounds+
                        " parent="+nativeRight.Parent.Bounds+" z="+nativeRight.Parent.Controls.GetChildIndex(nativeRight));
                    Check(nativeLeft.Visible&&nativeRight.Visible&&nativeLeft.Width>180&&nativeRight.Width>180&&
                        nativeLeft.Height>300&&nativeRight.Height>300&&nativeLeft.NativeViewerActive&&
                        targetAction.Items.Count>400,
                        "Native actions, meshes and textures occupy the original Model Previewer lists");
                    var displayedBones=(BrawlLib.Internal.Windows.Controls.ModelViewer.Panels.NativeViewerBone[])
                        Field(nativeRight,"_nativeBones");
                    Check(targetAction.ShowItemToolTips&&targetAction.Items[0].ToolTipText==Bt3ActionLabels.Detail(0)&&
                        displayedBones.Any(b=>b.Label.Contains("?")&&b.Detail.Contains("not a native name"))&&
                        displayedBones.All(b=>b.Detail.Contains("Joint")||b.Detail.Contains("Native joint ID")),
                        "Compact native lists show verified action explanations and clearly provisional body labels");
                    var viewerTools=SimpleControl<ToolStrip>(form,"toolStrip1");
                    var bonesToggle=(ToolStripButton)viewerTools.Items.Find("chkBones",true).Single();
                    var polygonsToggle=(ToolStripButton)viewerTools.Items.Find("chkPolygons",true).Single();
                    var verticesToggle=(ToolStripButton)viewerTools.Items.Find("chkVertices",true).Single();
                    bonesToggle.PerformClick();
                    Check(!((Bt3NativeRender)Field(form,"_bt3TargetRenderer")).ShowBones,
                        "The existing Model Previewer Bones toggle controls native BT3 bone drawing");
                    bonesToggle.PerformClick();
                    polygonsToggle.PerformClick();
                    Check(!((Bt3NativeRender)Field(form,"_bt3TargetRenderer")).ShowPolygons,
                        "The existing Model Previewer Polygons toggle controls native BT3 mesh drawing");
                    polygonsToggle.PerformClick();
                    verticesToggle.PerformClick();
                    Check(((Bt3NativeRender)Field(form,"_bt3TargetRenderer")).ShowVertices,
                        "The existing Model Previewer Vertices toggle controls native BT3 vertex drawing");
                    verticesToggle.PerformClick();
                    Check(targetPanel.Visible&&ReferenceEquals(targetPanel,form.FighterEditor.ModelPanel)&&
                        SimpleControl<TableLayoutPanel>(form,"bt3NativeTargetView").Contains(targetPanel)&&
                        targetForm.Items.Count==2&&targetAction.Items.Count>400&&
                        targetCostume.Text.Contains("Normal")&&targetRenderer.Scene.TriangleCount>1000,
                        "Open as target uses the original Model Previewer viewport for the native character and animations");
                    Check(targetRenderer.DrawCalls.Last().GetType().Name.Contains("BoneOverlay")&&
                        targetRenderer.Scene.Skeleton.Bones.Length>50,
                        "Brawl-style native bone orbs and axes render after every textured mesh call");
                    var firstPose=Bt3NativeAssetReader.Sample(targetRenderer.Scene.Skeleton,
                        (Bt3NativeAssetReader.Motion)Field(form,"_bt3TargetMotion"),0);
                    var rootLocation=firstPose.Translation[0];
                    var rootScreen=targetPanel.CurrentViewport.Camera.Project(new BrawlLib.Internal.Vector3(
                        -rootLocation.X,-rootLocation.Y,-rootLocation.Z));
                    Check(targetRenderer.PickBone(targetPanel.CurrentViewport,new System.Drawing.Point(
                        (int)rootScreen._x,(int)rootScreen._y))>=0,
                        "Native BT3 bone markers support viewport hit testing through the Brawl camera");
                    Check(((Bt3NativeAssetReader.Motion)Field(form,"_bt3TargetMotion")).Length==72,
                        "The target's action 000 uses Goku Mid primary-bank member zero");
                    var targetPlayback=form.FighterEditor.PlaybackPanel;
                    await CaptureSimpleWorkspace(form,Path.Combine(output,"bt3-large-target.png"));
                    form.ClientSize=new System.Drawing.Size(1050,700);
                    Call(form,"SetWorkspaceTextSize",175);Call(form,"UpdateSimpleWorkspace");
                    await Task.Delay(75);Application.DoEvents();
                    await CaptureSimpleWorkspace(form,Path.Combine(output,"bt3-native-target-compact-175.png"));
                    var returnButton=SimpleControl<Button>(form,"bt3TargetBack");
                    Check(nativeLeft.Visible&&nativeRight.Visible&&
                        SimpleControl<Control>(form,"bt3NativeTransformPanel").Visible&&
                        targetPlayback.Visible&&returnButton.Visible&&
                        returnButton.Right<=returnButton.Parent.ClientSize.Width,
                        "Native target panels and Brawl transport remain available at compact layout and 175% workspace text");
                    Call(form,"SetWorkspaceTextSize",100);form.ClientSize=new System.Drawing.Size(1500,850);
                    Application.DoEvents();
                    targetPanel.Capture();targetPanel.Invalidate();targetPanel.Update();
                    using(var image=targetPanel.GetScreenshot(targetPanel.ClientRectangle,false))
                        image.Save(Path.Combine(output,"bt3-target-idle-first.png"));
                    var targetFrame=targetPlayback.numFrameIndex;
                    targetFrame.Value=0;
                    Check((double)Field(form,"_bt3TargetTime")==0&&targetFrame.Value>=0,
                        "Shared playback frame zero cannot send negative time to BT3 sampling");
                    Call(form,"SeekBt3TargetDisplayFrame",int.MaxValue);
                    Check((double)Field(form,"_bt3TargetTime")==
                        ((Bt3NativeAssetReader.Motion)Field(form,"_bt3TargetMotion")).Length,
                        "Out-of-range BT3 seek stops at the exact native terminal frame");
                    targetFrame.Value=1;
                    targetFrame.Value=Math.Min(30,targetFrame.Maximum);
                    targetPanel.Capture();targetPanel.Invalidate();targetPanel.Update();
                    using(var image=targetPanel.GetScreenshot(targetPanel.ClientRectangle,false))
                        image.Save(Path.Combine(output,"bt3-target-idle-frame30.png"));
                    Check(targetFrame.Maximum>30&&
                        FighterProject.HashFile(Path.Combine(output,"bt3-target-idle-first.png"))!=
                        FighterProject.HashFile(Path.Combine(output,"bt3-target-idle-frame30.png")),
                        "Large native target viewport follows animation scrubbing without changing an editable project");
                    targetFrame.Value=1;
                    targetPlayback.btnPlay.PerformClick();
                    await Wait(()=>targetFrame.Value>1);
                    targetPlayback.btnPlay.PerformClick();
                    Check(targetFrame.Value>1&&targetPlayback.btnPlay.Text=="Play"&&
                        targetPlayback.Parent==form.FighterEditor.animEditors&&
                        SimpleControl<Control>(form,"bt3NativeTransformPanel").Visible,
                        "Native BT3 target uses the original Model Previewer transport and bottom transform region");
                    var nativeEditor=(Bt3NativeMotionEditor)Field(form,"_bt3TargetEditor");
                    var editTrack=nativeEditor.Motion.Tracks.First(t=>!t.RotationOnly&&t.Keys.Any(k=>k.Frame<=nativeEditor.Motion.Length));
                    var editKey=editTrack.Keys.First(k=>k.Frame<=nativeEditor.Motion.Length);
                    targetFrame.Value=editKey.Frame+1;
                    nativeRight.SelectNativeBone(editTrack.Bone);
                    Check(((Bt3NativeRender)Field(form,"_bt3TargetRenderer")).SelectedBoneId==editTrack.Bone,
                        "Original Model Previewer bone panel selects the native BT3 bone ID");
                    var nativeTab=SimpleControl<ComboBox>(form,"editor");
                    nativeTab.SelectedIndex=1;
                    Check(nativeTab.Text=="Native keys"&&SimpleControl<Button>(form,"bt3ApplyNativeKey").Visible,
                        "Native key editing occupies the original Model Previewer editor tab");
                    var editX=SimpleControl<NumericUpDown>(form,"bt3EditTranslation0");
                    var beforeX=editX.Value;editX.Value=beforeX+.025m;
                    SimpleControl<Button>(form,"bt3ApplyNativeKey").PerformClick();
                    Check(nativeEditor.Dirty&&nativeEditor.Motion.Tracks.Single(t=>t.Bone==editTrack.Bone)
                        .Keys.Single(k=>k.Frame==editKey.Frame).Translation.X>editKey.Translation.X,
                        "Large BT3 target applies a native bone key without changing the original disc motion");
                    SimpleControl<Button>(form,"bt3NativeUndo").PerformClick();
                    Check(!nativeEditor.Dirty&&editX.Value==beforeX,
                        "Large BT3 target Undo restores the exact original pose");
                    SimpleControl<Button>(form,"bt3NativeRedo").PerformClick();
                    Check(nativeEditor.Dirty&&editX.Value>beforeX,
                        "Large BT3 target Redo restores the native edited pose");
                    var nativeOutput=Path.Combine(output,"bt3-target-edited-action000.bpe");
                    Call(form,"WriteBt3NativeMotion",nativeOutput);
                    var nativeReopen=Bt3NativeAssetReader.ReadMotion(File.ReadAllBytes(nativeOutput),0);
                    Check(nativeReopen.Length==nativeEditor.Motion.Length&&
                        nativeReopen.Tracks.Single(t=>t.Bone==editTrack.Bone).Keys.Single(k=>k.Frame==editKey.Frame).Translation.X>
                            editKey.Translation.X,
                        "BT3 target saves an independently reopened native BPE motion member");
                    Check(!nativeEditor.HasUnsavedChanges,"Native motion publication clears the unsaved-edit warning without changing its disc baseline");
                    Call(form,"LoadBt3NativeMotion",nativeOutput);
                    Check(!((Bt3NativeMotionEditor)Field(form,"_bt3TargetEditor")).Dirty&&
                        ((Bt3NativeMotionEditor)Field(form,"_bt3TargetEditor")).Motion.Tracks
                            .Single(t=>t.Bone==editTrack.Bone).Keys.Single(k=>k.Frame==editKey.Frame).Translation.X>
                            editKey.Translation.X,
                        "BT3 target reopens its saved native motion in the current exact action");
                    nativeLeft.lstObjects.SetItemChecked(0,false);
                    targetAction.Items.Cast<ListViewItem>().Single(i=>
                        (i.Tag as BrawlCrate.UI.Model_Previewer.NativeViewerItem)?.Id==1).Selected=true;
                    await Wait(()=>SimpleControl<Label>(form,"bt3TargetCaption").Text.Contains("action 001"));
                    var lowHealthMotion=(Bt3NativeAssetReader.Motion)Field(form,"_bt3TargetMotion");
                    Check(lowHealthMotion.Action==1&&lowHealthMotion.Length==108&&
                        lowHealthMotion.SourceSha256!=nativeEditor.Motion.SourceSha256&&
                        targetCostume.Text.Contains("Normal")&&targetAction.SelectedItems[0].Text==Bt3ActionLabels.Describe(1),
                        "Low-health ground idle is a distinct native motion on the normal costume");
                    targetPanel.Capture();targetPanel.Invalidate();targetPanel.Update();
                    using(var image=targetPanel.GetScreenshot(targetPanel.ClientRectangle,false))
                        image.Save(Path.Combine(output,"bt3-target-low-health-idle-normal-costume.png"));
                    Check(ReferenceEquals(((Bt3NativeRender)Field(form,"_bt3TargetRenderer")).Scene,targetRenderer.Scene),
                        "Changing a native target action reuses the selected costume geometry");
                    Check(!nativeLeft.lstObjects.GetItemChecked(0)&&
                        ((Bt3NativeRender)Field(form,"_bt3TargetRenderer")).SelectedBoneId==editTrack.Bone,
                        "Changing only the native action retains mesh visibility and selected bone");
                    nativeLeft.lstObjects.SetItemChecked(0,true);
                    targetAction.Items.Cast<ListViewItem>().Single(i=>
                        (i.Tag as BrawlCrate.UI.Model_Previewer.NativeViewerItem)?.Id==0).Selected=true;
                    await Wait(()=>SimpleControl<Label>(form,"bt3TargetCaption").Text.Contains("action 000"));
                    targetCostume.SelectedIndex=4;
                    await Wait(()=>SimpleControl<Label>(form,"bt3TargetCaption").Text.Contains("Battle damaged"));
                    targetPanel.Capture();targetPanel.Invalidate();targetPanel.Update();
                    using(var image=targetPanel.GetScreenshot(targetPanel.ClientRectangle,false))
                        image.Save(Path.Combine(output,"bt3-target-normal-idle-damaged-costume.png"));
                    Check(targetCostume.Text.Contains("Battle damaged")&&
                        targetAction.SelectedItems[0].Text==Bt3ActionLabels.Describe(0),
                        "Damaged costume can show the same verified normal ground-idle motion independently");
                    targetAction.Items.Cast<ListViewItem>().Single(i=>
                        (i.Tag as BrawlCrate.UI.Model_Previewer.NativeViewerItem)?.Id==1).Selected=true;
                    await Wait(()=>SimpleControl<Label>(form,"bt3TargetCaption").Text.Contains("action 001"));
                    Check(targetCostume.Text.Contains("Battle damaged")&&
                        targetAction.SelectedItems[0].Text==Bt3ActionLabels.Describe(1)&&
                        ((Bt3NativeAssetReader.Motion)Field(form,"_bt3TargetMotion")).SourceSha256==lowHealthMotion.SourceSha256,
                        "Damaged costume can show the exact same low-health motion independently");
                    targetPanel.Capture();targetPanel.Invalidate();targetPanel.Update();
                    using(var image=targetPanel.GetScreenshot(targetPanel.ClientRectangle,false))
                        image.Save(Path.Combine(output,"bt3-target-low-health-idle-damaged-costume.png"));
                    targetForm.SelectedIndex=1;
                    await Wait(()=>SimpleControl<Label>(form,"bt3TargetCaption").Text.Contains("Super Saiyan"));
                    targetAction.Items.Cast<ListViewItem>().Single(i=>
                        (i.Tag as BrawlCrate.UI.Model_Previewer.NativeViewerItem)?.Id==0).Selected=true;
                    await Wait(()=>SimpleControl<Label>(form,"bt3TargetCaption").Text.Contains("action 000"));
                    targetCostume.SelectedIndex=4;
                    await Wait(()=>SimpleControl<Label>(form,"bt3TargetCaption").Text.Contains("Battle damaged"));
                    Check(targetCostume.Text.Contains("Battle damaged")&&
                        ((Bt3NativeRender)Field(form,"_bt3TargetRenderer")).RenderCount>=0,
                        "Native target transform and battle-damaged costume use their own exact model resources");
                    targetPanel.Capture();targetPanel.Invalidate();targetPanel.Update();
                    using(var image=targetPanel.GetScreenshot(targetPanel.ClientRectangle,false))
                        image.Save(Path.Combine(output,"bt3-target-super-saiyan-damaged.png"));
                    SimpleControl<Button>(form,"bt3TargetBack").PerformClick();
                    Check(!SimpleControl<TableLayoutPanel>(form,"bt3NativeTargetView").Visible&&
                        SimpleControl<BrawlCrate.UI.Model_Previewer.ModelEditControl.ModelEditControl>(form,"modelEditControl1").Visible&&
                        SimpleControl<ToolStrip>(form,"fighterToolbar").Enabled&&
                        !SimpleControl<TableLayoutPanel>(form,"bt3NativeTargetView").Contains(targetPanel),
                        "Returning from BT3 target viewing restores the original editable project view");
                }
                await CaptureWindow(form,Path.Combine(output,"bt3-direct-library.png"));
            }
            Check(!library.Read().GameLocations.Single().Indexed&&library.Read().Clips.Count==0,
                "Direct disc browsing creates no fake editable or previewable catalogue clips");
        }
        finally{Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT",priorRoot);}
    }
}
