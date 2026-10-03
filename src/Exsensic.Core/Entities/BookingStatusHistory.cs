using System;
using System.Collections.Generic;
using System.Text;

using Exsensic.Contracts.Enums;

namespace Exsensic.Core.Entities;

public class BookingStatusHistory
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public BookingStatus? FromStatus { get; set; }
    public BookingStatus ToStatus { get; set; }
    public Guid ChangedByUserId { get; set; }
    public DateTimeOffset ChangedAtUtc { get; set; }
    public string? Note { get; set; }
}
