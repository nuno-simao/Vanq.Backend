# Parte 5: DTOs e Requests de Autenticação

## 📋 Visão Geral
**Duração**: 20-25 minutos  
**Complexidade**: Baixa-Média  
**Dependências**: Partes 1-4 (Setup + Entidades + EF)

Esta parte implementa todos os DTOs, requests e responses para o sistema de autenticação, incluindo configuração do AutoMapper para conversões automáticas.

## 🎯 Objetivos
- ✅ Criar todos os DTOs de resposta (UserDto, AuthenticationResult, etc.)
- ✅ Implementar todos os requests de entrada (LoginRequest, RegisterRequest, etc.)
- ✅ Configurar AutoMapper profiles para conversões
- ✅ Implementar responses padronizadas (ApiResponse)
- ✅ Criar DTOs para API Keys e 2FA

## 📁 Arquivos a serem Criados

```
src/IDE.Application/Auth/
├── DTOs/
│   ├── AuthenticationResult.cs
│   ├── UserDto.cs
│   ├── ApiKeyDto.cs
│   └── TwoFactorSetupResult.cs
├── Requests/
│   ├── LoginRequest.cs
│   ├── RegisterRequest.cs
│   ├── RefreshTokenRequest.cs
│   ├── PasswordResetRequest.cs
│   ├── PasswordResetConfirmRequest.cs
│   ├── EmailVerificationRequest.cs
│   ├── ApiKeyCreateRequest.cs
│   ├── TwoFactorSetupRequest.cs
│   └── OAuthLoginRequest.cs
└── Profiles/
    ├── UserProfile.cs
    └── AuthProfile.cs
src/IDE.Shared/Common/
├── ApiResponse.cs
└── PaginatedResponse.cs
```

## 🚀 Execução Passo a Passo

### 1. Criar DTOs de Resposta

#### src/IDE.Application/Auth/DTOs/AuthenticationResult.cs
```csharp
namespace IDE.Application.Auth.DTOs;

/// <summary>
/// Resultado de uma operação de autenticação
/// </summary>
public class AuthenticationResult
{
    /// <summary>
    /// Indica se a autenticação foi bem-sucedida
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Token de acesso JWT
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Token de refresh para renovação
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// Data de expiração do access token
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Dados do usuário autenticado
    /// </summary>
    public UserDto? User { get; set; }

    /// <summary>
    /// Lista de erros de validação ou autenticação
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Indica se é necessário fornecer código de dois fatores
    /// </summary>
    public bool RequiresTwoFactor { get; set; } = false;

    /// <summary>
    /// Token temporário para completar autenticação 2FA
    /// </summary>
    public string? TwoFactorToken { get; set; }

    /// <summary>
    /// Método de dois fatores configurado (para exibição)
    /// </summary>
    public string? TwoFactorMethod { get; set; }

    // Métodos auxiliares

    /// <summary>
    /// Cria resultado de sucesso
    /// </summary>
    public static AuthenticationResult Success(
        string accessToken, 
        string refreshToken, 
        DateTime expiresAt, 
        UserDto user)
    {
        return new AuthenticationResult
        {
            Success = true,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            User = user
        };
    }

    /// <summary>
    /// Cria resultado de falha
    /// </summary>
    public static AuthenticationResult Failure(params string[] errors)
    {
        return new AuthenticationResult
        {
            Success = false,
            Errors = errors.ToList()
        };
    }

    /// <summary>
    /// Cria resultado que requer 2FA
    /// </summary>
    public static AuthenticationResult RequiresTwoFactorAuth(
        string twoFactorToken, 
        string method)
    {
        return new AuthenticationResult
        {
            Success = false,
            RequiresTwoFactor = true,
            TwoFactorToken = twoFactorToken,
            TwoFactorMethod = method
        };
    }
}
```

#### src/IDE.Application/Auth/DTOs/UserDto.cs
```csharp
using IDE.Domain.Enums;

namespace IDE.Application.Auth.DTOs;

/// <summary>
/// DTO para dados do usuário (sem informações sensíveis)
/// </summary>
public class UserDto
{
    /// <summary>
    /// Identificador único do usuário
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Email do usuário
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Nome de usuário
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Primeiro nome
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Último nome
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Nome completo
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// URL do avatar
    /// </summary>
    public string Avatar { get; set; } = string.Empty;

    /// <summary>
    /// Provedor do avatar
    /// </summary>
    public string AvatarProvider { get; set; } = string.Empty;

    /// <summary>
    /// Indica se o email foi verificado
    /// </summary>
    public bool EmailVerified { get; set; }

    /// <summary>
    /// Data de verificação do email
    /// </summary>
    public DateTime? EmailVerifiedAt { get; set; }

    /// <summary>
    /// Plano do usuário
    /// </summary>
    public UserPlan Plan { get; set; }

    /// <summary>
    /// Nome do plano (para exibição)
    /// </summary>
    public string PlanName { get; set; } = string.Empty;

    /// <summary>
    /// Indica se 2FA está habilitado
    /// </summary>
    public bool TwoFactorEnabled { get; set; }

    /// <summary>
    /// Método de 2FA ativo
    /// </summary>
    public TwoFactorMethod TwoFactorMethod { get; set; }

    /// <summary>
    /// Nome do método 2FA (para exibição)
    /// </summary>
    public string TwoFactorMethodName { get; set; } = string.Empty;

    /// <summary>
    /// Data de criação da conta
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Data do último login
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Estatísticas do usuário
    /// </summary>
    public UserStatsDto Stats { get; set; } = new();
}

/// <summary>
/// Estatísticas do usuário
/// </summary>
public class UserStatsDto
{
    /// <summary>
    /// Número de workspaces
    /// </summary>
    public int WorkspaceCount { get; set; } = 0;

    /// <summary>
    /// Número de API Keys ativas
    /// </summary>
    public int ActiveApiKeysCount { get; set; } = 0;

    /// <summary>
    /// Último login de localização suspeita
    /// </summary>
    public bool HasSuspiciousActivity { get; set; } = false;

    /// <summary>
    /// Conta está próxima dos limites do plano
    /// </summary>
    public bool NearPlanLimits { get; set; } = false;
}
```

#### src/IDE.Application/Auth/DTOs/ApiKeyDto.cs
```csharp
namespace IDE.Application.Auth.DTOs;

/// <summary>
/// DTO para API Keys (sem dados sensíveis como hash)
/// </summary>
public class ApiKeyDto
{
    /// <summary>
    /// Identificador único da API Key
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Nome descritivo da API Key
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Chave pública (sk_xxxxx) - apenas no momento da criação
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// Últimos 4 caracteres da chave para identificação
    /// </summary>
    public string KeySuffix { get; set; } = string.Empty;

    /// <summary>
    /// Data de expiração
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Indica se está ativa
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Indica se está expirada
    /// </summary>
    public bool IsExpired { get; set; }

    /// <summary>
    /// Data de criação
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Data da última utilização
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// IP do último uso
    /// </summary>
    public string LastUsedIp { get; set; } = string.Empty;

    /// <summary>
    /// Contador de uso
    /// </summary>
    public int UsageCount { get; set; }

    /// <summary>
    /// Escopos/permissões da chave
    /// </summary>
    public List<string> Scopes { get; set; } = new();

    /// <summary>
    /// Status da chave para exibição
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Dias até expiração
    /// </summary>
    public int DaysUntilExpiration { get; set; }

    /// <summary>
    /// Indica se está próxima da expiração
    /// </summary>
    public bool IsNearExpiration { get; set; }
}
```

#### src/IDE.Application/Auth/DTOs/TwoFactorSetupResult.cs
```csharp
using IDE.Domain.Enums;

namespace IDE.Application.Auth.DTOs;

/// <summary>
/// Resultado do setup de autenticação de dois fatores
/// </summary>
public class TwoFactorSetupResult
{
    /// <summary>
    /// Indica se o setup foi bem-sucedido
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Segredo TOTP para configuração no app autenticador
    /// </summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// QR Code em Base64 para facilitar configuração
    /// </summary>
    public string QrCodeBase64 { get; set; } = string.Empty;

    /// <summary>
    /// URL para configuração manual
    /// </summary>
    public string ManualEntryUrl { get; set; } = string.Empty;

    /// <summary>
    /// Lista de códigos de recuperação
    /// </summary>
    public List<string> RecoveryCodes { get; set; } = new();

    /// <summary>
    /// Método de 2FA sendo configurado
    /// </summary>
    public TwoFactorMethod Method { get; set; }

    /// <summary>
    /// Instruções específicas para o método
    /// </summary>
    public string Instructions { get; set; } = string.Empty;

    /// <summary>
    /// Lista de erros, se houver
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Cria resultado de sucesso para TOTP
    /// </summary>
    public static TwoFactorSetupResult SuccessTotp(
        string secret, 
        string qrCode, 
        string manualUrl, 
        List<string> recoveryCodes)
    {
        return new TwoFactorSetupResult
        {
            Success = true,
            Secret = secret,
            QrCodeBase64 = qrCode,
            ManualEntryUrl = manualUrl,
            RecoveryCodes = recoveryCodes,
            Method = TwoFactorMethod.Totp,
            Instructions = "Escaneie o QR Code com seu app autenticador (Google Authenticator, Authy, etc.) ou insira o código manualmente."
        };
    }

    /// <summary>
    /// Cria resultado de falha
    /// </summary>
    public static TwoFactorSetupResult Failure(params string[] errors)
    {
        return new TwoFactorSetupResult
        {
            Success = false,
            Errors = errors.ToList()
        };
    }
}
```

### 2. Criar Requests de Entrada

#### src/IDE.Application/Auth/Requests/RegisterRequest.cs
```csharp
namespace IDE.Application.Auth.Requests;

/// <summary>
/// Request para registro de novo usuário
/// </summary>
public class RegisterRequest
{
    /// <summary>
    /// Email do usuário
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Nome de usuário desejado
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Senha
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Confirmação da senha
    /// </summary>
    public string PasswordConfirm { get; set; } = string.Empty;

    /// <summary>
    /// Primeiro nome
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Último nome
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Aceita termos de uso
    /// </summary>
    public bool AcceptTerms { get; set; } = false;

    /// <summary>
    /// Aceita receber emails de marketing
    /// </summary>
    public bool AcceptMarketing { get; set; } = false;

    /// <summary>
    /// Token de captcha (se necessário)
    /// </summary>
    public string? CaptchaToken { get; set; }

    /// <summary>
    /// Código de convite/referência (opcional)
    /// </summary>
    public string? InviteCode { get; set; }
}
```

#### src/IDE.Application/Auth/Requests/LoginRequest.cs
```csharp
namespace IDE.Application.Auth.Requests;

/// <summary>
/// Request para login de usuário
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// Email ou nome de usuário
    /// </summary>
    public string EmailOrUsername { get; set; } = string.Empty;

    /// <summary>
    /// Senha
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Manter conectado (refresh token de longa duração)
    /// </summary>
    public bool RememberMe { get; set; } = false;

    /// <summary>
    /// Informações do dispositivo
    /// </summary>
    public string DeviceInfo { get; set; } = string.Empty;

    /// <summary>
    /// Código de dois fatores (TOTP ou email)
    /// </summary>
    public string? TwoFactorCode { get; set; }

    /// <summary>
    /// Token de 2FA (para continuação de login)
    /// </summary>
    public string? TwoFactorToken { get; set; }

    /// <summary>
    /// Código de recuperação 2FA
    /// </summary>
    public string? RecoveryCode { get; set; }

    /// <summary>
    /// Token de captcha (se necessário)
    /// </summary>
    public string? CaptchaToken { get; set; }
}
```

#### src/IDE.Application/Auth/Requests/RefreshTokenRequest.cs
```csharp
namespace IDE.Application.Auth.Requests;

/// <summary>
/// Request para renovar token JWT
/// </summary>
public class RefreshTokenRequest
{
    /// <summary>
    /// Refresh token atual
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// Access token expirado (opcional, para validação)
    /// </summary>
    public string? ExpiredAccessToken { get; set; }

    /// <summary>
    /// Informações do dispositivo (para auditoria)
    /// </summary>
    public string? DeviceInfo { get; set; }
}
```

#### src/IDE.Application/Auth/Requests/PasswordResetRequest.cs
```csharp
namespace IDE.Application.Auth.Requests;

/// <summary>
/// Request para solicitar reset de senha
/// </summary>
public class PasswordResetRequest
{
    /// <summary>
    /// Email do usuário
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Token de captcha (se necessário)
    /// </summary>
    public string? CaptchaToken { get; set; }
}
```

#### src/IDE.Application/Auth/Requests/PasswordResetConfirmRequest.cs
```csharp
namespace IDE.Application.Auth.Requests;

/// <summary>
/// Request para confirmar reset de senha com token
/// </summary>
public class PasswordResetConfirmRequest
{
    /// <summary>
    /// Token de reset recebido por email
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Nova senha
    /// </summary>
    public string NewPassword { get; set; } = string.Empty;

    /// <summary>
    /// Confirmação da nova senha
    /// </summary>
    public string NewPasswordConfirm { get; set; } = string.Empty;
}
```

#### src/IDE.Application/Auth/Requests/EmailVerificationRequest.cs
```csharp
namespace IDE.Application.Auth.Requests;

/// <summary>
/// Request para verificar email
/// </summary>
public class EmailVerificationRequest
{
    /// <summary>
    /// Token de verificação recebido por email
    /// </summary>
    public string Token { get; set; } = string.Empty;
}

/// <summary>
/// Request para reenviar email de verificação
/// </summary>
public class ResendEmailVerificationRequest
{
    /// <summary>
    /// Email para reenvio
    /// </summary>
    public string Email { get; set; } = string.Empty;
}
```

#### src/IDE.Application/Auth/Requests/ApiKeyCreateRequest.cs
```csharp
namespace IDE.Application.Auth.Requests;

/// <summary>
/// Request para criar nova API Key
/// </summary>
public class ApiKeyCreateRequest
{
    /// <summary>
    /// Nome descritivo da API Key
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Data de expiração (opcional)
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Escopos/permissões desejados
    /// </summary>
    public List<string> Scopes { get; set; } = new();

    /// <summary>
    /// Descrição adicional
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Request para atualizar API Key
/// </summary>
public class ApiKeyUpdateRequest
{
    /// <summary>
    /// Novo nome (opcional)
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Ativar/desativar
    /// </summary>
    public bool? IsActive { get; set; }

    /// <summary>
    /// Novos escopos
    /// </summary>
    public List<string>? Scopes { get; set; }
}
```

#### src/IDE.Application/Auth/Requests/TwoFactorSetupRequest.cs
```csharp
using IDE.Domain.Enums;

namespace IDE.Application.Auth.Requests;

/// <summary>
/// Request para iniciar setup de 2FA
/// </summary>
public class TwoFactorSetupRequest
{
    /// <summary>
    /// Método de 2FA desejado
    /// </summary>
    public TwoFactorMethod Method { get; set; }
}

/// <summary>
/// Request para habilitar 2FA após setup
/// </summary>
public class EnableTwoFactorRequest
{
    /// <summary>
    /// Método configurado
    /// </summary>
    public TwoFactorMethod Method { get; set; }

    /// <summary>
    /// Código de verificação do app/email
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Senha atual para confirmação
    /// </summary>
    public string CurrentPassword { get; set; } = string.Empty;
}

/// <summary>
/// Request para desabilitar 2FA
/// </summary>
public class DisableTwoFactorRequest
{
    /// <summary>
    /// Código atual ou código de recuperação
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Senha atual para confirmação
    /// </summary>
    public string CurrentPassword { get; set; } = string.Empty;
}
```

#### src/IDE.Application/Auth/Requests/OAuthLoginRequest.cs
```csharp
namespace IDE.Application.Auth.Requests;

/// <summary>
/// Request para login via OAuth
/// </summary>
public class OAuthLoginRequest
{
    /// <summary>
    /// Nome do provedor (Google, GitHub, Microsoft)
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Código de autorização do OAuth
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// URL de redirect configurada
    /// </summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>
    /// State para validação CSRF
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// Informações do dispositivo
    /// </summary>
    public string? DeviceInfo { get; set; }
}
```

### 3. Criar Responses Padronizadas

#### src/IDE.Shared/Common/ApiResponse.cs
```csharp
using System.Text.Json.Serialization;

namespace IDE.Shared.Common;

/// <summary>
/// Response padrão da API
/// </summary>
/// <typeparam name="T">Tipo dos dados</typeparam>
public class ApiResponse<T>
{
    /// <summary>
    /// Indica se a operação foi bem-sucedida
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Mensagem de retorno
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Dados retornados
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// Lista de erros de validação
    /// </summary>
    public List<ValidationError> Errors { get; set; } = new();

    /// <summary>
    /// Timestamp da resposta
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// ID único da requisição para rastreamento
    /// </summary>
    public string RequestId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Metadados adicionais
    /// </summary>
    public Dictionary<string, object>? Meta { get; set; }

    // Métodos estáticos para facilitar criação

    /// <summary>
    /// Cria response de sucesso
    /// </summary>
    public static ApiResponse<T> Ok(T data, string message = "Success")
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data
        };
    }

    /// <summary>
    /// Cria response de sucesso sem dados
    /// </summary>
    public static ApiResponse<object> Ok(string message = "Success")
    {
        return new ApiResponse<object>
        {
            Success = true,
            Message = message
        };
    }

    /// <summary>
    /// Cria response de erro
    /// </summary>
    public static ApiResponse<T> Error(string message, List<ValidationError>? errors = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message,
            Errors = errors ?? new List<ValidationError>()
        };
    }

    /// <summary>
    /// Cria response de erro de validação
    /// </summary>
    public static ApiResponse<T> ValidationError(List<ValidationError> errors)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = "Validation failed",
            Errors = errors
        };
    }
}

/// <summary>
/// Response sem dados específicos
/// </summary>
public class ApiResponse : ApiResponse<object>
{
}

/// <summary>
/// Erro de validação
/// </summary>
public class ValidationError
{
    /// <summary>
    /// Campo que contém erro
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// Mensagem do erro
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Código do erro
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// Valor que causou o erro
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? AttemptedValue { get; set; }

    public ValidationError() { }

    public ValidationError(string field, string message, string? code = null)
    {
        Field = field;
        Message = message;
        Code = code;
    }
}
```

#### src/IDE.Shared/Common/PaginatedResponse.cs
```csharp
namespace IDE.Shared.Common;

/// <summary>
/// Response paginado
/// </summary>
/// <typeparam name="T">Tipo dos dados</typeparam>
public class PaginatedResponse<T> : ApiResponse<List<T>>
{
    /// <summary>
    /// Página atual (1-based)
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Itens por página
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Total de itens
    /// </summary>
    public long TotalItems { get; set; }

    /// <summary>
    /// Total de páginas
    /// </summary>
    public int TotalPages { get; set; }

    /// <summary>
    /// Indica se tem página anterior
    /// </summary>
    public bool HasPreviousPage { get; set; }

    /// <summary>
    /// Indica se tem próxima página
    /// </summary>
    public bool HasNextPage { get; set; }

    /// <summary>
    /// Número da primeira página
    /// </summary>
    public int FirstPage { get; set; } = 1;

    /// <summary>
    /// Número da última página
    /// </summary>
    public int LastPage { get; set; }

    /// <summary>
    /// Cria response paginado
    /// </summary>
    public static PaginatedResponse<T> Create(
        List<T> data,
        int page,
        int pageSize,
        long totalItems,
        string message = "Success")
    {
        var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

        return new PaginatedResponse<T>
        {
            Success = true,
            Message = message,
            Data = data,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages,
            HasPreviousPage = page > 1,
            HasNextPage = page < totalPages,
            LastPage = Math.Max(totalPages, 1)
        };
    }

    /// <summary>
    /// Cria response paginado vazio
    /// </summary>
    public static PaginatedResponse<T> Empty(int page = 1, int pageSize = 10)
    {
        return Create(new List<T>(), page, pageSize, 0, "No data found");
    }
}

/// <summary>
/// Parâmetros de paginação
/// </summary>
public class PaginationRequest
{
    private int _page = 1;
    private int _pageSize = 10;

    /// <summary>
    /// Página solicitada (1-based)
    /// </summary>
    public int Page
    {
        get => _page;
        set => _page = Math.Max(1, value);
    }

    /// <summary>
    /// Itens por página
    /// </summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = Math.Clamp(value, 1, 100); // Max 100 itens por página
    }

    /// <summary>
    /// Campo para ordenação
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// Direção da ordenação
    /// </summary>
    public SortDirection SortDirection { get; set; } = SortDirection.Asc;

    /// <summary>
    /// Filtro de busca
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Calcula quantos itens pular
    /// </summary>
    public int Skip => (Page - 1) * PageSize;
}

/// <summary>
/// Direção da ordenação
/// </summary>
public enum SortDirection
{
    Asc = 0,
    Desc = 1
}
```

### 4. Criar AutoMapper Profiles

#### src/IDE.Application/Auth/Profiles/UserProfile.cs
```csharp
using AutoMapper;
using IDE.Application.Auth.DTOs;
using IDE.Domain.Entities;
using IDE.Domain.Enums;

namespace IDE.Application.Auth.Profiles;

/// <summary>
/// Profile do AutoMapper para User
/// </summary>
public class UserProfile : Profile
{
    public UserProfile()
    {
        // User -> UserDto
        CreateMap<User, UserDto>()
            .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.FullName))
            .ForMember(dest => dest.PlanName, opt => opt.MapFrom(src => GetPlanName(src.Plan)))
            .ForMember(dest => dest.TwoFactorMethodName, opt => opt.MapFrom(src => GetTwoFactorMethodName(src.TwoFactorMethod)))
            .ForMember(dest => dest.Stats, opt => opt.Ignore()) // Será preenchido separadamente
            .AfterMap((src, dest, context) =>
            {
                // Preencher estatísticas básicas
                dest.Stats = new UserStatsDto
                {
                    ActiveApiKeysCount = src.ApiKeys?.Count(ak => ak.IsValid) ?? 0,
                    HasSuspiciousActivity = src.LoginHistory?.Any(lh => lh.IsSuspicious) ?? false
                };
            });

        // RegisterRequest -> User
        CreateMap<Requests.RegisterRequest, User>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.PasswordHash, opt => opt.Ignore())
            .ForMember(dest => dest.EmailVerified, opt => opt.MapFrom(src => false))
            .ForMember(dest => dest.Plan, opt => opt.MapFrom(src => UserPlan.Free))
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
            .ForMember(dest => dest.Avatar, opt => opt.MapFrom(src => GenerateDefaultAvatar(src.Email)))
            .ForMember(dest => dest.AvatarProvider, opt => opt.MapFrom(src => "Default"));
    }

    /// <summary>
    /// Obtém nome amigável do plano
    /// </summary>
    private static string GetPlanName(UserPlan plan)
    {
        return plan switch
        {
            UserPlan.Free => "Gratuito",
            UserPlan.Premium => "Premium",
            UserPlan.Enterprise => "Empresarial",
            _ => "Desconhecido"
        };
    }

    /// <summary>
    /// Obtém nome amigável do método 2FA
    /// </summary>
    private static string GetTwoFactorMethodName(TwoFactorMethod method)
    {
        return method switch
        {
            TwoFactorMethod.None => "Desabilitado",
            TwoFactorMethod.Totp => "App Autenticador",
            TwoFactorMethod.Email => "Email",
            _ => "Desconhecido"
        };
    }

    /// <summary>
    /// Gera avatar padrão baseado no email
    /// </summary>
    private static string GenerateDefaultAvatar(string email)
    {
        // Gerar avatar usando Gravatar ou serviço similar
        var hash = System.Security.Cryptography.MD5.HashData(
            System.Text.Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()));
        var hashString = Convert.ToHexString(hash).ToLowerInvariant();
        
        return $"https://www.gravatar.com/avatar/{hashString}?d=identicon&s=200";
    }
}
```

#### src/IDE.Application/Auth/Profiles/AuthProfile.cs
```csharp
using AutoMapper;
using IDE.Application.Auth.DTOs;
using IDE.Domain.Entities;
using System.Text.Json;

namespace IDE.Application.Auth.Profiles;

/// <summary>
/// Profile do AutoMapper para entidades de autenticação
/// </summary>
public class AuthProfile : Profile
{
    public AuthProfile()
    {
        // ApiKey -> ApiKeyDto
        CreateMap<ApiKey, ApiKeyDto>()
            .ForMember(dest => dest.Key, opt => opt.Ignore()) // Só mostrar na criação
            .ForMember(dest => dest.KeySuffix, opt => opt.MapFrom(src => GetKeySuffix(src.Key)))
            .ForMember(dest => dest.IsExpired, opt => opt.MapFrom(src => src.IsExpired))
            .ForMember(dest => dest.Scopes, opt => opt.MapFrom(src => ParseScopes(src.Scopes)))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => GetApiKeyStatus(src)))
            .ForMember(dest => dest.DaysUntilExpiration, opt => opt.MapFrom(src => GetDaysUntilExpiration(src.ExpiresAt)))
            .ForMember(dest => dest.IsNearExpiration, opt => opt.MapFrom(src => src.IsNearExpiration));

        // ApiKeyCreateRequest -> ApiKey (será usado no service)
        CreateMap<Requests.ApiKeyCreateRequest, ApiKey>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.Key, opt => opt.Ignore())
            .ForMember(dest => dest.KeyHash, opt => opt.Ignore())
            .ForMember(dest => dest.UserId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => true))
            .ForMember(dest => dest.Scopes, opt => opt.MapFrom(src => JsonSerializer.Serialize(src.Scopes)))
            .ForMember(dest => dest.ExpiresAt, opt => opt.MapFrom(src => 
                src.ExpiresAt ?? DateTime.UtcNow.AddDays(90)));

        // UserLoginHistory -> UserLoginHistoryDto (se necessário)
        CreateMap<UserLoginHistory, UserLoginHistoryDto>()
            .ForMember(dest => dest.LocationString, opt => opt.MapFrom(src => FormatLocation(src.Country, src.City)))
            .ForMember(dest => dest.DeviceString, opt => opt.MapFrom(src => FormatDevice(src.UserAgent)))
            .ForMember(dest => dest.StatusString, opt => opt.MapFrom(src => src.IsSuccess ? "Sucesso" : "Falha"))
            .ForMember(dest => dest.RiskLevelString, opt => opt.MapFrom(src => GetRiskLevelString(src.RiskScore)));
    }

    /// <summary>
    /// Obtém últimos 4 caracteres da chave
    /// </summary>
    private static string GetKeySuffix(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length < 4)
            return "****";
        
        return $"***{key[^4..]}";
    }

    /// <summary>
    /// Converte JSON de scopes para lista
    /// </summary>
    private static List<string> ParseScopes(string scopesJson)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(scopesJson) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// Obtém status da API Key para exibição
    /// </summary>
    private static string GetApiKeyStatus(ApiKey apiKey)
    {
        if (!apiKey.IsActive) return "Inativa";
        if (apiKey.IsExpired) return "Expirada";
        if (apiKey.IsNearExpiration) return "Próxima do Vencimento";
        return "Ativa";
    }

    /// <summary>
    /// Calcula dias até expiração
    /// </summary>
    private static int GetDaysUntilExpiration(DateTime expiresAt)
    {
        var days = (expiresAt - DateTime.UtcNow).Days;
        return Math.Max(0, days);
    }

    /// <summary>
    /// Formatar localização para exibição
    /// </summary>
    private static string FormatLocation(string country, string city)
    {
        if (string.IsNullOrEmpty(country) && string.IsNullOrEmpty(city))
            return "Localização desconhecida";
        
        if (string.IsNullOrEmpty(city))
            return country;
        
        if (string.IsNullOrEmpty(country))
            return city;
        
        return $"{city}, {country}";
    }

    /// <summary>
    /// Extrair informações básicas do User Agent
    /// </summary>
    private static string FormatDevice(string userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
            return "Dispositivo desconhecido";
        
        // Extrair informações básicas do User Agent
        if (userAgent.Contains("Chrome")) return "Chrome Browser";
        if (userAgent.Contains("Firefox")) return "Firefox Browser";
        if (userAgent.Contains("Safari")) return "Safari Browser";
        if (userAgent.Contains("Edge")) return "Edge Browser";
        if (userAgent.Contains("Mobile")) return "Mobile Device";
        
        return "Desktop Browser";
    }

    /// <summary>
    /// Converter score de risco em string
    /// </summary>
    private static string GetRiskLevelString(int riskScore)
    {
        return riskScore switch
        {
            < 20 => "Baixo",
            < 50 => "Médio",
            < 80 => "Alto",
            _ => "Muito Alto"
        };
    }
}

/// <summary>
/// DTO para histórico de login (se necessário)
/// </summary>
public class UserLoginHistoryDto
{
    public Guid Id { get; set; }
    public DateTime LoginAt { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string LocationString { get; set; } = string.Empty;
    public string DeviceString { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public string StatusString { get; set; } = string.Empty;
    public string FailureReason { get; set; } = string.Empty;
    public string LoginMethod { get; set; } = string.Empty;
    public bool IsSuspicious { get; set; }
    public int RiskScore { get; set; }
    public string RiskLevelString { get; set; } = string.Empty;
}
```

### 5. Validar Implementação

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

- [ ] **DTOs de resposta** completos para todas as operações
- [ ] **Requests de entrada** com todas as propriedades necessárias
- [ ] **AutoMapper profiles** configurados corretamente
- [ ] **ApiResponse** padronizado implementado
- [ ] **PaginatedResponse** para listas funcionando
- [ ] **Compilação bem-sucedida** sem erros ou warnings
- [ ] **Namespaces** organizados corretamente

## 📝 Arquivos Criados

Esta parte criará aproximadamente **15 arquivos**:
- 4 DTOs de resposta
- 8 Requests de entrada
- 2 Responses padronizadas
- 2 AutoMapper profiles

## 🔄 Próximos Passos

Após concluir esta parte, você estará pronto para:
- **Parte 6**: Validações e Regras de Negócio
- Implementar FluentValidation validators
- Configurar regras de validação personalizadas

## 🚨 Troubleshooting Comum

**Erros de mapeamento**: Verifique se todas as propriedades estão mapeadas corretamente  
**Namespaces**: Certifique-se de que todos os using statements estão corretos  
**AutoMapper**: Os profiles serão registrados automaticamente pelo DI  

---
**⏱️ Tempo estimado**: 20-25 minutos  
**🎯 Próxima parte**: 06-validacoes-regras-negocio.md  
**📋 Dependências**: Partes 1-4 concluídas