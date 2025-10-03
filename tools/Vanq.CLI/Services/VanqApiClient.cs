using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Vanq.CLI.Configuration;
using Vanq.CLI.Models;

namespace Vanq.CLI.Services;

/// <summary>
/// HTTP client wrapper for Vanq.API with automatic token refresh and retry logic.
/// </summary>
public class VanqApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _profileName;
    private CliCredentials? _credentials;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public VanqApiClient(string apiEndpoint, string profileName)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(apiEndpoint),
            Timeout = TimeSpan.FromSeconds(30)
        };
        _profileName = profileName;
    }

    public async Task InitializeAsync()
    {
        _credentials = await CredentialsManager.LoadCredentialsAsync(_profileName);
    }

    public bool IsAuthenticated => _credentials != null && !_credentials.IsExpired();

    public async Task<HttpResponseMessage> GetAsync(string endpoint, CancellationToken ct = default)
    {
        return await SendWithRetryAsync(HttpMethod.Get, endpoint, null, ct);
    }

    public async Task<T?> GetAsync<T>(string endpoint, CancellationToken ct = default)
    {
        var response = await GetAsync(endpoint, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
    }

    public async Task<HttpResponseMessage> PostAsync(string endpoint, object? body, CancellationToken ct = default)
    {
        return await SendWithRetryAsync(HttpMethod.Post, endpoint, body, ct);
    }

    public async Task<T?> PostAsync<T>(string endpoint, object? body, CancellationToken ct = default)
    {
        var response = await PostAsync(endpoint, body, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
    }

    public async Task<HttpResponseMessage> PutAsync(string endpoint, object? body, CancellationToken ct = default)
    {
        return await SendWithRetryAsync(HttpMethod.Put, endpoint, body, ct);
    }

    public async Task<HttpResponseMessage> PatchAsync(string endpoint, object? body, CancellationToken ct = default)
    {
        return await SendWithRetryAsync(HttpMethod.Patch, endpoint, body, ct);
    }

    public async Task<HttpResponseMessage> DeleteAsync(string endpoint, CancellationToken ct = default)
    {
        return await SendWithRetryAsync(HttpMethod.Delete, endpoint, null, ct);
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        HttpMethod method,
        string endpoint,
        object? body,
        CancellationToken ct,
        int maxRetries = 3)
    {
        int attempt = 0;
        Exception? lastException = null;

        while (attempt < maxRetries)
        {
            try
            {
                // Check if token needs refresh
                if (_credentials != null && _credentials.IsExpiringSoon())
                {
                    await RefreshTokenAsync(ct);
                }

                var request = new HttpRequestMessage(method, endpoint);

                // Add auth header if authenticated
                if (_credentials != null)
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _credentials.AccessToken);
                }

                // Add body if present
                if (body != null)
                {
                    request.Content = JsonContent.Create(body);
                }

                var response = await _httpClient.SendAsync(request, ct);

                // If 401 and we have credentials, try refreshing token
                if (response.StatusCode == HttpStatusCode.Unauthorized && _credentials != null && attempt == 0)
                {
                    await RefreshTokenAsync(ct);
                    attempt++;
                    continue; // Retry with new token
                }

                // Success or non-retryable error
                return response;
            }
            catch (HttpRequestException ex) when (attempt < maxRetries - 1)
            {
                lastException = ex;
                attempt++;

                // Exponential backoff: 1s, 2s, 4s
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                await Task.Delay(delay, ct);
            }
        }

        throw lastException ?? new HttpRequestException("Request failed after retries");
    }

    private async Task RefreshTokenAsync(CancellationToken ct)
    {
        if (_credentials == null)
            throw new InvalidOperationException("No credentials available for refresh");

        try
        {
            var refreshRequest = new
            {
                refreshToken = _credentials.RefreshToken
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "/auth/refresh")
            {
                Content = JsonContent.Create(refreshRequest)
            };

            var response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                // Refresh failed - clear credentials
                _credentials = null;
                await CredentialsManager.DeleteCredentialsAsync(_profileName);
                throw new InvalidOperationException("Token refresh failed. Please login again.");
            }

            var result = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions, ct);

            if (result == null)
                throw new InvalidOperationException("Invalid refresh response");

            // Update credentials
            _credentials = new CliCredentials(
                _profileName,
                result.AccessToken,
                result.RefreshToken,
                DateTime.UtcNow.AddMinutes(result.ExpiresInMinutes),
                _credentials.Email
            );

            await CredentialsManager.SaveCredentialsAsync(_credentials);
        }
        catch
        {
            // Clear invalid credentials
            _credentials = null;
            await CredentialsManager.DeleteCredentialsAsync(_profileName);
            throw;
        }
    }

    public void SetCredentials(CliCredentials credentials)
    {
        _credentials = credentials;
    }

    private record TokenResponse(string AccessToken, string RefreshToken, int ExpiresInMinutes);
}
