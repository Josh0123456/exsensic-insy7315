using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Exsensic.E2ETests;

/// <summary>The three roles from docs/CONTRACTS.md §2; each has a demo account on staging.</summary>
public enum Role
{
    /// <summary>A business booking services.</summary>
    Client,

    /// <summary>An Exsensic employee assigned to bookings.</summary>
    Staff,

    /// <summary>Exsensic management.</summary>
    Admin,
}

/// <summary>
/// Shared set-up for the browser tests: starts Chromium, opens one browser session per signed-in
/// user, records a Playwright trace (screenshots and DOM snapshots) for every session, and signs in
/// through the real login screen. Site address and accounts come from environment variables, so no
/// URL or password is ever stored in the repository.
/// </summary>
public abstract class E2ETestBase : IAsyncLifetime
{
    private readonly List<(IBrowserContext Context, string Name)> _sessions = [];
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    /// <summary>The deployed site under test, for example the staging web app.</summary>
    protected static string BaseUrl => Required("E2E_BASE_URL").TrimEnd('/');

    /// <summary>
    /// Staging runs on a small shared B1 plan and is tested minutes after a deploy restarts it, so the
    /// first requests can be slow. These limits allow for that without hiding real failures.
    /// </summary>
    private const float ActionTimeoutMs = 60_000;

    /// <summary>Longer than the Web app's 75-second API timeout, so a cold first sign-in still completes.</summary>
    private const float SignInTimeoutMs = 90_000;

    /// <summary>Starts Playwright and a headless Chromium browser before each test.</summary>
    public async Task InitializeAsync()
    {
        Assertions.SetDefaultExpectTimeout(30_000);
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
    }

    /// <summary>Saves each session's trace to TestResults/playwright, then closes the browser.</summary>
    public async Task DisposeAsync()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "TestResults", "playwright");
        Directory.CreateDirectory(folder);
        foreach (var (context, name) in _sessions)
        {
            await context.Tracing.StopAsync(new() { Path = Path.Combine(folder, name + ".zip") });
            await context.CloseAsync();
        }

        if (_browser is not null) await _browser.DisposeAsync();
        _playwright?.Dispose();
    }

    /// <summary>
    /// Opens a fresh, signed-out browser session (its own cookies) with tracing on.
    /// The trace is named after the calling test plus a label, for example "Journey_ClientBooks_AdminConfirms-admin".
    /// </summary>
    protected async Task<IPage> NewSessionAsync(string label = "main", [CallerMemberName] string test = "")
    {
        var context = await _browser!.NewContextAsync(new() { BaseURL = BaseUrl });
        context.SetDefaultTimeout(ActionTimeoutMs);
        context.SetDefaultNavigationTimeout(ActionTimeoutMs);
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = false });
        _sessions.Add((context, $"{test}-{label}"));
        return await context.NewPageAsync();
    }

    /// <summary>
    /// The API allows 5 sign-ins per minute per visitor IP, and every test runs from the same runner IP.
    /// Spacing sign-ins at least 13 seconds apart keeps the whole run under that limit (tests run one at a time).
    /// </summary>
    private static readonly TimeSpan SignInSpacing = TimeSpan.FromSeconds(13);
    private static DateTime _lastSignIn = DateTime.MinValue;

    /// <summary>Opens a new session and signs in as the demo account for the role.</summary>
    protected async Task<IPage> SignedInAsync(Role role, [CallerMemberName] string test = "")
    {
        var wait = _lastSignIn + SignInSpacing - DateTime.UtcNow;
        if (wait > TimeSpan.Zero) await Task.Delay(wait);
        _lastSignIn = DateTime.UtcNow;

        var page = await NewSessionAsync(role.ToString().ToLowerInvariant(), test);
        var prefix = "E2E_" + role.ToString().ToUpperInvariant();

        await page.GotoAsync("/Account/Login");
        await page.GetByLabel("Email address").FillAsync(Required(prefix + "_EMAIL"));
        await page.GetByLabel("Password").FillAsync(Required(prefix + "_PASSWORD"));
        // Wait only until the server answers with the next page, not until every script and stylesheet has
        // loaded; the following steps wait for exactly what they need. The first sign-in after a deploy is the
        // API's first password check and database call, so it may take up to the Web app's 75-second API timeout.
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync(new() { Timeout = SignInTimeoutMs });
        await page.WaitForURLAsync(url => !url.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase),
            new() { WaitUntil = WaitUntilState.Commit, Timeout = SignInTimeoutMs });
        return page;
    }

    /// <summary>
    /// Books the first free slot of the first service (or the first whose card contains <paramref name="serviceName"/>)
    /// and fills every requirement field (text fields get <paramref name="text"/>).
    /// Returns the booking id taken from the confirmation page URL.
    /// </summary>
    protected static async Task<string> BookFirstFreeSlotAsync(IPage client, string? serviceName, string text)
    {
        await client.GotoAsync("/Services");
        await client.Locator("article.service-card", new() { HasText = serviceName })
            .First.GetByRole(AriaRole.Link, new() { NameRegex = new Regex("^Book ") }).ClickAsync();

        // Step 1: the first selectable time on the selected date. Every run books real slots on staging, so
        // when the shown fortnight is full, move on with "Show later dates" (the seed covers about six weeks).
        var freeSlot = client.Locator(".slot-panel:not([hidden]) label.slot-choice:has(input:not([disabled]))").First;
        for (var fortnight = 0; fortnight < 3 && !await freeSlot.IsVisibleAsync(); fortnight++)
        {
            await client.GetByRole(AriaRole.Link, new() { Name = "Show later dates" }).ClickAsync();
            await client.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        }

        await freeSlot.ClickAsync();
        await client.GetByRole(AriaRole.Button, new() { Name = "Continue to requirements" }).ClickAsync();

        // Step 2: fill the requirement form whatever the service category's template contains.
        var form = client.Locator("form:has(button:text('Submit booking request'))");
        foreach (var select in await form.Locator("select").AllAsync())
        {
            await select.SelectOptionAsync(new SelectOptionValue { Index = 1 });
        }

        foreach (var textArea in await form.Locator("textarea").AllAsync())
        {
            await textArea.FillAsync(text);
        }

        foreach (var input in await form.Locator("input:not([type=hidden])").AllAsync())
        {
            var type = await input.GetAttributeAsync("type");
            if (type == "checkbox") await input.CheckAsync();
            else if (type == "number") await input.FillAsync(await input.GetAttributeAsync("min") ?? "1");
            else if (type == "url") await input.FillAsync("https://example.com");
            else await input.FillAsync(text);
        }

        await client.GetByRole(AriaRole.Button, new() { Name = "Submit booking request" }).ClickAsync();
        await client.WaitForURLAsync(new Regex("/Bookings/[^/]+/Confirmation"));
        return Regex.Match(client.Url, "/Bookings/([^/]+)/Confirmation").Groups[1].Value;
    }

    /// <summary>
    /// Clicks a button that opens Christopher's accessible confirmation dialog, then the dialog's
    /// confirm button with the same name.
    /// </summary>
    protected static async Task ClickAndConfirmAsync(IPage page, string buttonName)
    {
        // The dialog is opened by site.js; clicking before it has loaded would submit the form directly.
        await page.WaitForLoadStateAsync(LoadState.Load);
        await page.GetByRole(AriaRole.Button, new() { Name = buttonName }).First.ClickAsync();
        await page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = buttonName }).ClickAsync();
    }

    /// <summary>The status word shown by the status badge on a booking page.</summary>
    /// <remarks>Only inside the page's main content: the navigation's notification counter uses the same style.</remarks>
    protected static ILocator StatusBadge(IPage page) => page.Locator("main .status-badge").First;

    private static string Required(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Set the {name} environment variable to run the end-to-end tests.");
}
