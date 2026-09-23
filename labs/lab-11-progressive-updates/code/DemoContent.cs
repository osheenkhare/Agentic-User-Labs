using System.Runtime.CompilerServices;

namespace ProgressiveUpdates;

internal static class DemoContent
{
    private static readonly string[] Chunks =
    [
        "This ",
        "long-running ",
        "operation ",
        "continues ",
        "after the incoming ",
        "Teams request ",
        "has completed.",
        "\n\nThe final response was assembled through background progress updates.",
    ];

    public static async IAsyncEnumerable<string> GenerateChunksAsync(
        TimeSpan duration,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        TimeSpan chunkDelay = TimeSpan.FromMilliseconds(
            Math.Max(250, duration.TotalMilliseconds / Chunks.Length));

        foreach (string chunk in Chunks)
        {
            await Task.Delay(chunkDelay, cancellationToken);
            yield return chunk;
        }
    }

    public static List<WorkPlanStep> CreateWorkPlanSteps() =>
    [
        new("Understand the request", WorkPlanStatus.Pending),
        new("Gather the required context", WorkPlanStatus.Pending),
        new("Complete the requested work", WorkPlanStatus.Pending),
        new("Verify the result", WorkPlanStatus.Pending),
    ];
}
