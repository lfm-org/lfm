// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.App.Runs;

public interface IArtworkRotationRandomizer
{
    int Next(int exclusiveMax);
}

public sealed class ArtworkRotationRandomizer : IArtworkRotationRandomizer
{
    public int Next(int exclusiveMax) => Random.Shared.Next(exclusiveMax);
}

public static class RunArtworkRotation
{
    public static IReadOnlyList<string> Shuffle(
        IReadOnlyList<string> portraitUrls,
        IArtworkRotationRandomizer randomizer)
    {
        ArgumentNullException.ThrowIfNull(portraitUrls);
        ArgumentNullException.ThrowIfNull(randomizer);

        var shuffled = portraitUrls.ToArray();
        for (var index = shuffled.Length - 1; index > 0; index--)
        {
            var swapIndex = randomizer.Next(index + 1);
            if (swapIndex < 0 || swapIndex > index)
            {
                throw new InvalidOperationException(
                    $"Artwork randomizer returned {swapIndex} for range [0, {index}].");
            }

            (shuffled[index], shuffled[swapIndex]) = (shuffled[swapIndex], shuffled[index]);
        }

        return shuffled;
    }
}
