using System;
using System.Collections.Generic;
using System.Text;

using Exsensic.Contracts.Enums;

namespace Exsensic.Core.Entities;

public class Notification
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public int? BookingId { get; set; }
    public NotificationType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
