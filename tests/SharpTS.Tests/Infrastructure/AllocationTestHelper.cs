using Xunit;

namespace SharpTS.Tests.Infrastructure;

internal static class AllocationTestHelper
{
    internal static long[] MeasureRunAllocations(Func<double> run, double expectedResult)
    {
        Assert.Equal(expectedResult, run()); // Warm JIT and emitted runtime state.
        var samples = new long[5];
        // Use the steady-state floor so transient runtime bookkeeping on busy CI
        // runners does not fail the gate. Persistent allocations remain in every sample.
        for (int sample = 0; sample < samples.Length; sample++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            double result = run();
            samples[sample] = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(expectedResult, result);
        }
        return samples;
    }
}
