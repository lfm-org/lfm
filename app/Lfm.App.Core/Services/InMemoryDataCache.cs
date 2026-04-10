namespace Lfm.App.Services;

public sealed class InMemoryDataCache : IDataCache
{
    public event Action<string>? OnInvalidated;
    public void Invalidate(string key) => OnInvalidated?.Invoke(key);
}
