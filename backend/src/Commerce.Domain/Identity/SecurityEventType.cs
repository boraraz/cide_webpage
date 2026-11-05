namespace Commerce.Domain.Identity;

public enum SecurityEventType
{
    Registration,
    LoginSucceeded,
    LoginFailed,
    PasswordChanged,
    PasswordReset,
    SessionRevoked,
    AdminBootstrapped,
}
