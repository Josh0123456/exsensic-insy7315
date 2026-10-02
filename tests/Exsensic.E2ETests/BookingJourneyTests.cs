using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Exsensic.E2ETests;

/// <summary>
/// The booking life cycle across all three roles, run in separate browser sessions exactly as the
/// live demo does it: client books, admin confirms with staff, staff completes.
/// </summary>
public class BookingJourneyTests : E2ETestBase
{
    /// <summary>The client books the first free slot (Requested); the admin confirms it with a qualified staff member; the client then sees Confirmed.</summary>
    [Fact]
    public async Task Journey_ClientBooks_AdminConfirms()
    {
        var client = await SignedInAsync(Role.Client);
        var bookingId = await BookFirstFreeSlotAsync(client, serviceName: null, text: "End-to-end test booking.");
        await Assertions.Expect(StatusBadge(client)).ToHaveTextAsync("Requested");

        var admin = await SignedInAsync(Role.Admin);
        await admin.GotoAsync($"/Admin/Bookings/{bookingId}");
        await admin.GetByLabel("Assign staff").SelectOptionAsync(new SelectOptionValue { Index = 1 });
        await ClickAndConfirmAsync(admin, "Confirm booking");
        await Assertions.Expect(StatusBadge(admin)).ToHaveTextAsync("Confirmed");

        await client.GotoAsync($"/Bookings/{bookingId}");
        await Assertions.Expect(StatusBadge(client)).ToHaveTextAsync("Confirmed");
    }

    /// <summary>
    /// The staff member completes the confirmed booking whose slot started earlier today.
    /// Dean's seeder guarantees that booking exists on staging for the demo photographer.
    /// </summary>
    [Fact]
    public async Task StaffCompletes_SeededPastBooking()
    {
        var staff = await SignedInAsync(Role.Staff);

        var today = staff.Locator("section.schedule-day", new() { Has = staff.Locator(".today-label") });
        await today.Locator("article", new() { HasText = "Confirmed" }).First
            .GetByRole(AriaRole.Link, new() { NameRegex = new Regex("^View booking") }).ClickAsync();

        await ClickAndConfirmAsync(staff, "Mark as completed");
        await Assertions.Expect(StatusBadge(staff)).ToHaveTextAsync("Completed");
    }

    /// <summary>
    /// Dean's stored-XSS scenario: a script typed into a requirement is shown as plain text on the
    /// staff detail page, and no browser dialog ever opens.
    /// </summary>
    [Fact]
    public async Task StoredScript_InRequirements_IsShownAsText()
    {
        const string Script = "<script>alert(1)</script>";

        // Photoshoot, so the demo photographer (the E2E staff account) is the qualified staff member.
        var client = await SignedInAsync(Role.Client);
        var bookingId = await BookFirstFreeSlotAsync(client, serviceName: "Photoshoot", text: Script);

        var admin = await SignedInAsync(Role.Admin);
        await admin.GotoAsync($"/Admin/Bookings/{bookingId}");
        await admin.GetByLabel("Assign staff").SelectOptionAsync(new SelectOptionValue { Index = 1 });
        await ClickAndConfirmAsync(admin, "Confirm booking");

        var staff = await SignedInAsync(Role.Staff);
        var dialogOpened = false;
        staff.Dialog += (_, dialog) => { dialogOpened = true; _ = dialog.DismissAsync(); };

        await staff.GotoAsync($"/Staff/Bookings/{bookingId}");
        await Assertions.Expect(staff.GetByText(Script).First).ToBeVisibleAsync();
        Assert.False(dialogOpened, "A script from the requirements ran in the browser.");
    }
}
