using Exsensic.Contracts.Bookings;
using Exsensic.Contracts.Enums;
using Exsensic.Core.Bookings;
using Exsensic.Core.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Exsensic.IntegrationTests.Bookings;

/// <summary>
/// Booking creation against a real SQL Server database: what gets saved on success, and that every
/// slot rule and validation failure is rejected without saving anything.
/// </summary>
[Collection(BookingDatabaseCollection.Name)]
public sealed class BookingCreationTests(BookingDatabaseFixture fixture)
{
    /// <summary>Valid Photoshoot answers, with notes left blank to check blanks aren't stored.</summary>
    private static Dictionary<string, string> ValidAnswers() => new()
    {
        ["shootType"] = "Product",
        ["location"] = "Studio",
        ["quantity"] = "25",
        ["description"] = "  White-background shots of our new range.  ",
        ["notes"] = "",
    };

    /// <summary>
    /// A valid request saves a Requested booking with its requirements, one history row and the client's
    /// notification, all in one go.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ValidRequest_SavesBookingHistoryNotificationAndRequirements()
    {
        var clientId = Guid.NewGuid();
        var serviceId = await fixture.AddServiceAsync();
        var slotId = await fixture.AddSlotAsync();

        var created = await CreateAsync(new CreateBookingRequest(serviceId, slotId, ValidAnswers()), clientId);

        Assert.Equal(BookingStatus.Requested, created.Status);
        Assert.StartsWith(BookingReferenceGenerator.Prefix, created.Reference);

        await using var db = fixture.NewContext();
        var booking = await db.Bookings
            .Include(b => b.Requirements)
            .Include(b => b.StatusHistory)
            .SingleAsync(b => b.Id == created.Id);

        Assert.Equal(clientId, booking.ClientUserId);
        Assert.Equal(slotId, booking.TimeSlotId);
        Assert.Null(booking.StaffUserId);
        Assert.Equal(BookingDatabaseFixture.NowUtc, booking.CreatedAtUtc);

        Assert.Equal(4, booking.Requirements.Count);
        Assert.Equal("White-background shots of our new range.", booking.Requirements.Single(r => r.FieldKey == "description").FieldValue);
        Assert.DoesNotContain(booking.Requirements, r => r.FieldKey == "notes");

        var history = Assert.Single(booking.StatusHistory);
        Assert.Null(history.FromStatus);
        Assert.Equal(BookingStatus.Requested, history.ToStatus);
        Assert.Equal(clientId, history.ChangedByUserId);

        var notification = Assert.Single(await db.Notifications.Where(n => n.BookingId == created.Id).ToListAsync());
        Assert.Equal(clientId, notification.UserId);
        Assert.Equal(NotificationType.BookingRequested, notification.Type);
    }

    /// <summary>Invalid answers give field errors keyed like the Web form, and nothing is saved.</summary>
    [Fact]
    public async Task CreateAsync_InvalidRequirements_ThrowsValidationAndSavesNothing()
    {
        var serviceId = await fixture.AddServiceAsync();
        var slotId = await fixture.AddSlotAsync();
        var answers = ValidAnswers();
        answers["quantity"] = "9999";
        answers["budget"] = "lots";

        var ex = await Assert.ThrowsAsync<RequestValidationException>(
            () => CreateAsync(new CreateBookingRequest(serviceId, slotId, answers), Guid.NewGuid()));

        Assert.Contains("Requirements[quantity]", ex.Errors.Keys);
        Assert.Contains("Requirements[budget]", ex.Errors.Keys);
        await AssertNoBookingForSlotAsync(slotId);
    }

    /// <summary>A missing or archived service is 404, never a booking.</summary>
    [Fact]
    public async Task CreateAsync_ArchivedService_ThrowsNotFound()
    {
        var serviceId = await fixture.AddServiceAsync(isActive: false);
        var slotId = await fixture.AddSlotAsync();

        await Assert.ThrowsAsync<NotFoundException>(
            () => CreateAsync(new CreateBookingRequest(serviceId, slotId, ValidAnswers()), Guid.NewGuid()));

        await AssertNoBookingForSlotAsync(slotId);
    }

    /// <summary>Every slot rule from docs/CONTRACTS.md §4 is enforced: blocked, started and too short.</summary>
    [Theory]
    [InlineData("blocked")]
    [InlineData("past")]
    [InlineData("too short")]
    public async Task CreateAsync_UnbookableSlot_ThrowsSlotUnavailable(string problem)
    {
        var serviceId = await fixture.AddServiceAsync(durationMinutes: 120);
        var slotId = problem switch
        {
            "blocked" => await fixture.AddSlotAsync(isBlocked: true),
            "past" => await fixture.AddSlotAsync(daysFromNow: -1 - Random.Shared.Next(1, 300)),
            _ => await fixture.AddSlotAsync(lengthMinutes: 60),
        };

        await Assert.ThrowsAsync<SlotUnavailableException>(
            () => CreateAsync(new CreateBookingRequest(serviceId, slotId, ValidAnswers()), Guid.NewGuid()));

        await AssertNoBookingForSlotAsync(slotId);
    }

    /// <summary>A slot that doesn't exist is reported as unavailable.</summary>
    [Fact]
    public async Task CreateAsync_MissingSlot_ThrowsSlotUnavailable()
    {
        var serviceId = await fixture.AddServiceAsync();

        await Assert.ThrowsAsync<SlotUnavailableException>(
            () => CreateAsync(new CreateBookingRequest(serviceId, int.MaxValue, ValidAnswers()), Guid.NewGuid()));
    }

    /// <summary>A second client can't book a slot that already has a Requested booking.</summary>
    [Fact]
    public async Task CreateAsync_SlotAlreadyBooked_ThrowsSlotUnavailable()
    {
        var serviceId = await fixture.AddServiceAsync();
        var slotId = await fixture.AddSlotAsync();
        await CreateAsync(new CreateBookingRequest(serviceId, slotId, ValidAnswers()), Guid.NewGuid());

        await Assert.ThrowsAsync<SlotUnavailableException>(
            () => CreateAsync(new CreateBookingRequest(serviceId, slotId, ValidAnswers()), Guid.NewGuid()));

        await using var db = fixture.NewContext();
        Assert.Equal(1, await db.Bookings.CountAsync(b => b.TimeSlotId == slotId));
    }

    /// <summary>Once a booking is cancelled its slot is free again, because only active bookings hold a slot.</summary>
    [Fact]
    public async Task CreateAsync_SlotFreedByCancellation_CanBeBookedAgain()
    {
        var serviceId = await fixture.AddServiceAsync();
        var slotId = await fixture.AddSlotAsync();
        var first = await CreateAsync(new CreateBookingRequest(serviceId, slotId, ValidAnswers()), Guid.NewGuid());

        await using (var db = fixture.NewContext())
        {
            var booking = await db.Bookings.SingleAsync(b => b.Id == first.Id);
            booking.Cancel("Plans changed.", booking.ClientUserId, BookingDatabaseFixture.NowUtc);
            await db.SaveChangesAsync();
        }

        var second = await CreateAsync(new CreateBookingRequest(serviceId, slotId, ValidAnswers()), Guid.NewGuid());

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(BookingStatus.Requested, second.Status);
    }

    /// <summary>Runs CreateAsync in its own scope, the way one API request would.</summary>
    private async Task<BookingCreatedDto> CreateAsync(CreateBookingRequest request, Guid clientId)
    {
        await using var scope = fixture.NewScope();
        var service = scope.ServiceProvider.GetRequiredService<BookingService>();
        return await service.CreateAsync(request, clientId, CancellationToken.None);
    }

    private async Task AssertNoBookingForSlotAsync(int slotId)
    {
        await using var db = fixture.NewContext();
        Assert.False(await db.Bookings.AnyAsync(b => b.TimeSlotId == slotId));
    }
}
