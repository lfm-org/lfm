// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Contracts.WoW;
using Xunit;

namespace Lfm.App.Core.Tests.WoW;

public class WowConstantsTests
{
    [Fact]
    public void MaxLevel_is_current_retail_cap()
    {
        Assert.Equal(80, WowConstants.MaxLevel);
    }
}
