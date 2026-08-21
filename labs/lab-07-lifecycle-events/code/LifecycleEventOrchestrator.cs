namespace LifecycleEvents;

internal sealed class LifecycleEventOrchestrator
{
    internal string GetInstalledResponse() =>
        "Hello! I am installed and ready to help.";

    internal string GetMembersAddedResponse(int memberCount) =>
        memberCount == 1
            ? "Welcome! A new member joined this conversation."
            : $"Welcome! {memberCount} new members joined this conversation.";

    internal string GetMembersRemovedResponse(int memberCount) =>
        memberCount == 1
            ? "A member left this conversation."
            : $"{memberCount} members left this conversation.";
}