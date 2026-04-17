// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Api.Auth;

public interface ISessionCipher
{
    string Protect(SessionPrincipal principal);
    SessionPrincipal? Unprotect(string payload);
}
