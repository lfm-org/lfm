// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.App.Services;

internal static class HttpEtag
{
    public static string? Read(HttpResponseMessage response)
    {
        if (response.Headers.ETag?.Tag is { Length: > 0 } typed)
            return typed;

        return response.Headers.TryGetValues("ETag", out var values)
            ? values.FirstOrDefault()
            : null;
    }
}
