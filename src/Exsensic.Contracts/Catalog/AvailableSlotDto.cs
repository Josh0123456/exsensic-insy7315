using System;
using System.Collections.Generic;
using System.Text;

namespace Exsensic.Contracts.Catalog;

public sealed record AvailableSlotDto(
    int TimeSlotId,
    DateOnly SlotDate,
    TimeOnly StartTime,
    TimeOnly EndTime);
