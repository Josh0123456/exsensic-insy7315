using System;
using System.Collections.Generic;
using System.Text;

namespace Exsensic.Core.Entities;

public class StaffProfile
{
    public Guid UserId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
}
