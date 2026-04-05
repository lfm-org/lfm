namespace Lfm.Api.Auth;

public interface ISessionCipher
{
    string Protect(SessionPrincipal principal);
    SessionPrincipal? Unprotect(string payload);
}
