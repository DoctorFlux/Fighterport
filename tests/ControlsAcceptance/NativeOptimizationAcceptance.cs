using System;
using System.Threading.Tasks;

internal static partial class Program
{
    // CPU-only adapter qualification shares the private-desktop harness without
    // duplicating the longer interactive workspace lifecycle around every case.
    static Task NativeAnimationOptimizationAcceptance(string[] args, string output)
    {
        if (!PrivateDesktop) throw new InvalidOperationException("Native optimization qualification requires the private desktop.");
        NativeOptimizationChecks(args[0], output);
        NativeSourceSnapshotChecks(System.IO.Path.Combine(output, "native-source-snapshots"));
        NativeConstantOptimizationChecks(args[0], System.IO.Path.Combine(output, "native-constants"));
        NativeCompactStorageChecks(System.IO.Path.Combine(output, "compact-storage"));
        NativeCompactRuntimeChecks(args[0], System.IO.Path.Combine(output, "compact-runtime")).GetAwaiter().GetResult();
        NativeSelectiveReductionChecks(args[0], System.IO.Path.Combine(output, "selective-reduction"));
        return Task.CompletedTask;
    }
}
