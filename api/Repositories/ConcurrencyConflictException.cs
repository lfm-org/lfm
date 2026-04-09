namespace Lfm.Api.Repositories;

public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException() : base("Concurrent modification detected") { }
    public ConcurrencyConflictException(Exception inner) : base("Concurrent modification detected", inner) { }
}
