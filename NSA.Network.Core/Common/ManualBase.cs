using NSA.Network.Core.Common.Interfaces;
using System.Text.Json;

namespace NSA.Network.Core.Common;

public abstract class ManualBase<T> : IRequestBase
{
    public abstract string URL { get; }

    public abstract string BuildRequest();

    public T? ParseResponse(string response, out ApiStatus statusCode)
    {
        var ninResponse = JsonSerializer.Deserialize<ApiResponse<T>>(response)!;
        statusCode = (ApiStatus)ninResponse.Status;

        return ninResponse.Result;
    }
}
