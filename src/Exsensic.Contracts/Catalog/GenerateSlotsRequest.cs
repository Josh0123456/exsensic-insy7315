using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Exsensic.Contracts.Catalog;

public sealed record GenerateSlotsRequest(
    DateOnly FromDate,
    DateOnly ToDate,
    DayOfWeek[] Weekdays,
    TimeOnly[] StartTimes,
    [Range(15, 480)] int DurationMinutes);
