using System;
using System.Collections.Generic;
using System.Text;

namespace Exsensic.Contracts.Catalog;

public sealed record TimeSlotDto(
    int Id,
    DateOnly SlotDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool IsBlocked,
    string? BlockReason,
    bool HasActiveBooking,
    string RowVersion);
