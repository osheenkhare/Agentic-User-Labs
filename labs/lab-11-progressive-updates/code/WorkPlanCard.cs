using Microsoft.Teams.Apps.Schema;
using Microsoft.Teams.Cards;
using Microsoft.Teams.Common;

namespace ProgressiveUpdates;

internal enum WorkPlanStatus
{
    Pending,
    InProgress,
    Done,
    Failed,
    Cancelled,
}

internal sealed record WorkPlanStep(string Title, WorkPlanStatus Status)
{
    public WorkPlanStatus Status { get; set; } = Status;
}

internal static class WorkPlanCard
{
    public const int MaxSteps = 8;
    public const int MaxTitleLength = 60;

    private const string MarkerColumnWidth = "20px";
    private const string TasksContainerId = "tasks-container";
    private const string ChevronDownId = "chevron-ChevronDown";
    private const string ChevronRightId = "chevron-ChevronRight";

    public static MessageActivity CreateMessage(
        IReadOnlyList<WorkPlanStep> steps,
        string? title = null) =>
        new MessageActivity().AddAttachment(
            TeamsAttachment.CreateBuilder()
                .WithAdaptiveCard(Build(steps, title))
                .Build());

    public static AdaptiveCard Build(
        IReadOnlyList<WorkPlanStep> steps,
        string? title = null)
    {
        ArgumentNullException.ThrowIfNull(steps);

        if (steps.Count is < 1 or > MaxSteps)
        {
            throw new ArgumentOutOfRangeException(
                nameof(steps),
                $"A work plan must contain between 1 and {MaxSteps} steps.");
        }

        string planTitle = string.IsNullOrWhiteSpace(title) ? "Task plan" : title.Trim();
        if (planTitle.Length > MaxTitleLength)
        {
            throw new ArgumentException(
                $"The plan title cannot exceed {MaxTitleLength} characters.",
                nameof(title));
        }

        int completed = steps.Count(step => step.Status == WorkPlanStatus.Done);
        bool hasFailure = steps.Any(step => step.Status == WorkPlanStatus.Failed);
        bool expanded = completed != steps.Count || hasFailure;

        ColumnSet header = new ColumnSet()
            .WithColumns(
                CreateChevron("ChevronDown", ChevronDownId, expanded),
                CreateChevron("ChevronRight", ChevronRightId, !expanded),
                new Column()
                    .WithWidth(Width("stretch"))
                    .WithItems(
                        new TextBlock()
                            .WithText($"{planTitle} — {completed}/{steps.Count} done")
                            .WithWeight(TextWeight.Bolder)
                            .WithWrap(true)))
            .WithSelectAction(
                new ToggleVisibilityAction().WithTargetElements(
                    new Union<IList<string>, IList<TargetElement>>(
                        new List<string>
                        {
                            TasksContainerId,
                            ChevronDownId,
                            ChevronRightId,
                        })));

        Container stepList = new Container()
            .WithId(TasksContainerId)
            .WithIsVisible(expanded)
            .WithItems(steps.Select(CreateStepRow).Cast<CardElement>().ToList());

        return new AdaptiveCard().WithBody(header, stepList);
    }

    private static ColumnSet CreateStepRow(WorkPlanStep step)
    {
        if (string.IsNullOrWhiteSpace(step.Title))
        {
            throw new ArgumentException("Work-plan step titles cannot be blank.", nameof(step));
        }

        return new ColumnSet().WithColumns(
            new Column()
                .WithWidth(Width(MarkerColumnWidth))
                .WithVerticalContentAlignment(VerticalAlignment.Center)
                .WithItems(CreateStepMarker(step.Status)),
            new Column()
                .WithWidth(Width("stretch"))
                .WithItems(
                    new TextBlock()
                        .WithText(step.Title)
                        .WithWrap(true)
                        .WithIsSubtle(step.Status == WorkPlanStatus.Pending)));
    }

    private static CardElement CreateStepMarker(WorkPlanStatus status) =>
        status switch
        {
            WorkPlanStatus.InProgress =>
                new ProgressRing().WithSize(ProgressRingSize.Tiny),
            WorkPlanStatus.Done =>
                new Icon()
                    .WithName("CheckmarkCircle")
                    .WithSize(IconSize.XSmall)
                    .WithStyle(IconStyle.Filled)
                    .WithColor(TextColor.Good),
            WorkPlanStatus.Failed =>
                new Icon()
                    .WithName("ErrorCircle")
                    .WithSize(IconSize.XSmall)
                    .WithStyle(IconStyle.Filled)
                    .WithColor(TextColor.Attention),
            WorkPlanStatus.Cancelled =>
                new Icon()
                    .WithName("DismissCircle")
                    .WithSize(IconSize.XSmall)
                    .WithStyle(IconStyle.Regular)
                    .WithColor(TextColor.Warning),
            _ =>
                new Icon()
                    .WithName("Circle")
                    .WithSize(IconSize.XSmall)
                    .WithStyle(IconStyle.Regular)
                    .WithColor(TextColor.Default),
        };

    private static Column CreateChevron(string name, string id, bool isVisible) =>
        new Column()
            .WithId(id)
            .WithWidth(Width(MarkerColumnWidth))
            .WithVerticalContentAlignment(VerticalAlignment.Center)
            .WithIsVisible(isVisible)
            .WithItems(new Icon().WithName(name).WithSize(IconSize.XSmall));

    private static IUnion<string, float> Width(string value) =>
        new Union<string, float>(value);
}
