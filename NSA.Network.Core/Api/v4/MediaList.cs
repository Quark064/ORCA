using NSA.Network.Core.Common;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace NSA.Network.Core.Api.v4;

public class MediaList : ManualBase<MediaListResult>
{
    public required string NsaToken;

    public override string URL => "https://api-lp1.znc.srv.nintendo.net/v4/Media/List";

    public override string BuildRequest()
    {
        var data = new JsonObject
        {
            ["parameter"] = new JsonObject()
        };

        return data.ToJsonString();
    }
}

public class MediaListResult
{
    [JsonPropertyName("media")]
    public List<MediaItem> Media { get; set; } = [];
}

public class MediaItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("applicationId")]
    public string ApplicationId { get; set; } = string.Empty;

    [JsonPropertyName("platformId")]
    public int PlatformId { get; set; }

    [JsonPropertyName("contentUri")]
    public string ContentUri { get; set; } = string.Empty;

    [JsonPropertyName("contentLength")]
    public long ContentLength { get; set; }

    [JsonPropertyName("thumbnailUri")]
    public string ThumbnailUri { get; set; } = string.Empty;

    [JsonPropertyName("appName")]
    public string AppName { get; set; } = string.Empty;

    [JsonPropertyName("orientation")]
    public int Orientation { get; set; }

    [JsonPropertyName("acdIndex")]
    public int AcdIndex { get; set; }

    [JsonPropertyName("extraData")]
    public string ExtraData { get; set; } = string.Empty;

    [JsonPropertyName("capturedAt")]
    public long CapturedAt { get; set; }

    [JsonPropertyName("expiresAt")]
    public long ExpiresAt { get; set; }

    [JsonPropertyName("uploadedAt")]
    public long UploadedAt { get; set; }

    [JsonPropertyName("hashtags")]
    public string Hashtags { get; set; } = string.Empty;

    [JsonPropertyName("videoDuration")]
    public int? VideoDuration { get; set; }
}