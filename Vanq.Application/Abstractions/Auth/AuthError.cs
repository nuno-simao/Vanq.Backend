namespace Vanq.Application.Abstractions.Auth;

public enum AuthError
{
    None = 0,
    EmailAlreadyInUse,
    InvalidCredentials,
    UserInactive,
    MissingUserContext,
    InvalidRefreshToken
}
