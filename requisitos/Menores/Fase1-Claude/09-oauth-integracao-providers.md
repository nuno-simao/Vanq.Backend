# Parte 9: Integração OAuth com Provedores

## 📋 Visão Geral
**Duração**: 30-45 minutos  
**Complexidade**: Média-Alta  
**Dependências**: Partes 1-8 (Setup + Entidades + EF + DTOs + Validações + Serviços + 2FA)

Esta parte implementa integração completa com provedores OAuth (Google, GitHub, Microsoft, Facebook), incluindo fluxo de autorização, registro automático de usuários, vinculação de contas existentes e gerenciamento de tokens.

## 🎯 Objetivos
- ✅ Implementar OAuth flow para Google, GitHub, Microsoft
- ✅ Configurar registro automático via OAuth
- ✅ Implementar vinculação de contas OAuth
- ✅ Criar gerenciamento de tokens OAuth
- ✅ Implementar refresh tokens OAuth
- ✅ Configurar rate limiting para OAuth
- ✅ Implementar desvinculação de contas

## 📁 Arquivos a serem Criados

```
src/IDE.Application/Auth/Services/OAuth/
├── IOAuthService.cs
├── OAuthService.cs
├── Providers/
│   ├── IOAuthProvider.cs
│   ├── GoogleOAuthProvider.cs
│   ├── GitHubOAuthProvider.cs
│   ├── MicrosoftOAuthProvider.cs
│   └── FacebookOAuthProvider.cs
├── Models/
│   ├── OAuthUserInfo.cs
│   ├── OAuthTokenResponse.cs
│   ├── OAuthLoginResult.cs
│   └── OAuthProviderConfig.cs
└── Exceptions/
    ├── OAuthException.cs
    └── OAuthProviderException.cs
```

## 🚀 Execução Passo a Passo

### 1. Criar Models OAuth

#### src/IDE.Application/Auth/Services/OAuth/Models/OAuthUserInfo.cs
```csharp
namespace IDE.Application.Auth.Services.OAuth.Models;

/// <summary>
/// Informações do usuário obtidas do provedor OAuth
/// </summary>
public class OAuthUserInfo
{
    /// <summary>
    /// ID único do usuário no provedor
    /// </summary>
    public string ProviderId { get; set; } = string.Empty;
    
    /// <summary>
    /// Nome do provedor (google, github, microsoft, etc.)
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;
    
    /// <summary>
    /// Email do usuário
    /// </summary>
    public string Email { get; set; } = string.Empty;
    
    /// <summary>
    /// Se o email foi verificado pelo provedor
    /// </summary>
    public bool EmailVerified { get; set; }
    
    /// <summary>
    /// Nome completo do usuário
    /// </summary>
    public string FullName { get; set; } = string.Empty;
    
    /// <summary>
    /// Primeiro nome
    /// </summary>
    public string FirstName { get; set; } = string.Empty;
    
    /// <summary>
    /// Último nome
    /// </summary>
    public string LastName { get; set; } = string.Empty;
    
    /// <summary>
    /// URL da foto do perfil
    /// </summary>
    public string? ProfilePictureUrl { get; set; }
    
    /// <summary>
    /// Username no provedor
    /// </summary>
    public string? Username { get; set; }
    
    /// <summary>
    /// Localização/país
    /// </summary>
    public string? Location { get; set; }
    
    /// <summary>
    /// Dados adicionais específicos do provedor
    /// </summary>
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}
```

#### src/IDE.Application/Auth/Services/OAuth/Models/OAuthTokenResponse.cs
```csharp
namespace IDE.Application.Auth.Services.OAuth.Models;

/// <summary>
/// Resposta de token OAuth do provedor
/// </summary>
public class OAuthTokenResponse
{
    /// <summary>
    /// Access token
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;
    
    /// <summary>
    /// Refresh token (se disponível)
    /// </summary>
    public string? RefreshToken { get; set; }
    
    /// <summary>
    /// Tipo do token (geralmente "Bearer")
    /// </summary>
    public string TokenType { get; set; } = "Bearer";
    
    /// <summary>
    /// Tempo de expiração em segundos
    /// </summary>
    public int ExpiresIn { get; set; }
    
    /// <summary>
    /// Escopos concedidos
    /// </summary>
    public string? Scope { get; set; }
    
    /// <summary>
    /// ID Token (para OpenID Connect)
    /// </summary>
    public string? IdToken { get; set; }
    
    /// <summary>
    /// Data de expiração calculada
    /// </summary>
    public DateTime ExpiresAt => DateTime.UtcNow.AddSeconds(ExpiresIn);
}
```

#### src/IDE.Application/Auth/Services/OAuth/Models/OAuthLoginResult.cs
```csharp
using IDE.Domain.Entities;

namespace IDE.Application.Auth.Services.OAuth.Models;

/// <summary>
/// Resultado do login OAuth
/// </summary>
public class OAuthLoginResult
{
    /// <summary>
    /// Se o login foi bem-sucedido
    /// </summary>
    public bool IsSuccess { get; set; }
    
    /// <summary>
    /// Usuário autenticado
    /// </summary>
    public User? User { get; set; }
    
    /// <summary>
    /// Se é um novo usuário criado
    /// </summary>
    public bool IsNewUser { get; set; }
    
    /// <summary>
    /// Conta OAuth vinculada/criada
    /// </summary>
    public UserOAuthAccount? OAuthAccount { get; set; }
    
    /// <summary>
    /// Informações do usuário do provedor
    /// </summary>
    public OAuthUserInfo? UserInfo { get; set; }
    
    /// <summary>
    /// Tokens OAuth
    /// </summary>
    public OAuthTokenResponse? Tokens { get; set; }
    
    /// <summary>
    /// Erro se houver
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Se requer vinculação manual (email já existe)
    /// </summary>
    public bool RequiresLinking { get; set; }
    
    /// <summary>
    /// Token temporário para vinculação
    /// </summary>
    public string? LinkingToken { get; set; }
}
```

#### src/IDE.Application/Auth/Services/OAuth/Models/OAuthProviderConfig.cs
```csharp
namespace IDE.Application.Auth.Services.OAuth.Models;

/// <summary>
/// Configuração do provedor OAuth
/// </summary>
public class OAuthProviderConfig
{
    /// <summary>
    /// Nome do provedor
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Client ID
    /// </summary>
    public string ClientId { get; set; } = string.Empty;
    
    /// <summary>
    /// Client Secret
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;
    
    /// <summary>
    /// URL de autorização
    /// </summary>
    public string AuthorizationUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// URL para trocar código por token
    /// </summary>
    public string TokenUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// URL para obter informações do usuário
    /// </summary>
    public string UserInfoUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// Escopos padrão
    /// </summary>
    public List<string> DefaultScopes { get; set; } = new();
    
    /// <summary>
    /// URL de redirecionamento
    /// </summary>
    public string RedirectUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// Se está habilitado
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
```

### 2. Criar Exceções OAuth

#### src/IDE.Application/Auth/Services/OAuth/Exceptions/OAuthException.cs
```csharp
namespace IDE.Application.Auth.Services.OAuth.Exceptions;

/// <summary>
/// Exceção base para erros OAuth
/// </summary>
public class OAuthException : Exception
{
    public string? ErrorCode { get; }
    public string? ErrorDescription { get; }
    public string? ProviderName { get; }

    public OAuthException(string message) : base(message)
    {
    }

    public OAuthException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public OAuthException(string message, string? errorCode, string? errorDescription, string? providerName = null) 
        : base(message)
    {
        ErrorCode = errorCode;
        ErrorDescription = errorDescription;
        ProviderName = providerName;
    }
}

/// <summary>
/// Exceção específica de provedor OAuth
/// </summary>
public class OAuthProviderException : OAuthException
{
    public OAuthProviderException(string providerName, string message) 
        : base($"[{providerName}] {message}")
    {
        ProviderName = providerName;
    }

    public OAuthProviderException(string providerName, string message, Exception innerException) 
        : base($"[{providerName}] {message}", innerException)
    {
        ProviderName = providerName;
    }
}
```

### 3. Implementar Interface Base do Provedor OAuth

#### src/IDE.Application/Auth/Services/OAuth/Providers/IOAuthProvider.cs
```csharp
using IDE.Application.Auth.Services.OAuth.Models;

namespace IDE.Application.Auth.Services.OAuth.Providers;

/// <summary>
/// Interface base para provedores OAuth
/// </summary>
public interface IOAuthProvider
{
    /// <summary>
    /// Nome do provedor
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Gera URL de autorização
    /// </summary>
    string GetAuthorizationUrl(string state, IEnumerable<string>? scopes = null);
    
    /// <summary>
    /// Troca código de autorização por token
    /// </summary>
    Task<OAuthTokenResponse> ExchangeCodeForTokenAsync(string code, string? state = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Obtém informações do usuário usando access token
    /// </summary>
    Task<OAuthUserInfo> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Renova access token usando refresh token
    /// </summary>
    Task<OAuthTokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Revoga token
    /// </summary>
    Task<bool> RevokeTokenAsync(string token, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Valida configuração do provedor
    /// </summary>
    bool ValidateConfiguration();
}
```

### 4. Implementar Google OAuth Provider

#### src/IDE.Application/Auth/Services/OAuth/Providers/GoogleOAuthProvider.cs
```csharp
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using IDE.Application.Auth.Services.OAuth.Models;
using IDE.Application.Auth.Services.OAuth.Exceptions;

namespace IDE.Application.Auth.Services.OAuth.Providers;

/// <summary>
/// Provedor OAuth para Google
/// </summary>
public class GoogleOAuthProvider : IOAuthProvider
{
    public string Name => "Google";

    private readonly OAuthProviderConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleOAuthProvider> _logger;

    private const string AuthorizationBaseUrl = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenUrl = "https://oauth2.googleapis.com/token";
    private const string UserInfoUrl = "https://www.googleapis.com/oauth2/v2/userinfo";
    private const string RevokeUrl = "https://oauth2.googleapis.com/revoke";

    public GoogleOAuthProvider(
        IOptionsMonitor<OAuthProviderConfig> optionsMonitor,
        HttpClient httpClient,
        ILogger<GoogleOAuthProvider> logger)
    {
        _config = optionsMonitor.Get("Google");
        _httpClient = httpClient;
        _logger = logger;
    }

    public string GetAuthorizationUrl(string state, IEnumerable<string>? scopes = null)
    {
        var scopeList = scopes?.ToList() ?? _config.DefaultScopes.Any() 
            ? _config.DefaultScopes 
            : new List<string> { "openid", "email", "profile" };

        var parameters = new Dictionary<string, string>
        {
            ["client_id"] = _config.ClientId,
            ["redirect_uri"] = _config.RedirectUrl,
            ["scope"] = string.Join(" ", scopeList),
            ["response_type"] = "code",
            ["state"] = state,
            ["access_type"] = "offline", // Para obter refresh token
            ["prompt"] = "consent" // Força prompt de consentimento
        };

        var queryString = string.Join("&", parameters.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
        return $"{AuthorizationBaseUrl}?{queryString}";
    }

    public async Task<OAuthTokenResponse> ExchangeCodeForTokenAsync(string code, string? state = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["client_id"] = _config.ClientId,
                ["client_secret"] = _config.ClientSecret,
                ["code"] = code,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = _config.RedirectUrl
            };

            var content = new FormUrlEncodedContent(parameters);
            var response = await _httpClient.PostAsync(TokenUrl, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new OAuthProviderException(Name, $"Token exchange failed: {errorContent}");
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var tokenData = JsonSerializer.Deserialize<JsonElement>(jsonContent);

            return new OAuthTokenResponse
            {
                AccessToken = tokenData.GetProperty("access_token").GetString()!,
                RefreshToken = tokenData.TryGetProperty("refresh_token", out var refreshToken) ? refreshToken.GetString() : null,
                TokenType = tokenData.GetProperty("token_type").GetString()!,
                ExpiresIn = tokenData.GetProperty("expires_in").GetInt32(),
                Scope = tokenData.TryGetProperty("scope", out var scope) ? scope.GetString() : null,
                IdToken = tokenData.TryGetProperty("id_token", out var idToken) ? idToken.GetString() : null
            };
        }
        catch (Exception ex) when (!(ex is OAuthProviderException))
        {
            _logger.LogError(ex, "Erro ao trocar código por token no Google");
            throw new OAuthProviderException(Name, "Falha na troca de código por token", ex);
        }
    }

    public async Task<OAuthUserInfo> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            
            var response = await _httpClient.GetAsync(UserInfoUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new OAuthProviderException(Name, $"Failed to get user info: {errorContent}");
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var userData = JsonSerializer.Deserialize<JsonElement>(jsonContent);

            var email = userData.GetProperty("email").GetString()!;
            var name = userData.GetProperty("name").GetString()!;
            var nameParts = name.Split(' ', 2);

            return new OAuthUserInfo
            {
                ProviderId = userData.GetProperty("id").GetString()!,
                ProviderName = Name,
                Email = email,
                EmailVerified = userData.TryGetProperty("verified_email", out var verified) ? verified.GetBoolean() : false,
                FullName = name,
                FirstName = nameParts.Length > 0 ? nameParts[0] : name,
                LastName = nameParts.Length > 1 ? nameParts[1] : "",
                ProfilePictureUrl = userData.TryGetProperty("picture", out var picture) ? picture.GetString() : null,
                Location = userData.TryGetProperty("locale", out var locale) ? locale.GetString() : null,
                AdditionalData = new Dictionary<string, object>
                {
                    ["google_id"] = userData.GetProperty("id").GetString()!,
                    ["locale"] = userData.TryGetProperty("locale", out var loc) ? loc.GetString()! : ""
                }
            };
        }
        catch (Exception ex) when (!(ex is OAuthProviderException))
        {
            _logger.LogError(ex, "Erro ao obter informações do usuário do Google");
            throw new OAuthProviderException(Name, "Falha ao obter informações do usuário", ex);
        }
    }

    public async Task<OAuthTokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["client_id"] = _config.ClientId,
                ["client_secret"] = _config.ClientSecret,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token"
            };

            var content = new FormUrlEncodedContent(parameters);
            var response = await _httpClient.PostAsync(TokenUrl, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new OAuthProviderException(Name, $"Token refresh failed: {errorContent}");
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var tokenData = JsonSerializer.Deserialize<JsonElement>(jsonContent);

            return new OAuthTokenResponse
            {
                AccessToken = tokenData.GetProperty("access_token").GetString()!,
                RefreshToken = refreshToken, // Google pode não retornar novo refresh token
                TokenType = tokenData.GetProperty("token_type").GetString()!,
                ExpiresIn = tokenData.GetProperty("expires_in").GetInt32(),
                Scope = tokenData.TryGetProperty("scope", out var scope) ? scope.GetString() : null
            };
        }
        catch (Exception ex) when (!(ex is OAuthProviderException))
        {
            _logger.LogError(ex, "Erro ao renovar token do Google");
            throw new OAuthProviderException(Name, "Falha ao renovar token", ex);
        }
    }

    public async Task<bool> RevokeTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new Dictionary<string, string> { ["token"] = token };
            var content = new FormUrlEncodedContent(parameters);
            
            var response = await _httpClient.PostAsync(RevokeUrl, content, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao revogar token do Google");
            return false;
        }
    }

    public bool ValidateConfiguration()
    {
        return !string.IsNullOrEmpty(_config.ClientId) &&
               !string.IsNullOrEmpty(_config.ClientSecret) &&
               !string.IsNullOrEmpty(_config.RedirectUrl);
    }
}
```

### 5. Implementar GitHub OAuth Provider

#### src/IDE.Application/Auth/Services/OAuth/Providers/GitHubOAuthProvider.cs
```csharp
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using IDE.Application.Auth.Services.OAuth.Models;
using IDE.Application.Auth.Services.OAuth.Exceptions;

namespace IDE.Application.Auth.Services.OAuth.Providers;

/// <summary>
/// Provedor OAuth para GitHub
/// </summary>
public class GitHubOAuthProvider : IOAuthProvider
{
    public string Name => "GitHub";

    private readonly OAuthProviderConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubOAuthProvider> _logger;

    private const string AuthorizationBaseUrl = "https://github.com/login/oauth/authorize";
    private const string TokenUrl = "https://github.com/login/oauth/access_token";
    private const string UserInfoUrl = "https://api.github.com/user";
    private const string UserEmailUrl = "https://api.github.com/user/emails";

    public GitHubOAuthProvider(
        IOptionsMonitor<OAuthProviderConfig> optionsMonitor,
        HttpClient httpClient,
        ILogger<GitHubOAuthProvider> logger)
    {
        _config = optionsMonitor.Get("GitHub");
        _httpClient = httpClient;
        _logger = logger;

        // GitHub API requer User-Agent
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "IDE-Platform/1.0");
    }

    public string GetAuthorizationUrl(string state, IEnumerable<string>? scopes = null)
    {
        var scopeList = scopes?.ToList() ?? _config.DefaultScopes.Any() 
            ? _config.DefaultScopes 
            : new List<string> { "user:email" };

        var parameters = new Dictionary<string, string>
        {
            ["client_id"] = _config.ClientId,
            ["redirect_uri"] = _config.RedirectUrl,
            ["scope"] = string.Join(" ", scopeList),
            ["state"] = state
        };

        var queryString = string.Join("&", parameters.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
        return $"{AuthorizationBaseUrl}?{queryString}";
    }

    public async Task<OAuthTokenResponse> ExchangeCodeForTokenAsync(string code, string? state = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["client_id"] = _config.ClientId,
                ["client_secret"] = _config.ClientSecret,
                ["code"] = code,
                ["redirect_uri"] = _config.RedirectUrl
            };

            var content = new FormUrlEncodedContent(parameters);
            
            // GitHub espera Accept: application/json
            using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl) { Content = content };
            request.Headers.Add("Accept", "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new OAuthProviderException(Name, $"Token exchange failed: {errorContent}");
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var tokenData = JsonSerializer.Deserialize<JsonElement>(jsonContent);

            // Verifica se há erro na resposta
            if (tokenData.TryGetProperty("error", out var error))
            {
                var errorDescription = tokenData.TryGetProperty("error_description", out var desc) ? desc.GetString() : "Unknown error";
                throw new OAuthProviderException(Name, $"OAuth error: {error.GetString()} - {errorDescription}");
            }

            return new OAuthTokenResponse
            {
                AccessToken = tokenData.GetProperty("access_token").GetString()!,
                TokenType = tokenData.TryGetProperty("token_type", out var tokenType) ? tokenType.GetString()! : "Bearer",
                Scope = tokenData.TryGetProperty("scope", out var scope) ? scope.GetString() : null
            };
        }
        catch (Exception ex) when (!(ex is OAuthProviderException))
        {
            _logger.LogError(ex, "Erro ao trocar código por token no GitHub");
            throw new OAuthProviderException(Name, "Falha na troca de código por token", ex);
        }
    }

    public async Task<OAuthUserInfo> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            // Busca informações básicas do usuário
            var userResponse = await _httpClient.GetAsync(UserInfoUrl, cancellationToken);
            if (!userResponse.IsSuccessStatusCode)
            {
                var errorContent = await userResponse.Content.ReadAsStringAsync(cancellationToken);
                throw new OAuthProviderException(Name, $"Failed to get user info: {errorContent}");
            }

            var userContent = await userResponse.Content.ReadAsStringAsync(cancellationToken);
            var userData = JsonSerializer.Deserialize<JsonElement>(userContent);

            // Busca emails do usuário
            var emailResponse = await _httpClient.GetAsync(UserEmailUrl, cancellationToken);
            string primaryEmail = "";
            bool emailVerified = false;

            if (emailResponse.IsSuccessStatusCode)
            {
                var emailContent = await emailResponse.Content.ReadAsStringAsync(cancellationToken);
                var emailsData = JsonSerializer.Deserialize<JsonElement[]>(emailContent);
                
                var primaryEmailData = emailsData?.FirstOrDefault(e => 
                    e.TryGetProperty("primary", out var primary) && primary.GetBoolean());

                if (primaryEmailData?.TryGetProperty("email", out var email) == true)
                {
                    primaryEmail = email.GetString()!;
                    emailVerified = primaryEmailData?.TryGetProperty("verified", out var verified) == true ? verified.GetBoolean() : false;
                }
            }

            var fullName = userData.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "";
            var nameParts = !string.IsNullOrEmpty(fullName) ? fullName.Split(' ', 2) : new[] { "", "" };

            return new OAuthUserInfo
            {
                ProviderId = userData.GetProperty("id").GetInt64().ToString(),
                ProviderName = Name,
                Email = primaryEmail,
                EmailVerified = emailVerified,
                FullName = fullName,
                FirstName = nameParts.Length > 0 ? nameParts[0] : "",
                LastName = nameParts.Length > 1 ? nameParts[1] : "",
                ProfilePictureUrl = userData.TryGetProperty("avatar_url", out var avatar) ? avatar.GetString() : null,
                Username = userData.TryGetProperty("login", out var login) ? login.GetString() : null,
                Location = userData.TryGetProperty("location", out var location) ? location.GetString() : null,
                AdditionalData = new Dictionary<string, object>
                {
                    ["github_id"] = userData.GetProperty("id").GetInt64(),
                    ["login"] = userData.TryGetProperty("login", out var loginProp) ? loginProp.GetString()! : "",
                    ["company"] = userData.TryGetProperty("company", out var company) ? company.GetString() ?? "" : "",
                    ["blog"] = userData.TryGetProperty("blog", out var blog) ? blog.GetString() ?? "" : ""
                }
            };
        }
        catch (Exception ex) when (!(ex is OAuthProviderException))
        {
            _logger.LogError(ex, "Erro ao obter informações do usuário do GitHub");
            throw new OAuthProviderException(Name, "Falha ao obter informações do usuário", ex);
        }
    }

    public async Task<OAuthTokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        // GitHub não suporta refresh tokens da mesma forma que outros provedores
        throw new NotSupportedException("GitHub OAuth não suporta refresh tokens");
    }

    public async Task<bool> RevokeTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            // GitHub não tem endpoint específico de revogação
            // O token expira automaticamente ou pode ser revogado nas configurações do usuário
            await Task.CompletedTask;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao revogar token do GitHub");
            return false;
        }
    }

    public bool ValidateConfiguration()
    {
        return !string.IsNullOrEmpty(_config.ClientId) &&
               !string.IsNullOrEmpty(_config.ClientSecret) &&
               !string.IsNullOrEmpty(_config.RedirectUrl);
    }
}
```

### 6. Implementar Serviço Principal OAuth

#### src/IDE.Application/Auth/Services/OAuth/IOAuthService.cs
```csharp
using IDE.Application.Auth.Services.OAuth.Models;

namespace IDE.Application.Auth.Services.OAuth;

/// <summary>
/// Interface principal para serviços OAuth
/// </summary>
public interface IOAuthService
{
    /// <summary>
    /// Gera URL de autorização para um provedor
    /// </summary>
    Task<string> GetAuthorizationUrlAsync(string provider, string returnUrl, IEnumerable<string>? scopes = null);
    
    /// <summary>
    /// Processa callback OAuth e realiza login/registro
    /// </summary>
    Task<OAuthLoginResult> HandleCallbackAsync(string provider, string code, string? state = null, string? returnUrl = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Vincula conta OAuth a usuário existente
    /// </summary>
    Task<bool> LinkAccountAsync(Guid userId, string provider, string code, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Remove vinculação de conta OAuth
    /// </summary>
    Task<bool> UnlinkAccountAsync(Guid userId, string provider, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Lista contas OAuth vinculadas ao usuário
    /// </summary>
    Task<List<UserOAuthAccount>> GetLinkedAccountsAsync(Guid userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Renova token OAuth se possível
    /// </summary>
    Task<bool> RefreshTokenAsync(Guid userId, string provider, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Verifica se provedor está disponível e configurado
    /// </summary>
    bool IsProviderAvailable(string provider);
    
    /// <summary>
    /// Lista provedores disponíveis
    /// </summary>
    List<string> GetAvailableProviders();
}
```

#### src/IDE.Application/Auth/Services/OAuth/OAuthService.cs
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using IDE.Application.Auth.Services.Common;
using IDE.Application.Auth.Services.OAuth.Models;
using IDE.Application.Auth.Services.OAuth.Providers;
using IDE.Application.Auth.Services.OAuth.Exceptions;
using IDE.Domain.Entities;
using IDE.Domain.Enums;
using IDE.Infrastructure.Data;

namespace IDE.Application.Auth.Services.OAuth;

/// <summary>
/// Implementação principal do serviço OAuth
/// </summary>
public class OAuthService : IOAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly IServiceProvider _serviceProvider;
    private readonly ICryptoService _cryptoService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IDistributedCache _cache;
    private readonly ILogger<OAuthService> _logger;
    
    private const string StatePrefix = "oauth_state:";
    private const string LinkingTokenPrefix = "oauth_linking:";
    
    private readonly Dictionary<string, Type> _providers = new()
    {
        ["google"] = typeof(GoogleOAuthProvider),
        ["github"] = typeof(GitHubOAuthProvider),
        ["microsoft"] = typeof(MicrosoftOAuthProvider) // Implementar se necessário
    };

    public OAuthService(
        ApplicationDbContext context,
        IServiceProvider serviceProvider,
        ICryptoService cryptoService,
        IDateTimeProvider dateTimeProvider,
        IDistributedCache cache,
        ILogger<OAuthService> logger)
    {
        _context = context;
        _serviceProvider = serviceProvider;
        _cryptoService = cryptoService;
        _dateTimeProvider = dateTimeProvider;
        _cache = cache;
        _logger = logger;
    }

    public async Task<string> GetAuthorizationUrlAsync(string provider, string returnUrl, IEnumerable<string>? scopes = null)
    {
        if (!IsProviderAvailable(provider))
        {
            throw new ArgumentException($"Provedor '{provider}' não está disponível");
        }

        var oauthProvider = GetProvider(provider);
        var state = _cryptoService.GenerateUrlSafeToken(32);
        
        // Armazena state no cache com dados adicionais
        var stateData = new OAuthStateData
        {
            Provider = provider,
            ReturnUrl = returnUrl,
            CreatedAt = _dateTimeProvider.UtcNow
        };

        var cacheKey = StatePrefix + state;
        var cacheValue = JsonSerializer.Serialize(stateData);
        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
        };

        await _cache.SetStringAsync(cacheKey, cacheValue, cacheOptions);

        return oauthProvider.GetAuthorizationUrl(state, scopes);
    }

    public async Task<OAuthLoginResult> HandleCallbackAsync(
        string provider, 
        string code, 
        string? state = null, 
        string? returnUrl = null, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!IsProviderAvailable(provider))
            {
                return new OAuthLoginResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Provedor '{provider}' não está disponível"
                };
            }

            // Valida state se fornecido
            if (!string.IsNullOrEmpty(state))
            {
                var stateValid = await ValidateStateAsync(state, provider);
                if (!stateValid)
                {
                    return new OAuthLoginResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "State inválido ou expirado"
                    };
                }
            }

            var oauthProvider = GetProvider(provider);
            
            // Troca código por token
            var tokenResponse = await oauthProvider.ExchangeCodeForTokenAsync(code, state, cancellationToken);
            
            // Obtém informações do usuário
            var userInfo = await oauthProvider.GetUserInfoAsync(tokenResponse.AccessToken, cancellationToken);

            // Processa login/registro
            return await ProcessOAuthLoginAsync(provider, userInfo, tokenResponse, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante callback OAuth para provedor {Provider}", provider);
            return new OAuthLoginResult
            {
                IsSuccess = false,
                ErrorMessage = "Erro interno durante autenticação OAuth"
            };
        }
    }

    public async Task<bool> LinkAccountAsync(Guid userId, string provider, string code, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!IsProviderAvailable(provider))
            {
                return false;
            }

            var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
            if (user == null) return false;

            var oauthProvider = GetProvider(provider);
            var tokenResponse = await oauthProvider.ExchangeCodeForTokenAsync(code, cancellationToken: cancellationToken);
            var userInfo = await oauthProvider.GetUserInfoAsync(tokenResponse.AccessToken, cancellationToken);

            // Verifica se a conta OAuth já está vinculada a outro usuário
            var existingAccount = await _context.UserOAuthAccounts
                .FirstOrDefaultAsync(a => a.Provider == provider && a.ProviderId == userInfo.ProviderId, cancellationToken);

            if (existingAccount != null && existingAccount.UserId != userId)
            {
                return false; // Conta já vinculada a outro usuário
            }

            // Cria ou atualiza vinculação
            if (existingAccount == null)
            {
                var oauthAccount = new UserOAuthAccount
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Provider = provider,
                    ProviderId = userInfo.ProviderId,
                    Email = userInfo.Email,
                    DisplayName = userInfo.FullName,
                    AccessToken = tokenResponse.AccessToken,
                    RefreshToken = tokenResponse.RefreshToken,
                    TokenExpiresAt = tokenResponse.ExpiresAt,
                    CreatedAt = _dateTimeProvider.UtcNow,
                    UpdatedAt = _dateTimeProvider.UtcNow
                };

                _context.UserOAuthAccounts.Add(oauthAccount);
            }
            else
            {
                // Atualiza tokens
                existingAccount.AccessToken = tokenResponse.AccessToken;
                existingAccount.RefreshToken = tokenResponse.RefreshToken;
                existingAccount.TokenExpiresAt = tokenResponse.ExpiresAt;
                existingAccount.UpdatedAt = _dateTimeProvider.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Conta OAuth {Provider} vinculada ao usuário {UserId}", provider, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao vincular conta OAuth {Provider} ao usuário {UserId}", provider, userId);
            return false;
        }
    }

    public async Task<bool> UnlinkAccountAsync(Guid userId, string provider, CancellationToken cancellationToken = default)
    {
        try
        {
            var account = await _context.UserOAuthAccounts
                .FirstOrDefaultAsync(a => a.UserId == userId && a.Provider == provider, cancellationToken);

            if (account == null) return false;

            // Tenta revogar token no provedor
            if (IsProviderAvailable(provider) && !string.IsNullOrEmpty(account.AccessToken))
            {
                try
                {
                    var oauthProvider = GetProvider(provider);
                    await oauthProvider.RevokeTokenAsync(account.AccessToken, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao revogar token OAuth {Provider} para usuário {UserId}", provider, userId);
                }
            }

            _context.UserOAuthAccounts.Remove(account);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Conta OAuth {Provider} desvinculada do usuário {UserId}", provider, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao desvincular conta OAuth {Provider} do usuário {UserId}", provider, userId);
            return false;
        }
    }

    public async Task<List<UserOAuthAccount>> GetLinkedAccountsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.UserOAuthAccounts
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.Provider)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> RefreshTokenAsync(Guid userId, string provider, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!IsProviderAvailable(provider))
                return false;

            var account = await _context.UserOAuthAccounts
                .FirstOrDefaultAsync(a => a.UserId == userId && a.Provider == provider, cancellationToken);

            if (account?.RefreshToken == null) return false;

            var oauthProvider = GetProvider(provider);
            var newTokens = await oauthProvider.RefreshTokenAsync(account.RefreshToken, cancellationToken);

            account.AccessToken = newTokens.AccessToken;
            if (!string.IsNullOrEmpty(newTokens.RefreshToken))
            {
                account.RefreshToken = newTokens.RefreshToken;
            }
            account.TokenExpiresAt = newTokens.ExpiresAt;
            account.UpdatedAt = _dateTimeProvider.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao renovar token OAuth {Provider} para usuário {UserId}", provider, userId);
            return false;
        }
    }

    public bool IsProviderAvailable(string provider)
    {
        if (!_providers.ContainsKey(provider.ToLowerInvariant()))
            return false;

        var oauthProvider = GetProvider(provider);
        return oauthProvider.ValidateConfiguration();
    }

    public List<string> GetAvailableProviders()
    {
        return _providers.Keys
            .Where(p => IsProviderAvailable(p))
            .ToList();
    }

    // Métodos auxiliares privados
    private IOAuthProvider GetProvider(string provider)
    {
        var providerType = _providers[provider.ToLowerInvariant()];
        return (IOAuthProvider)_serviceProvider.GetService(providerType)!;
    }

    private async Task<bool> ValidateStateAsync(string state, string expectedProvider)
    {
        var cacheKey = StatePrefix + state;
        var cachedValue = await _cache.GetStringAsync(cacheKey);
        
        if (string.IsNullOrEmpty(cachedValue)) return false;

        var stateData = JsonSerializer.Deserialize<OAuthStateData>(cachedValue);
        return stateData?.Provider.Equals(expectedProvider, StringComparison.OrdinalIgnoreCase) == true;
    }

    private async Task<OAuthLoginResult> ProcessOAuthLoginAsync(
        string provider,
        OAuthUserInfo userInfo,
        OAuthTokenResponse tokens,
        CancellationToken cancellationToken)
    {
        // Verifica se usuário já existe por email
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == userInfo.Email, cancellationToken);

        // Verifica se conta OAuth já existe
        var existingOAuthAccount = await _context.UserOAuthAccounts
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Provider == provider && a.ProviderId == userInfo.ProviderId, cancellationToken);

        if (existingOAuthAccount != null)
        {
            // Atualiza tokens da conta OAuth existente
            existingOAuthAccount.AccessToken = tokens.AccessToken;
            existingOAuthAccount.RefreshToken = tokens.RefreshToken;
            existingOAuthAccount.TokenExpiresAt = tokens.ExpiresAt;
            existingOAuthAccount.UpdatedAt = _dateTimeProvider.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            return new OAuthLoginResult
            {
                IsSuccess = true,
                User = existingOAuthAccount.User,
                IsNewUser = false,
                OAuthAccount = existingOAuthAccount,
                UserInfo = userInfo,
                Tokens = tokens
            };
        }

        if (existingUser != null)
        {
            // Usuário existe mas não tem esta conta OAuth vinculada
            // Requer confirmação manual para vincular
            var linkingToken = _cryptoService.GenerateUrlSafeToken(32);
            
            // Armazena dados temporários para vinculação
            var linkingData = new OAuthLinkingData
            {
                UserId = existingUser.Id,
                Provider = provider,
                UserInfo = userInfo,
                Tokens = tokens,
                CreatedAt = _dateTimeProvider.UtcNow
            };

            var cacheKey = LinkingTokenPrefix + linkingToken;
            var cacheValue = JsonSerializer.Serialize(linkingData);
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            };

            await _cache.SetStringAsync(cacheKey, cacheValue, cacheOptions, cancellationToken);

            return new OAuthLoginResult
            {
                IsSuccess = false,
                RequiresLinking = true,
                LinkingToken = linkingToken,
                UserInfo = userInfo,
                ErrorMessage = "Uma conta com este email já existe. Confirme para vincular esta conta OAuth."
            };
        }

        // Criar novo usuário
        var newUser = new User
        {
            Id = Guid.NewGuid(),
            Email = userInfo.Email,
            Username = await GenerateUniqueUsernameAsync(userInfo, cancellationToken),
            FirstName = userInfo.FirstName,
            LastName = userInfo.LastName,
            EmailVerified = userInfo.EmailVerified,
            Status = UserStatus.Active,
            CreatedAt = _dateTimeProvider.UtcNow,
            UpdatedAt = _dateTimeProvider.UtcNow,
            // OAuth users não têm senha local inicialmente
            PasswordHash = null!
        };

        _context.Users.Add(newUser);

        var oauthAccount = new UserOAuthAccount
        {
            Id = Guid.NewGuid(),
            UserId = newUser.Id,
            Provider = provider,
            ProviderId = userInfo.ProviderId,
            Email = userInfo.Email,
            DisplayName = userInfo.FullName,
            AccessToken = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken,
            TokenExpiresAt = tokens.ExpiresAt,
            CreatedAt = _dateTimeProvider.UtcNow,
            UpdatedAt = _dateTimeProvider.UtcNow
        };

        _context.UserOAuthAccounts.Add(oauthAccount);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Novo usuário criado via OAuth {Provider}: {UserId} - {Email}", provider, newUser.Id, newUser.Email);

        return new OAuthLoginResult
        {
            IsSuccess = true,
            User = newUser,
            IsNewUser = true,
            OAuthAccount = oauthAccount,
            UserInfo = userInfo,
            Tokens = tokens
        };
    }

    private async Task<string> GenerateUniqueUsernameAsync(OAuthUserInfo userInfo, CancellationToken cancellationToken)
    {
        var baseUsername = userInfo.Username ?? 
                          userInfo.FirstName?.ToLowerInvariant() ?? 
                          userInfo.Email.Split('@')[0].ToLowerInvariant();

        // Remove caracteres inválidos
        baseUsername = new string(baseUsername.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '.').ToArray());
        
        if (string.IsNullOrEmpty(baseUsername))
        {
            baseUsername = "user";
        }

        var username = baseUsername;
        var counter = 1;

        while (await _context.Users.AnyAsync(u => u.Username == username, cancellationToken))
        {
            username = $"{baseUsername}{counter}";
            counter++;
        }

        return username;
    }

    /// <summary>
    /// Dados para state OAuth temporário
    /// </summary>
    private class OAuthStateData
    {
        public string Provider { get; set; } = string.Empty;
        public string ReturnUrl { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Dados para vinculação OAuth temporária
    /// </summary>
    private class OAuthLinkingData
    {
        public Guid UserId { get; set; }
        public string Provider { get; set; } = string.Empty;
        public OAuthUserInfo UserInfo { get; set; } = new();
        public OAuthTokenResponse Tokens { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }
}
```

### 7. Validar Implementação

Execute os comandos para validar:

```powershell
# Na raiz do projeto
dotnet restore
dotnet build

# Verificar se não há erros de compilação
dotnet build --verbosity normal
```

## ✅ Critérios de Validação

Ao final desta parte, você deve ter:

- [ ] **Provedores OAuth** (Google, GitHub) implementados
- [ ] **Serviço OAuth principal** completo
- [ ] **Models OAuth** bem estruturados
- [ ] **Fluxo de autorização** funcionando
- [ ] **Vinculação/desvinculação** de contas
- [ ] **Tratamento de erros** adequado
- [ ] **Compilação bem-sucedida** sem erros

## 📝 Arquivos Criados

Esta parte criará aproximadamente **14 arquivos**:
- 4 Models OAuth
- 2 Classes de exceção
- 1 Interface base de provedor
- 3 Provedores OAuth específicos
- 2 Serviços OAuth principais
- 2 Classes auxiliares

## 🔄 Próximos Passos

Após concluir esta parte, você estará pronto para:
- **Parte 10**: Sistema completo de Email
- Configurar middleware e infraestrutura
- Implementar endpoints da API

## 🚨 Troubleshooting Comum

**OAuth callback falha**: Verificar URLs de redirecionamento  
**Token inválido**: Verificar configurações de Client ID/Secret  
**User info não retorna**: Verificar escopos solicitados  
**State inválido**: Verificar configuração do cache Redis  

---
**⏱️ Tempo estimado**: 30-45 minutos  
**🎯 Próxima parte**: 10-sistema-email-completo.md  
**📋 Dependências**: Partes 1-8 concluídas