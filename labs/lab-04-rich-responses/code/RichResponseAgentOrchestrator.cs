using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Teams.Apps.Schema;

namespace RichResponse;

internal sealed class RichResponseAgentOrchestrator : IAgentOrchestrator
{
    public async IAsyncEnumerable<IAgentEvent> GetUpdatesAsync(
        MessageActivity activity,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string command = activity.Text?.Trim() ?? string.Empty;

        if (command.Equals("card", StringComparison.OrdinalIgnoreCase))
        {
            yield return new InformativeAgentEvent("Preparing an Adaptive Card");
            yield return new AdaptiveCardAgentEvent(CreateAdaptiveCard());
            yield break;
        }

        if (command.Equals("markdown", StringComparison.OrdinalIgnoreCase))
        {
            yield return new InformativeAgentEvent("Streaming Markdown");

            const string markdown =
                "# 🚀 Project Update\n\n" +
                "> **Rich Response Lab** is ready to test! 🎉\n\n" +
                "## 📊 Status\n\n" +
                "| Feature | Status | Notes |\n" +
                "|---|:---:|---|\n" +
                "| 🃏 Adaptive Cards | ✅ Ready | Structured content |\n" +
                "| 📝 Markdown | ✅ Ready | Stream-friendly |\n" +
                "| ⚡ Rich Responses | 🟢 Live | Ready for testing |\n\n" +
                "## ✨ What's included\n\n" +
                "- 🧩 **Adaptive Cards** — structured, interactive content.\n" +
                "- 📖 **Markdown** — readable content while streaming.\n" +
                "- 🔄 **Live updates** — designed for a smooth response experience.\n\n" +
                "### 🧪 Try it out\n\n" +
                "Run the next test with:\n\n" +
                "```text\n" +
                "card\n" +
                "```\n\n" +
                "> 💡 **Tip:** Compare the `card` response with the streamed Markdown response.\n\n" +
                "---\n\n" +
                "**Status:** 🟢 Ready for testing  |  **Next:** 👉 `card`";

            foreach (string chunk in markdown.Split(' '))
            {
                yield return new TextAgentEvent($"{chunk} ");
            }

            yield break;
        }

        yield return new TextAgentEvent("Say `card` or `markdown`.");
    }

    private static TeamsAttachment CreateAdaptiveCard()
    {
                JsonElement card = JsonSerializer.Deserialize<JsonElement>(
                        """
                        {
                            "type": "AdaptiveCard",
                            "version": "1.5",
                            "body": [
                                {
                                    "type": "TextBlock",
                                    "text": "Rich response sample",
                                    "size": "Large",
                                    "weight": "Bolder",
                                    "wrap": true
                                },
                                {
                                    "type": "TextBlock",
                                    "text": "This hardcoded Adaptive Card was returned by the lab 04 agent.",
                                    "wrap": true
                                },
                                {
                                    "type": "FactSet",
                                    "facts": [
                                        { "title": "Response", "value": "Adaptive Card" },
                                        { "title": "Status", "value": "Ready" }
                                    ]
                                }
                            ]
                        }
                        """);

        return TeamsAttachment.CreateBuilder()
            .WithAdaptiveCard(card)
            .Build();
    }
}