using Lfm.E2E.Infrastructure;
using Xunit;

namespace Lfm.E2E.Fixtures;

[CollectionDefinition("Accessibility")]
public class AccessibilityCollection : ICollectionFixture<AccessibilityFixture> { }

public class AccessibilityFixture : IAsyncLifetime
{
    public StackFixture Stack { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Stack = await SharedStack.GetAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
