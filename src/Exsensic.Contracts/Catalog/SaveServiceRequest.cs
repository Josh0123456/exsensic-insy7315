using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Exsensic.Contracts.Catalog;

public sealed record SaveServiceRequest(
    [Required, MaxLength(120)] string Name,
    [Required] string Category,
    [Required, MaxLength(2000)] string Description,
    [Range(15, 480)] int DurationMinutes,
    decimal? BasePrice);
