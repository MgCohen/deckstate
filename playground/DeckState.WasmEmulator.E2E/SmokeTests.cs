using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace DeckState.WasmEmulator.E2E;

/// <summary>
/// Browser E2E over the real WASM emulator: boots the app, drives the DOM, and asserts on the SVG
/// the real plugin loops render back. Complements the headless ReadyDeck ProtocolHarness.
/// </summary>
[Collection("emulator")]
public sealed class SmokeTests(EmulatorFixture fixture)
{
    private const int BootTimeoutMs = 45_000;

    // Open the app and wait for the WASM runtime to render the deck.
    private async Task<(IBrowserContext, IPage)> OpenAsync(string testName)
    {
        var (context, page) = await fixture.NewPageAsync(testName);
        await page.GotoAsync(fixture.BaseUrl, new PageGotoOptions { Timeout = BootTimeoutMs });
        await page.Locator(".deck-grid .k").First.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = BootTimeoutMs,
        });
        return (context, page);
    }

    [Fact]
    public async Task Boots_with_default_ReadyDeck_on_the_standard_device()
    {
        const string name = nameof(Boots_with_default_ReadyDeck_on_the_standard_device);
        var (context, page) = await OpenAsync(name);
        try
        {
            await Assertions.Expect(page.Locator("#project")).ToHaveValueAsync("ReadyDeck");
            await Assertions.Expect(page.Locator("#device")).ToHaveValueAsync("Stream Deck");
            await Assertions.Expect(page.Locator(".deck-grid .k")).ToHaveCountAsync(15); // 5x3
            // The real ReadyButtonKey renders IDLE for a fresh work key.
            await Assertions.Expect(page.Locator(".deck-grid .k").First).ToContainTextAsync("IDLE");
        }
        finally { await fixture.FinishAsync(context, name); }
    }

    [Fact]
    public async Task Work_key_toggles_idle_to_ready_and_counter_resets_the_deck()
    {
        const string name = nameof(Work_key_toggles_idle_to_ready_and_counter_resets_the_deck);
        var (context, page) = await OpenAsync(name);
        try
        {
            var workKey = page.Locator(".deck-grid .k").First; // slot 0 = Work
            var counterKey = page.Locator(".deck-grid .k").Last; // last slot = Counter

            await Assertions.Expect(workKey).ToContainTextAsync("IDLE");

            await workKey.ClickAsync();
            await Assertions.Expect(workKey).ToContainTextAsync("READY");

            // Pressing the counter runs the real Reset() and re-ticks every key back to IDLE.
            await counterKey.ClickAsync();
            await Assertions.Expect(workKey).ToContainTextAsync("IDLE");
        }
        finally { await fixture.FinishAsync(context, name); }
    }

    [Fact]
    public async Task Switching_device_reshapes_the_grid()
    {
        const string name = nameof(Switching_device_reshapes_the_grid);
        var (context, page) = await OpenAsync(name);
        try
        {
            await page.Locator("#device").SelectOptionAsync("Stream Deck XL");
            await Assertions.Expect(page.Locator(".deck-grid .k")).ToHaveCountAsync(32); // 8x4

            await page.Locator("#device").SelectOptionAsync("Stream Deck Mini");
            await Assertions.Expect(page.Locator(".deck-grid .k")).ToHaveCountAsync(6); // 3x2
        }
        finally { await fixture.FinishAsync(context, name); }
    }

    [Fact]
    public async Task Switching_to_SessionBoard_renders_the_mock_sessions()
    {
        const string name = nameof(Switching_to_SessionBoard_renders_the_mock_sessions);
        var (context, page) = await OpenAsync(name);
        try
        {
            await page.Locator("#project").SelectOptionAsync("SessionBoard");
            // The real SessionBoardKey draws a status label from the injected mock source.
            await Assertions.Expect(page.Locator(".deck-grid"))
                .ToContainTextAsync(new Regex("WORKING|REVIEW|NEEDS YOU|DONE|BLOCKED"));
        }
        finally { await fixture.FinishAsync(context, name); }
    }
}
