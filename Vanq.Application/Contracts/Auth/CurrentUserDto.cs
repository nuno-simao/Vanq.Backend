using System;
using System.Collections.Generic;

namespace Vanq.Application.Contracts.Auth;

public sealed record CurrentUserDto(Guid Id, string Email, IReadOnlyCollection<string> Roles, IReadOnlyCollection<string> Permissions);
