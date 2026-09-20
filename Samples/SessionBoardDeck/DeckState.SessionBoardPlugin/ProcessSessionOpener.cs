using System.Diagnostics;
using DeckState.StreamDeck;

namespace DeckState.Samples.SessionBoardDeck;

/// <summary>Hardware opener: hands the URL to the OS default browser.</summary>
public sealed class ProcessSessionOpener : ISessionOpener
{
    public ValueTask OpenAsync(string url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url)) { DeckLog.Write("open", "skipped: no url"); return ValueTask.CompletedTask; }

        // The plugin runs at the Stream Deck app's (elevated / uiAccess) integrity, where a direct
        // ShellExecute of a URL fails silently. Hand off via explorer.exe so the URL opens in the
        // user's normal-integrity default browser, and never let a launch failure bubble up.
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{url}\"") { UseShellExecute = true });
            DeckLog.Write("open", $"explorer {url}");
        }
        catch (Exception ex1)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                DeckLog.Write("open", $"shell {url}");
            }
            catch (Exception ex2) { DeckLog.Write("error", $"open failed {url}: {ex1.Message} / {ex2.Message}"); }
        }
        return ValueTask.CompletedTask;
    }
}
