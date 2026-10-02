using System;
using System.Collections.Generic;
using System.Text;

namespace Exsensic.Core.Entities;

public class TimeSlot
{
    public int Id { get; set; }
    public DateOnly SlotDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsBlocked { get; set; }
    public string? BlockReason { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
