using System.Diagnostics;
using Microsoft.Playwright;
using Xunit;

namespace DeckState.WasmEmulator.E2E;

/// <summary>
/// Shared across the smoke suite: boots the emulator's real dev server once, launches the
/// pre-installed Chromium (the box ships chromium-1194 + ffmpeg-1011; we point ExecutablePath at
/// it rather than downloading), and hands each test an isolated context that records video + trace.
/// </summary>
public sealed class EmulatorFixture : IAsyncLifetime
{
    // The box's pre-installed Chromium. Its build (1194) differs from the driver's expected 1187,
    // so we launch it by path instead of the version-matched lookup. ffmpeg (1011) matches, so
    // video recording works with nothing to install.
    private const string ChromiumPath = "/opt/pw-browsers/chromium";

    private Process? _server;
    private IPlaywright? _playwright;

    public IBrowser Browser { get; private set; } = null!;
    public string BaseUrl { get; } = "http://127.0.0.1:5099";
    public string VideosDir { get; private set; } = "";
    public string TracesDir { get; private set; } = "";

    public async Task InitializeAsync()
    {
        var repoRoot = FindRepoRoot();
        VideosDir = Path.Combine(repoRoot, "artifacts", "e2e", "videos");
        TracesDir = Path.Combine(repoRoot, "artifacts", "e2e", "traces");
        Directory.CreateDirectory(VideosDir);
        Directory.CreateDirectory(TracesDir);

        var csproj = Path.Combine(repoRoot, "playground", "DeckState.WasmEmulator", "DeckState.WasmEmulator.csproj");
        _server = StartServer(csproj);
        await WaitForServerAsync();

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            ExecutablePath = ChromiumPath,
        });
    }

    /// <summary>A fresh context + page for one test, recording video into its own folder, tracing on.</summary>
    public async Task<(IBrowserContext Context, IPage Page)> NewPageAsync(string testName)
    {
        var context = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1000, Height = 820 },
            RecordVideoDir = Path.Combine(VideosDir, testName),
        });
        await context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true, Sources = true });
        var page = await context.NewPageAsync();
        return (context, page);
    }

    /// <summary>Flush trace + video for a finished test.</summary>
    public async Task FinishAsync(IBrowserContext context, string testName)
    {
        await context.Tracing.StopAsync(new TracingStopOptions { Path = Path.Combine(TracesDir, $"{testName}.zip") });
        await context.CloseAsync(); // finalizes the .webm video
    }

    private Process StartServer(string csproj)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in new[] { "run", "--project", csproj, "-c", "Release", "--urls", BaseUrl })
            psi.ArgumentList.Add(arg);
        return Process.Start(psi) ?? throw new InvalidOperationException("Failed to start the emulator dev server.");
    }

    private async Task WaitForServerAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        for (var attempt = 0; attempt < 150; attempt++)
        {
            try
            {
                var response = await http.GetAsync(BaseUrl);
                if (response.IsSuccessStatusCode) return;
            }
            catch { /* server not accepting connections yet */ }
            await Task.Delay(1000);
        }
        throw new InvalidOperationException("Emulator dev server did not become ready in time.");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DeckState.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate repo root (DeckState.slnx).");
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null) await Browser.CloseAsync();
        _playwright?.Dispose();
        if (_server is { HasExited: false })
        {
            _server.Kill(entireProcessTree: true);
            try { await _server.WaitForExitAsync(); } catch { /* best effort */ }
        }
        _server?.Dispose();
    }
}

[CollectionDefinition("emulator")]
public sealed class EmulatorCollection : ICollectionFixture<EmulatorFixture>;
