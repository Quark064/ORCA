namespace ORCA.Main.Exceptions;

public class AccountExceptions
{
    public class UserAlreadyExists : Exception { }

    public class InvalidUrl : Exception { }

    public class AuthVerifierMissing : Exception { }

    public class AesKeyMissing : Exception { }

    public class SessionTokenMissing : Exception { }

    public class SessionTokenExpired : Exception { }
}
