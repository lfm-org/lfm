using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Lfm.App.Tests;

public abstract class ComponentTestBase : TestContext
{
    protected ComponentTestBase()
    {
        Services.AddFluentUIComponents();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }
}
