// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Lfm.Api.Auth;

public sealed class DataProtectionSessionCipher(IDataProtectionProvider provider) : ISessionCipher
{
    private static readonly string Purpose = "Lfm.Session.v1";
    private readonly IDataProtector _protector = provider.CreateProtector(Purpose);

    public string Protect(SessionPrincipal principal)
    {
        var json = JsonSerializer.Serialize(principal);
        return _protector.Protect(json);
    }

    public SessionPrincipal? Unprotect(string payload)
    {
        try
        {
            var json = _protector.Unprotect(payload);
            return JsonSerializer.Deserialize<SessionPrincipal>(json);
        }
        catch
        {
            return null;
        }
    }
}
