using System.Diagnostics;

namespace DeckState.Samples.SessionBoardDeck;

/// <summary>Hardware opener: hands the URL to the OS default browser.</summary>
public sealed class ProcessSessionOpener : ISessionOpener
{
    public ValueTask OpenAsync(string url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url)) return ValueTask.CompletedTask;

        // The plugin runs at the Stream Deck app's (elevated / uiAccess) integrity, where a direct
        // ShellExecute of a URL fails silently. Hand off via explorer.exe so the URL opens in the
        // user's normal-integrity default browser, and never let a launch failure bubble up.
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{url}\"") { UseShellExecute = true }); }
        catch
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { /* nothing we can do from here */ }
        }
        return ValueTask.CompletedTask;
    }
}
