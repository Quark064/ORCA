using ZoneTree;

namespace ORCA.Main.Bosses;

public sealed class DatabaseBoss : IDisposable
{
    public readonly IZoneTree<ulong, ulong> LoginMessageDB;
    public readonly IZoneTree<ulong, string> AuthVerifierDB;
    public readonly IZoneTree<ulong, ulong> TokenDB;
    public readonly IZoneTree<ulong, Memory<byte>> KeyDB;

    public DatabaseBoss()
    {
        LoginMessageDB = new ZoneTreeFactory<ulong, ulong>()
            .SetDataDirectory("data/loginMsg")
            .OpenOrCreate();

        AuthVerifierDB = new ZoneTreeFactory<ulong, string>()
            .SetDataDirectory("data/authVerifier")
            .OpenOrCreate();

        TokenDB = new ZoneTreeFactory<ulong, ulong>()
            .SetDataDirectory("data/token")
            .OpenOrCreate();

        KeyDB = new ZoneTreeFactory<ulong, Memory<byte>>()
            .SetDataDirectory("data/keys")
            .OpenOrCreate();
    }

    public void Dispose()
    {
        LoginMessageDB.Dispose();
        AuthVerifierDB.Dispose();
        TokenDB.Dispose();
        KeyDB.Dispose();
    }
}
