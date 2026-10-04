using System;
using System.Collections.Generic;
using System.Text;

namespace Exsensic.Contracts.Catalog;

public sealed record ServiceDto(
    int Id,
    string Name,
    string Category,
    string Description,
    int DurationMinutes,
    decimal? BasePrice,
    bool IsActive,
    string RowVersion);
