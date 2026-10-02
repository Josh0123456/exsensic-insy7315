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

    /// <summary>Starts Playwright and a headless Chromium browser before each test.</summary>
    public async Task InitializeAsync()
    {
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
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = false });
        _sessions.Add((context, $"{test}-{label}"));
        return await context.NewPageAsync();
    }

    /// <summary>Opens a new session and signs in as the demo account for the role.</summary>
    protected async Task<IPage> SignedInAsync(Role role, [CallerMemberName] string test = "")
    {
        var page = await NewSessionAsync(role.ToString().ToLowerInvariant(), test);
        var prefix = "E2E_" + role.ToString().ToUpperInvariant();

        await page.GotoAsync("/Account/Login");
        await page.GetByLabel("Email address").FillAsync(Required(prefix + "_EMAIL"));
        await page.GetByLabel("Password").FillAsync(Required(prefix + "_PASSWORD"));
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();
        await page.WaitForURLAsync(url => !url.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase));
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

        // Step 1: the first selectable time on the selected date.
        await client.Locator(".slot-panel:not([hidden]) label.slot-choice:has(input:not([disabled]))").First.ClickAsync();
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
        await page.GetByRole(AriaRole.Button, new() { Name = buttonName }).First.ClickAsync();
        await page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = buttonName }).ClickAsync();
    }

    /// <summary>The status word shown by the status badge on a booking page.</summary>
    protected static ILocator StatusBadge(IPage page) => page.Locator(".status-badge").First;

    private static string Required(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Set the {name} environment variable to run the end-to-end tests.");
}
