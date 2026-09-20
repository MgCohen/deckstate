using System.Diagnostics;

namespace DeckState.Samples.SessionBoardDeck;

/// <summary>Hardware opener: hands the URL to the OS default browser.</summary>
public sealed class ProcessSessionOpener : ISessionOpener
{
    public ValueTask OpenAsync(string url, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(url))
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        return ValueTask.CompletedTask;
    }
}
