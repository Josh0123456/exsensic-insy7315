using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Exsensic.E2ETests;

/// <summary>
/// Quick checks that the deployed site is up and each role lands on its own home page after signing in.
/// These are the tests kept even if the full journeys are cut (Team Plan scope-cut ladder).
/// </summary>
public class SmokeTests : E2ETestBase
{
    /// <summary>A guest can open the home page and reach a service catalogue with at least one bookable service.</summary>
    [Fact]
    public async Task HomePage_Loads_ShowsServices()
    {
        var page = await NewSessionAsync();

        var response = await page.GotoAsync("/");
        Assert.True(response?.Ok, $"Home page returned {response?.Status}.");

        await page.GetByRole(AriaRole.Navigation, new() { Name = "Main navigation" })
            .GetByRole(AriaRole.Link, new() { Name = "Services", Exact = true }).ClickAsync();
        await Assertions.Expect(page.Locator("article.service-card").First).ToBeVisibleAsync();
    }

    /// <summary>A client signs in and lands on the service catalogue.</summary>
    [Fact]
    public async Task Login_AsClient_LandsOnRoleHome()
    {
        var page = await SignedInAsync(Role.Client);
        await Assertions.Expect(page).ToHaveURLAsync(new Regex("/Services/?$"));
    }

    /// <summary>A staff member signs in and lands on their schedule.</summary>
    [Fact]
    public async Task Login_AsStaff_LandsOnRoleHome()
    {
        var page = await SignedInAsync(Role.Staff);
        await Assertions.Expect(page).ToHaveURLAsync(new Regex("/Staff/?$"));
    }

    /// <summary>An admin signs in and lands on the admin dashboard.</summary>
    [Fact]
    public async Task Login_AsAdmin_LandsOnRoleHome()
    {
        var page = await SignedInAsync(Role.Admin);
        await Assertions.Expect(page).ToHaveURLAsync(new Regex("/Admin/?$"));
    }
}
