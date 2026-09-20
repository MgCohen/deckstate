using System.Text;

namespace DeckState.StreamDeck;

/// <summary>
/// Lightweight append-only diagnostic log shared by the framework and plugins. One tab-separated
/// line per entry: {utc-iso}\t{category}\t{message}.
///
/// Writes to %LOCALAPPDATA%\DeckState\logs\deckstate-yyyyMMdd.log by default (a real per-user path;
/// the Stream Deck app's uiAccess integrity redirects %TEMP%, so that is deliberately avoided).
/// Override the directory with DECKSTATE_LOG_DIR; disable entirely with DECKSTATE_LOG=0.
/// Never throws — logging must never take down a plugin.
/// </summary>
public static class DeckLog
{
    private static readonly object Gate = new();

    private static readonly bool Enabled =
        !string.Equals(Environment.GetEnvironmentVariable("DECKSTATE_LOG"), "0", StringComparison.Ordinal);

    private static readonly string Directory =
        Environment.GetEnvironmentVariable("DECKSTATE_LOG_DIR")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeckState", "logs");

    public static void Write(string category, string message)
    {
        if (!Enabled) return;
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            var file = Path.Combine(Directory, $"deckstate-{DateTime.UtcNow:yyyyMMdd}.log");
            var line = $"{DateTime.UtcNow:O}\t{category}\t{message.ReplaceLineEndings(" ")}\n";
            lock (Gate) File.AppendAllText(file, line, Encoding.UTF8);
        }
        catch { /* diagnostics must never break the plugin */ }
    }
}
