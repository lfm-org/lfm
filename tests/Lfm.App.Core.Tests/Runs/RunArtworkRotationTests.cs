// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.App.Runs;
using Xunit;

namespace Lfm.App.Core.Tests.Runs;

public class RunArtworkRotationTests
{
    [Fact]
    public void Shuffle_Uses_Randomizer_To_Prepare_Rotation_Order()
    {
        var randomizer = new SequenceArtworkRotationRandomizer([1, 0]);

        var shuffled = RunArtworkRotation.Shuffle(["cinderbrew", "rookery", "stonevault"], randomizer);

        Assert.Equal(["stonevault", "cinderbrew", "rookery"], shuffled);
        Assert.Equal([3, 2], randomizer.ExclusiveMaxValues);
    }

    private sealed class SequenceArtworkRotationRandomizer(IReadOnlyList<int> values) : IArtworkRotationRandomizer
    {
        private readonly List<int> _exclusiveMaxValues = [];
        private int _index;

        public IReadOnlyList<int> ExclusiveMaxValues => _exclusiveMaxValues;

        public int Next(int exclusiveMax)
        {
            _exclusiveMaxValues.Add(exclusiveMax);
            return values[_index++];
        }
    }
}
