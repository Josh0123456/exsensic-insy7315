using System;
using System.Collections.Generic;
using System.Text;

namespace Exsensic.Contracts.Catalog;

public sealed record SetActiveRequest(bool IsActive, string RowVersion);
