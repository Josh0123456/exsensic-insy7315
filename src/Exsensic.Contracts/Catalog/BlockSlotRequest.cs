using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Exsensic.Contracts.Catalog;

public sealed record BlockSlotRequest(
    [Required, MaxLength(500)] string Reason,
    string RowVersion);
