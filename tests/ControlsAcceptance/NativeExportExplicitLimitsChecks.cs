using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;

internal static partial class Program
{
    // Independent adapter gate probes. The Core continuous certificate is
    // separately tested; these deliberately supplied candidates exercise the
    // final cumulative native gate with tighter saved limits.
    static void NativeExportExplicitLimitsChecks(MDL0Node model, string stage)
    {
        Directory.CreateDirectory(stage);
        string[] names=model.AllBones.Select(b=>b.Name).ToArray();
        foreach(var probe in new[]{new{Channel=3,Delta=.0525f,Tight=.025},new{Channel=6,Delta=.0015f,Tight=.001},new{Channel=3,Delta=.005f,Tight=.001}})
        using(var master=OptimizationFixture(names.Last(),"Independent explicit export limit "+probe.Channel))
        {
            var entry=(CHR0EntryNode)master.Children[0];
            OptimizationReplace(entry,probe.Channel,Enumerable.Range(0,5).Select(i=>OptimizationKey(i,i*.125f,.125f)).ToArray());
            var candidate=OptimizationTrack(entry);
            foreach(var key in candidate.Channels[probe.Channel])key.Value+=probe.Delta;
            string content=NativeAnimationOptimization.ContentKey(master);
            var options=new NativeAnimationOptimizationOptions{ExactOnly=false,SmallFile=true,TargetBones=names,
                LengthScale=NativeAnimationOptimization.GetBindBoundsDiagonal(model),DeferSizeValidation=true,
                ExportSettings=new NativeExportOptimizationSettings{Policy=NativeExportOptimizationPolicy.ErrorBounded},
                ExactBones=probe.Delta==.005f?new[]{names.Last()}:new string[0]};
            using(var accepted=NativeAnimationOptimization.Prepare(model,master,new[]{candidate},options,stage))
                Check(probe.Channel==3?accepted.MaximumOrientationError>probe.Delta*.9&&accepted.MaximumOrientationError<.1:
                    accepted.MaximumLocalTranslationError>.001&&accepted.MaximumLocalTranslationError<.002,
                    "Explicit export limits: ordinary approved bound admits the independently supplied "+probe.Channel+" delta after real serialization/native evaluation");
            if(probe.Channel==3)options.ExportSettings.RotationDegrees=probe.Tight;
            else options.ExportSettings.Translation=probe.Tight;
            bool rejected=false;
            try{using(var ignored=NativeAnimationOptimization.Prepare(model,master,new[]{candidate},options,stage)){}}
            catch(InvalidDataException error){rejected=error.Data.Contains("OptimizationQuality");}
            Check(rejected&&NativeAnimationOptimization.ContentKey(master)==content,
                "Explicit export limits: a tighter saved "+probe.Channel+" bound rejects the same cumulative decoded candidate without mutating its master");
        }
    }
}
