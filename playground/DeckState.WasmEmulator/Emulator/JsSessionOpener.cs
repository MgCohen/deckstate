using DeckState.Samples.SessionBoardDeck;
using Microsoft.JSInterop;

namespace DeckState.WasmEmulator.Emulator;

/// <summary>The emulator's swap-in for the OS URL opener: opens the provider URL in a new tab.</summary>
public sealed class JsSessionOpener(IJSRuntime js) : ISessionOpener
{
    public async ValueTask OpenAsync(string url, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(url))
            await js.InvokeVoidAsync("open", cancellationToken, url, "_blank", "noopener");
    }
}
