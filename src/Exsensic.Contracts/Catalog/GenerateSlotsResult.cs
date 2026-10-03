using System;
using System.Collections.Generic;
using System.Text;

namespace Exsensic.Contracts.Catalog;

public sealed record GenerateSlotsResult(int Created, int SkippedDuplicates);
