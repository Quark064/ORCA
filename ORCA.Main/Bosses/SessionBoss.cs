using Microsoft.Extensions.Caching.Memory;
using NSA.Security.Core;
using NSA.Security.Core.Attestation.Constants;

namespace ORCA.Main.Bosses;

public class SessionBoss(IMemoryCache cache)
{
    public Version Version { get; } = new(3, 5, 0);
    public GenPlatform Platform { get; } = GenPlatform.Android;

    private readonly IMemoryCache _sCache = cache;

    public Session GetSession(ulong discordId)
    {
        return _sCache.GetOrCreate(discordId, entry =>
        {
            entry.SetSlidingExpiration(TimeSpan.FromHours(1));

            return new Session(Platform, Version);
        }) ?? throw new InvalidOperationException("Failed to generate or retrieve Session.");
    }
}
