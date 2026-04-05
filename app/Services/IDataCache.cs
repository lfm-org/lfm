namespace Lfm.App.Services;

public interface IDataCache
{
    void Invalidate(string key);
    event Action<string>? OnInvalidated;
}
