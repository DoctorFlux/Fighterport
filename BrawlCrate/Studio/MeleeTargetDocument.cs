using FighterPort.Adapters.Melee;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Modeling;
using System;
using System.IO;
using System.Linq;
using System.Threading;
namespace BrawlCrate.Studio
{
    internal static class MeleeTargetDocument
    {
        internal static FighterProject Prepare(LibrarySourceLease source,LibraryCatalogue catalogue,CancellationToken cancellation)
        {
            if(source?.MeleeModel==null||source.Reference==null||source.MeleeRenderer?.Parts==null)throw new InvalidDataException("Choose an exact normal-detail native model and animation.");
            var reference=source.Reference;var project=FighterProject.Create(source.Model.Name);
            try
            {
                var record=catalogue.Models.Single(m=>m.Id==reference.ModelId);var modelMount=catalogue.Mounts.Single(m=>m.Id==record.MountId);var motionMount=catalogue.Mounts.Single(m=>m.Id==reference.MotionMountId);
                var nativeMotion=source.MeleeMotionMembers.SelectMany(MeleeNative.Motions).Single(m=>m.Key==reference.MotionKey&&m.Member.Sha256==reference.MotionResourceSha256);
                var modelSource=project.CreateNativeMaster("original-model.dat","native-source",path=>File.WriteAllBytes(path,MeleeGameSource.Read(modelMount,cancellation)));project.NativeSources.Add(modelSource);
                var motionSource=project.CreateNativeMaster("original-motion.dat","native-source",path=>File.WriteAllBytes(path,nativeMotion.Member.CopyOriginalBytes()));project.NativeSources.Add(motionSource);
                string contextPath=motionMount.VirtualPath.EndsWith("AJ.dat",StringComparison.OrdinalIgnoreCase)?motionMount.VirtualPath.Substring(0,motionMount.VirtualPath.Length-6)+".dat":null;
                var context=catalogue.Mounts.SingleOrDefault(m=>m.GameSourceId==motionMount.GameSourceId&&string.Equals(m.VirtualPath,contextPath,StringComparison.OrdinalIgnoreCase));
                if(context==null)throw new InvalidDataException("Normal-detail fighter context is unavailable.");
                var contextSource=project.CreateNativeMaster("original-context.dat","native-source",path=>File.WriteAllBytes(path,MeleeGameSource.Read(context,cancellation)));project.NativeSources.Add(contextSource);
                FighterAsset commonSource=null;
                var commonMount=catalogue.Mounts.SingleOrDefault(m=>m.GameSourceId==motionMount.GameSourceId&&string.Equals(Path.GetFileName(m.VirtualPath),"PlCo.dat",StringComparison.OrdinalIgnoreCase));
                if(commonMount!=null){commonSource=project.CreateNativeMaster("original-common.dat","native-source",path=>File.WriteAllBytes(path,MeleeGameSource.Read(commonMount,cancellation)));project.NativeSources.Add(commonSource);}
                FighterAsset executable=null;byte[] dol=null;
                var location=catalogue.GameLocations.Single(l=>l.Id==motionMount.GameSourceId);
                try{using(var reader=new MeleeSource(location.Path,MeleeGameSource.BasePath(catalogue,location),cancellation))dol=reader.ReadExecutable();}
                catch(FileNotFoundException){/* Identical native costume tables can still bind without executable context. */}
                if(dol!=null){executable=project.CreateNativeMaster("original-main.dol","native-source",path=>File.WriteAllBytes(path,dol));project.NativeSources.Add(executable);}
                cancellation.ThrowIfCancellationRequested();
                project.Model=project.CreateNativeMaster("editable-model.mdl0","model",path=>source.Model.ExportChecked(path));
                var animation=project.CreateNativeMaster("editable-animation.chr0","animation",path=>{using(var master=MeleeAnimationMaster.Prepare(source.MeleeModel,nativeMotion,source.PreviewAnimation.Name))master.ExportChecked(path);});project.Animations.Add(animation);
                var baseline=project.CreateNativeMaster("original-authoring.chr0","native-source",path=>File.Copy(animation.WorkingPath,path));project.NativeSources.Add(baseline);
                project.TargetGame="Melee";project.Frame=1;
                project.NativeTarget=new NativeTargetBinding{Provider=MeleeGameSource.NativeProvider,AnimationBaselineSource=baseline.Id,AnimationMasterId=animation.Id,ModelSource=modelSource.Id,MotionSource=motionSource.Id,ContextSource=contextSource.Id,CommonSource=commonSource?.Id,ExecutableSource=executable?.Id,ModelKey=reference.ModelKey,MotionKey=reference.MotionKey,MotionSnapshotKey="0/"+nativeMotion.Symbol,OriginMotionContainerSha256=reference.MotionContainerSha256,SourceRig=reference.SourceRig,NativeModelSha256=reference.ModelResourceSha256,NativeMotionSha256=reference.MotionResourceSha256,OriginContainerSha256=reference.ModelContainerSha256,OriginModelPath=modelMount.OriginPath??modelMount.LocalPath,OriginMotionPath=motionMount.OriginPath??motionMount.LocalPath,AuthoringModelSha256=project.Model.ContentSha256,AuthoringAnimationSha256=animation.ContentSha256,PartChoices=Enumerable.Range(0,source.MeleeRenderer.Parts.GroupCount).Select(source.MeleeRenderer.Parts.Choice).ToArray()};
                using(var model=NodeFactory.FromFile(null,project.Model.WorkingPath) as MDL0Node)using(var clip=NodeFactory.FromFile(null,animation.WorkingPath) as CHR0Node)
                {if(model==null||clip==null)throw new InvalidDataException("Editable native masters did not reopen.");MeleeAnimationMaster.VerifySerialized(source.MeleeModel,nativeMotion,clip);project.ModelNodePath=model.TreePath;project.SelectedAnimation=animation.Id+"|@root";}
                Validate(project);return project;
            }
            catch{project.Dispose();throw;}
        }
        internal static MeleeNativeModel OriginalModel(FighterProject project)
        {
            var binding=project.NativeTarget;var source=project.NativeSources.Single(s=>s.Id==binding.ModelSource);
            var model=MeleeNative.ReadMembers(File.ReadAllBytes(source.WorkingPath)).SelectMany(MeleeNative.Models).Single(m=>m.Key==binding.ModelKey&&m.Member.Sha256==binding.NativeModelSha256);
            if(MeleeNative.RigHash(model)!=binding.SourceRig)throw new InvalidDataException("The owned native rig snapshot changed.");return model;
        }
        internal static MeleeNativeMotion OriginalMotion(FighterProject project)
        {
            var binding=project.NativeTarget;var source=project.NativeSources.Single(s=>s.Id==binding.MotionSource);
            return MeleeNative.ReadMembers(File.ReadAllBytes(source.WorkingPath)).SelectMany(MeleeNative.Motions).Single(m=>m.Key==binding.MotionSnapshotKey&&m.Member.Sha256==binding.NativeMotionSha256);
        }
        internal static MeleePartVisibility Parts(FighterProject project,MeleeNativeModel model)
        {
            var binding=project.NativeTarget;var context=project.NativeSources.Single(s=>s.Id==binding.ContextSource);
            string actor=MeleeInventory.MotionActor(OriginalMotion(project).Symbol);string symbol="ftData"+actor.Substring(3,actor.Length-5);
            var data=MeleeNative.ReadMembers(File.ReadAllBytes(context.WorkingPath)).SelectMany(m=>m.Native.Roots).Where(r=>r.Name==symbol).Select(r=>r.Data as HSDRaw.Melee.Pl.SBM_FighterData).Single()??throw new InvalidDataException("Owned fighter-data root is unavailable.");
            int? costume=null;
            if(binding.ExecutableSource!=null)
            {
                var exe=project.NativeSources.Single(s=>s.Id==binding.ExecutableSource);byte[] dol=File.ReadAllBytes(exe.WorkingPath);
                // Bind by the native model symbol. The original virtual filename is
                // recovered from the verified source table, never guessed by color.
                costume=MeleeCostumeBinding.FindIndexByJoint(dol,model.Symbol,actor,data.ModelLookupTables.CostumeVisibilityLookups.Length);
            }
            var parts=MeleePartVisibility.Create(data.ModelLookupTables,MeleeProjection.DisplayObjectCount(model),context.ContentSha256,symbol,costume);
            if(parts.GroupCount!=binding.PartChoices.Length)throw new InvalidDataException("Native part context changed.");
            for(int i=0;i<parts.GroupCount;i++)parts.Select(i,binding.PartChoices[i]);return parts;
        }
        internal static void Validate(FighterProject project)
        {
            if(project.NativeTarget?.Provider!=MeleeGameSource.NativeProvider||project.TargetGame!="Melee")throw new InvalidDataException("Incorrect native target adapter.");
            foreach(var source in project.NativeSources)if(FighterProject.HashFile(source.WorkingPath)!=source.ContentSha256)throw new InvalidDataException("An owned native source snapshot changed.");
            var model=OriginalModel(project);var motion=OriginalMotion(project);Parts(project,model);
            using(var authoring=NodeFactory.FromFile(null,project.Model.WorkingPath) as MDL0Node)
            {if(authoring!=null){authoring.Children.ToArray();authoring.BoneGroup?.Children.ToArray();}
             if(authoring==null)throw new InvalidDataException("Editable model is unavailable.");NativeJointIdentity.Bind(authoring,MeleeGameSource.NativeProvider,project.NativeTarget.SourceRig,MeleeNative.Joints(model.Root).Length);}
        }
        internal static INativeModelEditingBackend Attach(FighterProject project,MDL0Node model)
        {return new MeleeModelEditingBackend(project,model,OriginalModel(project),OriginalMotion(project));}
    }
}
