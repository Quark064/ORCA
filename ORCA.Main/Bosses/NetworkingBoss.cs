using Microsoft.Extensions.Logging;
using NSA.Network.Core.Accounts.Connect.v1;
using NSA.Network.Core.Accounts.v2;
using NSA.Network.Core.Api;
using NSA.Network.Core.Api.v4;
using NSA.Network.Core.Common;
using NSA.Security.Core;
using ORCA.Main.Exceptions;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ORCA.Main.Bosses;

public class NetworkingBoss
{
    private readonly HttpClient _webClient;
    private readonly HttpClient _apiClient;
    private readonly HttpClient _appClient;

    private readonly SessionBoss _sb;

    private readonly ILogger _logger;

    public NetworkingBoss(
        IHttpClientFactory clientFactory,
        SessionBoss sb,
        ILogger<NetworkingBoss> logger
    )
    {
        _webClient = clientFactory.CreateClient("WebClient");
        _apiClient = clientFactory.CreateClient("ApiClient");
        _appClient = clientFactory.CreateClient("AppClient");

        _sb = sb;

        _logger = logger;
    }

    public async Task<ApiSessionTokenResponse> GetSessionToken(ApiSessionToken request)
    {
        try
        {
            var content = request.BuildRequest();
            var result = await _webClient.PostAsync(request.URL, content);

            result.EnsureSuccessStatusCode();

            var jsonResult = await result.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<ApiSessionTokenResponse>(jsonResult)!;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get Session Token.");
            throw new NetworkExceptions.SessionTokenGetFailure();
        }
    }
    
    public async Task<ApiTokenResponse> GetApiTokens(ApiToken request)
    {
        try
        {
            var content = request.BuildRequest();
            var result = await _webClient.PostAsync(request.URL, content);

            result.EnsureSuccessStatusCode();

            var jsonResult = await result.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<ApiTokenResponse>(jsonResult)!;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get Api Tokens");
            throw new NetworkExceptions.ApiTokensGetFailure();
        }
    }

    public async Task<UsersMeResponse> GetUsersMe(UsersMe request)
    {
        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, request.URL);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.AccessToken);
            httpRequest.Headers.TryAddWithoutValidation("Content-Type", "application/json");
            httpRequest.Headers.TryAddWithoutValidation("Accept-Language", "en-US");

            var result = await _apiClient.SendAsync(httpRequest);
            
            result.EnsureSuccessStatusCode();

            var jsonResult = await result.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<UsersMeResponse>(jsonResult)!;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get UsersMe");
            throw new NetworkExceptions.ApiTokensGetFailure();
        }
    }


    public async Task<AccountLoginResult> GetNsaToken(AccountLogin request, Session session)
    {
        try
        {
            var textBody = request.BuildRequest();
            var cipherBody = await session.EncryptRequest(request.URL, string.Empty, textBody);

            using var content = new ByteArrayContent(cipherBody);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            
            content.Headers.Add("x-platform", "Android");
            content.Headers.Add("x-productversion", _sb.Version.ToString());

            var response = await _appClient.PostAsync(request.URL, content);

            response.EnsureSuccessStatusCode();

            var responseBytes = await response.Content.ReadAsByteArrayAsync();
            var responseJson = await session.DecryptResponse(responseBytes);

            var result = request.ParseResponse(responseJson, out ApiStatus statusCode);
            if (statusCode != ApiStatus.OK)
            {
                _logger.LogError("/Account/Login returned a non-success status code! {Json}", responseJson);
                throw new NetworkExceptions.NsaTokenGetFailure();
            }

            if (result == null)
            {
                _logger.LogWarning("Failed to parse the response from /Account/Login! {Json}", responseJson);
                throw new NetworkExceptions.NsaTokenGetFailure();
            }

            return result;
        }
        catch (NetworkExceptions.NsaTokenGetFailure)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get NSA Token.");
            throw new NetworkExceptions.NsaTokenGetFailure();
        }
    }


    public async Task<MediaListResult> GetMedia(MediaList request, Session session)
    {
        try
        {
            var responseJson = await SendEncryptedRequest
            (
                request.URL,
                request.NsaToken,
                request.BuildRequest(),
                session
            );

            var result = request.ParseResponse(responseJson, out ApiStatus statusCode);
            if (statusCode != ApiStatus.OK)
            {
                _logger.LogError("/Media/List returned a non-success status code! {Json}", responseJson);

                throw new NetworkExceptions.MediaGetFailure();
            }

            if (result == null)
            {
                _logger.LogWarning("Failed to parse the response from /Media/List! {Json}", responseJson);

                throw new NetworkExceptions.MediaGetFailure();
            }

            return result;
        }
        catch (NetworkExceptions.MediaGetFailure)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get Media!");
            throw new NetworkExceptions.MediaGetFailure();
        }
    }

    public async Task<FriendListResult> GetFriends(FriendList request, Session session)
    {
        try
        {
            var responseJson = await SendEncryptedRequest
            (
                request.URL,
                request.NsaToken,
                request.BuildRequest(),
                session
            );

            var result = request.ParseResponse(responseJson, out ApiStatus statusCode);
            if (statusCode != ApiStatus.OK)
            {
                _logger.LogError("/Friend/List returned a non-success status code! {Json}", responseJson);
                throw new NetworkExceptions.FriendGetFailure();
            }

            if (result == null)
            {
                _logger.LogWarning("Failed to parse the response from /Friend/List! {Json}", responseJson);
                throw new NetworkExceptions.FriendGetFailure();
            }

            return result;
        }
        catch (NetworkExceptions.FriendGetFailure)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get Friends!");
            throw new NetworkExceptions.FriendGetFailure();
        }
    }

    public async Task<ShowSelfResponse> GetSelf(ShowSelf request, Session session)
    {
        try
        {
            var responseJson = await SendEncryptedRequest
            (
                request.URL,
                request.NsaToken,
                request.BuildRequest(),
                session
            );

            var result = request.ParseResponse(responseJson, out ApiStatus statusCode);
            if (statusCode != ApiStatus.OK)
            {
                _logger.LogError("/User/ShowSelf returned a non-success status code! {Json}", responseJson);
                throw new NetworkExceptions.SelfGetFailure();
            }

            if (result == null)
            {
                _logger.LogWarning("Failed to parse the response from /User/ShowSelf! {Json}", responseJson);
                throw new NetworkExceptions.SelfGetFailure();
            }

            return result;
        }
        catch (NetworkExceptions.SelfGetFailure)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get Self!");
            throw new NetworkExceptions.SelfGetFailure();
        }
    }

    public async Task<List<PlayLogShowResponse>> GetPlayLog(PlayLogShow request, Session session)
    {
        try
        {
            var responseJson = await SendEncryptedRequest
            (
                request.URL,
                request.NsaToken,
                request.BuildRequest(),
                session
            );

            var result = request.ParseResponse(responseJson, out ApiStatus statusCode);
            if (statusCode != ApiStatus.OK)
            {
                _logger.LogError("/User/PlayLog/Show returned a non-success status code! {Json}", responseJson);
                throw new NetworkExceptions.PlayLogShowGetFailure();
            }

            if (result == null)
            {
                _logger.LogWarning("Failed to parse the response from /User/PlayLog/Show! {Json}", responseJson);
                throw new NetworkExceptions.PlayLogShowGetFailure();
            }

            return result;
        }
        catch (NetworkExceptions.PlayLogShowGetFailure)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get Self!");
            throw new NetworkExceptions.PlayLogShowGetFailure();
        }
    }


    public async Task<WebServiceTokenResponse> GetWebServiceToken(WebServiceToken request, Session session)
    {
        try
        {
            var textBody = request.BuildRequest();
            var cipherBody = await session.EncryptRequest(request.URL, request.AccountLoginToken, textBody);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, request.URL)
            {
                Content = new ByteArrayContent(cipherBody)
            };

            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.AccountLoginToken);
            httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            httpRequest.Headers.Add("x-platform", "Android");
            httpRequest.Headers.Add("x-productversion", _sb.Version.ToString());

            var response = await _appClient.SendAsync(httpRequest);

            response.EnsureSuccessStatusCode();

            var responseBytes = await response.Content.ReadAsByteArrayAsync();
            var responseJson = await session.DecryptResponse(responseBytes);

            var result = request.ParseResponse(responseJson, out ApiStatus statusCode);
            if (statusCode != ApiStatus.OK)
            {
                _logger.LogError("v4/Game/GetWebServiceToken returned a non-success status code! {Json}", responseJson);
                throw new NetworkExceptions.GameWebTokenGetFailure();
            }

            if (result == null)
            {
                _logger.LogWarning("Failed to parse the response from v4/Game/GetWebServiceToken! {Json}", responseJson);
                throw new NetworkExceptions.GameWebTokenGetFailure();
            }

            return result;
        }
        catch (NetworkExceptions.GameWebTokenGetFailure)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get GameWeb Token.");
            throw new NetworkExceptions.GameWebTokenGetFailure();
        }
    }

    public async Task<BulletTokenResponse> GetBulletToken(BulletToken request)
    {
        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, request.URL);
            httpRequest.Headers.TryAddWithoutValidation("x-app-ver", _sb.Version.ToString());
            httpRequest.Headers.TryAddWithoutValidation("x-gamewebtoken", request.GameWebToken);
            httpRequest.Headers.TryAddWithoutValidation("accept-language", "en-US");
            httpRequest.Headers.TryAddWithoutValidation("content-type", "application/json");

            var result = await _appClient.SendAsync(httpRequest);

            result.EnsureSuccessStatusCode();

            var jsonResult = await result.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<BulletTokenResponse>(jsonResult)!;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get Bullet Token!");
            throw new NetworkExceptions.BulletTokenGetFailure();
        }
    }


    public async Task<RemoteResource> GetRemoteResourceStream(string url)
    {
        try
        {
            var response = await _appClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

            response.EnsureSuccessStatusCode();

            var stream = await response.Content.ReadAsStreamAsync();

            return new RemoteResource(response, stream);
        }
        catch
        {
            throw new NetworkExceptions.RemoteResourceGetFailure();
        }
    }

    private async Task<string> SendEncryptedRequest(string url, string nsaToken, string textBody, Session session)
    {
        var cipherBody = await session.EncryptRequest(url, nsaToken, textBody);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new ByteArrayContent(cipherBody)
        };

        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", nsaToken);
        httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using var response = await _appClient.SendAsync(httpRequest);

        response.EnsureSuccessStatusCode();

        var responseBytes = await response.Content.ReadAsByteArrayAsync();

        return await session.DecryptResponse(responseBytes);
    }
}

public sealed class RemoteResource(HttpResponseMessage response, Stream stream) : IDisposable
{
    public Stream Stream { get; } = stream;
    private readonly HttpResponseMessage _response = response;

    public void Dispose()
    {
        Stream.Dispose();
        _response.Dispose();
    }
}
