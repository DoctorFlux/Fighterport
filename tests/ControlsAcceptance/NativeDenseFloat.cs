using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

internal static partial class Program
{
    // Dedicated procedural storage/quality qualification. The supplied model
    // supplies authentic native hierarchy/binds/all weights, not an invented
    // protection context for any imported animation. All tested animation
    // curves and exact protection policies are owned procedural fixtures.
    static async Task NativeDenseFloat(string[] args,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Native L4 export-copy checks require the private desktop.");
        string modelFile=Path.GetFullPath(args[0]),motionFile=Path.GetFullPath(args[1]);
        var originals=new[]{modelFile,motionFile}.Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(path=>path,FighterProject.HashFile,StringComparer.OrdinalIgnoreCase);
        using(var root=NodeFactory.FromFile(null,modelFile))
        {
            if(root==null)throw new InvalidDataException("The independent native target could not be decoded.");
            root.Populate();
            var models=Walk(root).OfType<MDL0Node>().ToArray();
            if(models.Length!=1)throw new InvalidDataException("L4 primitive qualification requires one exact native model.");
            var model=models[0];model.Populate();
            if(!(NativeAnimationOptimization.GetBindBoundsDiagonal(model)>0))
                throw new InvalidDataException("L4 primitive quality checks require finite positive authentic bind geometry.");
            await NativeDenseFloatChecks(model,output);
        }
        Check(originals.All(pair=>FighterProject.HashFile(pair.Key)==pair.Value),
            "L4 primitive qualification preserves every supplied model/motion file byte");
    }
}
