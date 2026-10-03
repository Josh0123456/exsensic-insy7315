using System;
using System.Collections.Generic;
using System.Text;

namespace Exsensic.Core.Entities;

public class ClientProfile
{
    public Guid UserId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}
