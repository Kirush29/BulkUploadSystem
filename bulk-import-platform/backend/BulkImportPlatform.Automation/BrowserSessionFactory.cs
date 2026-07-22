using Microsoft.Playwright;

namespace BulkImportPlatform.Automation;

/// <summary>
/// Launches the Chromium session used to drive irentosoft. Headless by default in production;
/// the headed+video mode (RpaRunOptionsDto.Headed) exists only for diagnosing a specific failing
/// row later - it mirrors IrentoQA's BaseTest.cs setup but is opt-in here, not the default.
/// </summary>
public class BrowserSessionFactory
{
    public class Session : IAsyncDisposable
    {
        public required IPlaywright Playwright { get; init; }
        public required IBrowser Browser { get; init; }
        public required IBrowserContext Context { get; init; }
        public required IPage Page { get; init; }

        public async ValueTask DisposeAsync()
        {
            await Context.CloseAsync();
            await Browser.CloseAsync();
            Playwright.Dispose();
        }
    }

    private readonly string _videoDir;

    public BrowserSessionFactory(string? videoDir = null)
    {
        _videoDir = videoDir ?? Path.Combine(AppContext.BaseDirectory, "RpaEvidence", "Videos");
    }

    /// <summary>
    /// Verifies Playwright's browser binaries are actually installed, with a clear, actionable
    /// error instead of a cryptic native-library crash if they aren't. Call once at startup.
    /// </summary>
    public async Task VerifyBrowserInstalledAsync()
    {
        try
        {
            using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        }
        catch (PlaywrightException ex)
        {
            throw new InvalidOperationException(
                "Playwright's Chromium browser is not installed. From the Api project's build " +
                "output directory, run: pwsh playwright.ps1 install chromium (or ./playwright.ps1 " +
                "on Windows PowerShell). See BulkImportPlatform.Automation's README notes.", ex);
        }
    }

    public async Task<Session> CreateAsync(bool headed = false)
    {
        var playwright = await Microsoft.Playwright.Playwright.CreateAsync();

        var browser = await playwright.Chromium.LaunchAsync(new()
        {
            Headless = !headed
        });

        var contextOptions = new BrowserNewContextOptions { ViewportSize = null };
        if (headed)
        {
            contextOptions.RecordVideoDir = _videoDir;
            contextOptions.RecordVideoSize = new() { Width = 1280, Height = 720 };
        }

        var context = await browser.NewContextAsync(contextOptions);
        var page = await context.NewPageAsync();

        return new Session { Playwright = playwright, Browser = browser, Context = context, Page = page };
    }
}
