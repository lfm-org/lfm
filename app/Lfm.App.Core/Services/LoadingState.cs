namespace Lfm.App.Services;

public abstract record LoadingState<T>
{
    public sealed record Idle : LoadingState<T>;
    public sealed record Loading : LoadingState<T>;
    public sealed record Success(T Value) : LoadingState<T>;
    public sealed record Failure(string Message) : LoadingState<T>;
}
