using Exsensic.Contracts.Enums;

namespace Exsensic.Core.Entities;

/// Central booking record. Split into two files so Dean owns the shape and Daniel owns the state machine.
public partial class Booking
{
    public int Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public Guid ClientUserId { get; set; }
    public int ServiceId { get; set; }
    public int TimeSlotId { get; set; }
    public Guid? StaffUserId { get; set; }
    public BookingStatus Status { get; set; }
    public string? CancellationReason { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // No ApplicationUser nav. Core doesn't reference Data.
    public Service Service { get; set; } = null!;
    public TimeSlot TimeSlot { get; set; } = null!;
    public ICollection<BookingRequirement> Requirements { get; set; } = new List<BookingRequirement>();
    public ICollection<BookingStatusHistory> StatusHistory { get; set; } = new List<BookingStatusHistory>();
}
