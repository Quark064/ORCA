namespace ORCA.Main.Exceptions;

public static class NetworkExceptions
{
    public class SessionTokenGetFailure : Exception { }

    public class ApiTokensGetFailure : Exception { }

    public class NsaTokenGetFailure : Exception { }

    public class MediaGetFailure : Exception { }

    public class FriendGetFailure : Exception { }

    public class SelfGetFailure : Exception { }

    public class PlayLogShowGetFailure : Exception { }

    public class GameWebTokenGetFailure : Exception { }

    public class BulletTokenGetFailure : Exception { }

    public class RemoteResourceGetFailure : Exception { }
}
