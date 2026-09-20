using DeckState.StreamDeck;
using Microsoft.JSInterop;

namespace DeckState.WasmEmulator.Emulator;

/// <summary>One action a project exposes to the deck — a palette entry you can place on a key.</summary>
public sealed record EmulatorAction(string ActionId, string ShortLabel, string Description);

/// <summary>
/// A runnable DeckState project, described so the emulator can host it generically: the actions it
/// offers, how to start its real plugin loop over the in-process transport, and a sensible default
/// layout for a given device. Adding a project here makes it selectable in the dropdown.
/// </summary>
public sealed class EmulatorProject
{
    public required string Name { get; init; }

    /// <summary>The actions that can be placed on keys, in palette order.</summary>
    public required IReadOnlyList<EmulatorAction> Actions { get; init; }

    /// <summary>
    /// Builds the plugin's run delegate for the host, given the JS runtime (for host seams like the
    /// URL opener) and the selected device's column count (so coordinate→slot math matches).
    /// </summary>
    public required Func<IJSRuntime, int, Func<StreamDeckConnection, CancellationToken, Task>> CreateRun { get; init; }

    /// <summary>The slot → action-id assignment this project loads with on a fresh device.</summary>
    public required Func<DeckDevice, IReadOnlyDictionary<int, string>> DefaultLayout { get; init; }

    public EmulatorAction? ActionById(string? actionId) =>
        actionId is null ? null : Actions.FirstOrDefault(a => a.ActionId == actionId);
}
