using DeckState.StreamDeck;

namespace DeckState.Samples.SessionBoardDeck;

public static partial class SessionBoardPlugin
{
    private const int HardwareColumns = 4;

    /// <summary>
    /// Hardware entrypoint (the delegate the Stream Deck host runs): wires the real Battlebox HTTP
    /// source and the OS URL opener, then hands off to the shared loop. This file is compiled only
    /// for the device plugin — the WASM emulator swaps in its own source and opener instead.
    /// </summary>
    public static async Task RunAsync(StreamDeckConnection connection, CancellationToken cancellationToken)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var endpoint = new Uri(Environment.GetEnvironmentVariable("DECKSTATE_BATTLEBOX_URL") ?? "http://127.0.0.1:8787/api/sessions");

        // Cloudflare Access service token (machine-to-machine). When both are set,
        // send them on every request so a CF Access-protected endpoint authorizes the poll.
        var cfClientId = Environment.GetEnvironmentVariable("DECKSTATE_BATTLEBOX_CF_CLIENT_ID");
        var cfClientSecret = Environment.GetEnvironmentVariable("DECKSTATE_BATTLEBOX_CF_CLIENT_SECRET");
        if (!string.IsNullOrEmpty(cfClientId) && !string.IsNullOrEmpty(cfClientSecret))
        {
            http.DefaultRequestHeaders.Add("CF-Access-Client-Id", cfClientId);
            http.DefaultRequestHeaders.Add("CF-Access-Client-Secret", cfClientSecret);
        }

        var source = new BattleboxSessionBoardClient(http, endpoint);
        await RunAsync(connection, source, new ProcessSessionOpener(), HardwareColumns, cancellationToken);
    }
}
