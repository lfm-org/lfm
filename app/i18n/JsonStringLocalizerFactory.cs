using Microsoft.Extensions.Localization;

namespace Lfm.App.i18n;

/// <summary>
/// Minimal factory that returns the singleton <see cref="JsonStringLocalizer"/>.
/// </summary>
public sealed class JsonStringLocalizerFactory(IStringLocalizer localizer) : IStringLocalizerFactory
{
    public IStringLocalizer Create(Type resourceSource) => localizer;

    public IStringLocalizer Create(string baseName, string location) => localizer;
}
