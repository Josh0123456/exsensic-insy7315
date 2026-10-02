using System;
using System.Collections.Generic;
using System.Text;

namespace Exsensic.Core.Entities;


public class BookingRequirement
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public string FieldKey { get; set; } = string.Empty;
    public string FieldValue { get; set; } = string.Empty;
}
