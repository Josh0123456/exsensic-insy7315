using System;
using System.Collections.Generic;
using System.Text;

namespace Exsensic.Contracts.Admin;

public sealed record SetUserActiveRequest(bool IsActive);
