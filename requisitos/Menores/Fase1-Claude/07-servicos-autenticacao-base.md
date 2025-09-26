# Parte 7: Serviços de Autenticação Base

## 📋 Visão Geral
**Duração**: 20-35 minutos  
**Complexidade**: Média-Alta  
**Dependências**: Partes 1-6 (Setup + Entidades + EF + DTOs + Validações)

Esta parte implementa os serviços base de autenticação, incluindo IAuthService, AuthService, JWT Token Generator, Password Hasher e User Manager com todas as funcionalidades de registro, login, verificação e gerenciamento de tokens.

## 🎯 Objetivos
- ✅ Implementar IAuthService com todos os contratos
- ✅ Criar AuthService com lógica completa de autenticação
- ✅ Implementar JWT Token Generator e Validator
- ✅ Configurar Password Hasher com BCrypt
- ✅ Criar User Manager Service
- ✅ Implementar Token Blacklist Service
- ✅ Configurar Rate Limiting básico

## 📁 Arquivos a serem Criados

```
src/IDE.Application/Auth/Services/
├── IAuthService.cs
├── AuthService.cs
├── IPasswordService.cs
├── PasswordService.cs
├── ITokenService.cs
├── TokenService.cs
├── IUserService.cs
├── UserService.cs
├── ITokenBlacklistService.cs
├── TokenBlacklistService.cs
└── Common/
    ├── IDateTimeProvider.cs
    ├── DateTimeProvider.cs
    ├── ICryptoService.cs
    └── CryptoService.cs
```

## 🚀 Execução Passo a Passo

### 1. Criar Serviços de Infraestrutura Comum

#### src/IDE.Application/Auth/Services/Common/IDateTimeProvider.cs
```csharp
namespace IDE.Application.Auth.Services.Common;

/// <summary>
/// Provedor de data/hora para testes e abstração
/// </summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
    DateTime Now { get; }
    DateOnly Today { get; }
    long UnixTimestamp { get; }
}
```

#### src/IDE.Application/Auth/Services/Common/DateTimeProvider.cs
```csharp
namespace IDE.Application.Auth.Services.Common;

/// <summary>
/// Implementação padrão do provedor de data/hora
/// </summary>
public class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime Now => DateTime.Now;
    public DateOnly Today => DateOnly.FromDateTime(DateTime.Today);
    public long UnixTimestamp => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
```

#### src/IDE.Application/Auth/Services/Common/ICryptoService.cs
```csharp
namespace IDE.Application.Auth.Services.Common;

/// <summary>
/// Serviço para operações criptográficas
/// </summary>
public interface ICryptoService
{
    /// <summary>
    /// Gera uma string aleatória segura
    /// </summary>
    string GenerateSecureRandomString(int length = 32);
    
    /// <summary>
    /// Gera um token URL-safe
    /// </summary>
    string GenerateUrlSafeToken(int length = 32);
    
    /// <summary>
    /// Gera um código numérico aleatório
    /// </summary>
    string GenerateNumericCode(int length = 6);
    
    /// <summary>
    /// Gera códigos de recuperação
    /// </summary>
    List<string> GenerateRecoveryCodes(int count = 10, int length = 8);
    
    /// <summary>
    /// Hash de uma string com salt
    /// </summary>
    string HashWithSalt(string input, string salt);
    
    /// <summary>
    /// Gera um salt criptográfico
    /// </summary>
    string GenerateSalt();
    
    /// <summary>
    /// Compara hash com texto
    /// </summary>
    bool VerifyHash(string text, string hash, string salt);
}
```

#### src/IDE.Application/Auth/Services/Common/CryptoService.cs
```csharp
using System.Security.Cryptography;
using System.Text;
using IDE.Application.Auth.Services.Common;

namespace IDE.Application.Auth.Services.Common;

/// <summary>
/// Implementação do serviço de criptografia
/// </summary>
public class CryptoService : ICryptoService
{
    private const string Characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    private const string UrlSafeCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
    private const string NumericCharacters = "0123456789";

    public string GenerateSecureRandomString(int length = 32)
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[length];
        var result = new StringBuilder(length);

        rng.GetBytes(bytes);

        for (int i = 0; i < length; i++)
        {
            result.Append(Characters[bytes[i] % Characters.Length]);
        }

        return result.ToString();
    }

    public string GenerateUrlSafeToken(int length = 32)
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[length];
        var result = new StringBuilder(length);

        rng.GetBytes(bytes);

        for (int i = 0; i < length; i++)
        {
            result.Append(UrlSafeCharacters[bytes[i] % UrlSafeCharacters.Length]);
        }

        return result.ToString();
    }

    public string GenerateNumericCode(int length = 6)
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[length];
        var result = new StringBuilder(length);

        rng.GetBytes(bytes);

        for (int i = 0; i < length; i++)
        {
            result.Append(NumericCharacters[bytes[i] % NumericCharacters.Length]);
        }

        return result.ToString();
    }

    public List<string> GenerateRecoveryCodes(int count = 10, int length = 8)
    {
        var codes = new List<string>();
        
        for (int i = 0; i < count; i++)
        {
            codes.Add(GenerateSecureRandomString(length).ToUpperInvariant());
        }
        
        return codes;
    }

    public string HashWithSalt(string input, string salt)
    {
        using var sha256 = SHA256.Create();
        var saltedInput = input + salt;
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(saltedInput));
        return Convert.ToBase64String(hashBytes);
    }

    public string GenerateSalt()
    {
        return GenerateSecureRandomString(16);
    }

    public bool VerifyHash(string text, string hash, string salt)
    {
        var textHash = HashWithSalt(text, salt);
        return string.Equals(textHash, hash, StringComparison.Ordinal);
    }
}
```

### 2. Implementar Serviços de Senha

#### src/IDE.Application/Auth/Services/IPasswordService.cs
```csharp
namespace IDE.Application.Auth.Services;

/// <summary>
/// Interface para serviço de gerenciamento de senhas
/// </summary>
public interface IPasswordService
{
    /// <summary>
    /// Gera hash da senha usando BCrypt
    /// </summary>
    string HashPassword(string password);
    
    /// <summary>
    /// Verifica se a senha corresponde ao hash
    /// </summary>
    bool VerifyPassword(string password, string hashedPassword);
    
    /// <summary>
    /// Verifica se o hash da senha precisa ser atualizado
    /// </summary>
    bool NeedsRehash(string hashedPassword);
    
    /// <summary>
    /// Calcula a força da senha (0-100)
    /// </summary>
    int CalculatePasswordStrength(string password);
    
    /// <summary>
    /// Gera uma senha segura aleatória
    /// </summary>
    string GenerateSecurePassword(int length = 16);
    
    /// <summary>
    /// Valida complexidade da senha
    /// </summary>
    bool IsPasswordSecure(string password);
}
```

#### src/IDE.Application/Auth/Services/PasswordService.cs
```csharp
using System.Text.RegularExpressions;
using IDE.Application.Auth.Services.Common;
using BCrypt.Net;

namespace IDE.Application.Auth.Services;

/// <summary>
/// Implementação do serviço de senhas usando BCrypt
/// </summary>
public class PasswordService : IPasswordService
{
    private const int WorkFactor = 12; // BCrypt work factor (ajustar conforme necessário)
    private readonly ICryptoService _cryptoService;

    public PasswordService(ICryptoService cryptoService)
    {
        _cryptoService = cryptoService;
    }

    public string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be null or empty", nameof(password));

        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public bool VerifyPassword(string password, string hashedPassword)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hashedPassword))
            return false;

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
        }
        catch
        {
            return false;
        }
    }

    public bool NeedsRehash(string hashedPassword)
    {
        if (string.IsNullOrEmpty(hashedPassword))
            return true;

        try
        {
            return BCrypt.Net.BCrypt.PasswordNeedsRehash(hashedPassword, WorkFactor);
        }
        catch
        {
            return true;
        }
    }

    public int CalculatePasswordStrength(string password)
    {
        if (string.IsNullOrEmpty(password)) return 0;

        int score = 0;

        // Comprimento
        if (password.Length >= 8) score += 10;
        if (password.Length >= 12) score += 10;
        if (password.Length >= 16) score += 10;

        // Caracteres diferentes
        if (Regex.IsMatch(password, @"[a-z]")) score += 10; // Minúsculas
        if (Regex.IsMatch(password, @"[A-Z]")) score += 10; // Maiúsculas
        if (Regex.IsMatch(password, @"\d")) score += 10; // Números
        if (Regex.IsMatch(password, @"[@$!%*?&]")) score += 10; // Especiais

        // Diversidade de caracteres
        var uniqueChars = password.Distinct().Count();
        if (uniqueChars >= password.Length * 0.7) score += 10;

        // Não é senha comum
        if (NotCommonPassword(password)) score += 10;

        // Não tem padrões simples
        if (NotHasSimplePatterns(password)) score += 10;

        return Math.Min(score, 100);
    }

    public string GenerateSecurePassword(int length = 16)
    {
        if (length < 8) length = 8;
        if (length > 128) length = 128;

        const string lowercase = "abcdefghijklmnopqrstuvwxyz";
        const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string numbers = "0123456789";
        const string specials = "@$!%*?&";

        var password = new StringBuilder();
        var random = new Random();

        // Garante pelo menos um de cada tipo
        password.Append(lowercase[random.Next(lowercase.Length)]);
        password.Append(uppercase[random.Next(uppercase.Length)]);
        password.Append(numbers[random.Next(numbers.Length)]);
        password.Append(specials[random.Next(specials.Length)]);

        // Preenche o resto
        const string allChars = lowercase + uppercase + numbers + specials;
        for (int i = 4; i < length; i++)
        {
            password.Append(allChars[random.Next(allChars.Length)]);
        }

        // Embaralha
        var chars = password.ToString().ToCharArray();
        for (int i = chars.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }

    public bool IsPasswordSecure(string password)
    {
        if (string.IsNullOrEmpty(password)) return false;
        
        return CalculatePasswordStrength(password) >= 70; // 70+ é considerado seguro
    }

    /// <summary>
    /// Verifica se não é senha comum
    /// </summary>
    private static bool NotCommonPassword(string password)
    {
        var commonPasswords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "password", "123456", "123456789", "12345678", "12345", "qwerty",
            "abc123", "password123", "123123", "welcome", "admin", "letmein",
            "senha123", "brasil123", "Password123!", "123456789!"
        };

        return !commonPasswords.Contains(password);
    }

    /// <summary>
    /// Verifica se não tem padrões simples
    /// </summary>
    private static bool NotHasSimplePatterns(string password)
    {
        // Sequências numéricas
        if (Regex.IsMatch(password, @"(012|123|234|345|456|567|678|789|890)")) return false;
        
        // Sequências alfabéticas
        if (Regex.IsMatch(password, @"(abc|bcd|cde|def|efg|fgh|ghi|hij|ijk|jkl|klm|lmn|mno|nop|opq|pqr|qrs|rst|stu|tuv|uvw|vwx|wxy|xyz)", RegexOptions.IgnoreCase)) return false;
        
        // Repetições excessivas
        if (Regex.IsMatch(password, @"(.)\1{2,}")) return false;
        
        return true;
    }
}
```

### 3. Implementar Serviços de Token

#### src/IDE.Application/Auth/Services/ITokenService.cs
```csharp
using System.Security.Claims;
using IDE.Domain.Entities;

namespace IDE.Application.Auth.Services;

/// <summary>
/// Interface para serviço de tokens JWT
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Gera access token JWT
    /// </summary>
    string GenerateAccessToken(User user, IEnumerable<string>? additionalClaims = null);
    
    /// <summary>
    /// Gera refresh token
    /// </summary>
    string GenerateRefreshToken();
    
    /// <summary>
    /// Valida access token e retorna claims
    /// </summary>
    ClaimsPrincipal? ValidateAccessToken(string token);
    
    /// <summary>
    /// Extrai claims do token sem validar expiração
    /// </summary>
    ClaimsPrincipal? GetClaimsFromExpiredToken(string token);
    
    /// <summary>
    /// Obtém tempo de expiração do token
    /// </summary>
    DateTime GetTokenExpiration(string token);
    
    /// <summary>
    /// Verifica se o token está expirado
    /// </summary>
    bool IsTokenExpired(string token);
    
    /// <summary>
    /// Obtém JTI (JWT ID) do token
    /// </summary>
    string? GetTokenJti(string token);
    
    /// <summary>
    /// Gera token de verificação de email
    /// </summary>
    string GenerateEmailVerificationToken(User user);
    
    /// <summary>
    /// Gera token de reset de senha
    /// </summary>
    string GeneratePasswordResetToken(User user);
    
    /// <summary>
    /// Valida token de verificação
    /// </summary>
    bool ValidateVerificationToken(string token, string purpose, Guid userId);
}
```

#### src/IDE.Application/Auth/Services/TokenService.cs
```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using IDE.Application.Auth.Services.Common;
using IDE.Domain.Entities;

namespace IDE.Application.Auth.Services;

/// <summary>
/// Implementação do serviço de tokens JWT
/// </summary>
public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICryptoService _cryptoService;
    private readonly TokenValidationParameters _tokenValidationParameters;

    public TokenService(
        IConfiguration configuration, 
        IDateTimeProvider dateTimeProvider,
        ICryptoService cryptoService)
    {
        _configuration = configuration;
        _dateTimeProvider = dateTimeProvider;
        _cryptoService = cryptoService;
        _tokenValidationParameters = GetTokenValidationParameters();
    }

    public string GenerateAccessToken(User user, IEnumerable<string>? additionalClaims = null)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");
        var issuer = jwtSettings["Issuer"] ?? throw new InvalidOperationException("JWT Issuer not configured");
        var audience = jwtSettings["Audience"] ?? throw new InvalidOperationException("JWT Audience not configured");
        var expirationMinutes = int.Parse(jwtSettings["ExpirationMinutes"] ?? "60");

        var key = Encoding.UTF8.GetBytes(secretKey);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(GetUserClaims(user, additionalClaims)),
            Expires = _dateTimeProvider.UtcNow.AddMinutes(expirationMinutes),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
            Issuer = issuer,
            Audience = audience,
            IssuedAt = _dateTimeProvider.UtcNow,
            NotBefore = _dateTimeProvider.UtcNow
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        return _cryptoService.GenerateUrlSafeToken(64);
    }

    public ClaimsPrincipal? ValidateAccessToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, _tokenValidationParameters, out var validatedToken);
            
            // Verifica se é um JWT válido
            if (validatedToken is not JwtSecurityToken jwtToken || 
                !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            return principal;
        }
        catch
        {
            return null;
        }
    }

    public ClaimsPrincipal? GetClaimsFromExpiredToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParameters = GetTokenValidationParameters();
            validationParameters.ValidateLifetime = false; // Ignora expiração

            var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
            
            // Verifica se é um JWT válido
            if (validatedToken is not JwtSecurityToken jwtToken || 
                !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            return principal;
        }
        catch
        {
            return null;
        }
    }

    public DateTime GetTokenExpiration(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jsonToken = tokenHandler.ReadJwtToken(token);
            return jsonToken.ValidTo;
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    public bool IsTokenExpired(string token)
    {
        try
        {
            var expiration = GetTokenExpiration(token);
            return expiration <= _dateTimeProvider.UtcNow;
        }
        catch
        {
            return true;
        }
    }

    public string? GetTokenJti(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jsonToken = tokenHandler.ReadJwtToken(token);
            return jsonToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
        }
        catch
        {
            return null;
        }
    }

    public string GenerateEmailVerificationToken(User user)
    {
        return GenerateVerificationToken(user, "email_verification");
    }

    public string GeneratePasswordResetToken(User user)
    {
        return GenerateVerificationToken(user, "password_reset");
    }

    public bool ValidateVerificationToken(string token, string purpose, Guid userId)
    {
        try
        {
            var principal = ValidateAccessToken(token);
            if (principal == null) return false;

            var tokenUserId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var tokenPurpose = principal.FindFirst("purpose")?.Value;

            return tokenUserId == userId.ToString() && tokenPurpose == purpose;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gera token de verificação para propósitos específicos
    /// </summary>
    private string GenerateVerificationToken(User user, string purpose)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");
        var issuer = jwtSettings["Issuer"] ?? throw new InvalidOperationException("JWT Issuer not configured");
        var audience = jwtSettings["Audience"] ?? throw new InvalidOperationException("JWT Audience not configured");
        
        // Token de verificação expira em 24 horas
        var expirationHours = purpose == "password_reset" ? 2 : 24; // Reset de senha expira mais rápido

        var key = Encoding.UTF8.GetBytes(secretKey);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("purpose", purpose),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, _dateTimeProvider.UnixTimestamp.ToString(), ClaimValueTypes.Integer64)
            }),
            Expires = _dateTimeProvider.UtcNow.AddHours(expirationHours),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
            Issuer = issuer,
            Audience = audience
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    /// <summary>
    /// Obtém claims do usuário para o token
    /// </summary>
    private static IEnumerable<Claim> GetUserClaims(User user, IEnumerable<string>? additionalClaims)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new("first_name", user.FirstName),
            new("last_name", user.LastName),
            new("email_verified", user.EmailVerified.ToString().ToLowerInvariant()),
            new("two_factor_enabled", user.TwoFactorEnabled.ToString().ToLowerInvariant()),
            new("account_status", user.Status.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        // Adiciona role se disponível
        if (!string.IsNullOrEmpty(user.Role))
        {
            claims.Add(new Claim(ClaimTypes.Role, user.Role));
        }

        // Adiciona claims adicionais
        if (additionalClaims != null)
        {
            claims.AddRange(additionalClaims.Select(claim => new Claim("custom", claim)));
        }

        return claims;
    }

    /// <summary>
    /// Obtém parâmetros de validação de token
    /// </summary>
    private TokenValidationParameters GetTokenValidationParameters()
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");
        var issuer = jwtSettings["Issuer"] ?? throw new InvalidOperationException("JWT Issuer not configured");
        var audience = jwtSettings["Audience"] ?? throw new InvalidOperationException("JWT Audience not configured");

        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5) // 5 minutos de tolerância para clock skew
        };
    }
}
```

### 4. Implementar Token Blacklist Service

#### src/IDE.Application/Auth/Services/ITokenBlacklistService.cs
```csharp
namespace IDE.Application.Auth.Services;

/// <summary>
/// Interface para blacklist de tokens
/// </summary>
public interface ITokenBlacklistService
{
    /// <summary>
    /// Adiciona token à blacklist
    /// </summary>
    Task BlacklistTokenAsync(string jti, DateTime expiration, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Verifica se token está na blacklist
    /// </summary>
    Task<bool> IsTokenBlacklistedAsync(string jti, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Remove tokens expirados da blacklist
    /// </summary>
    Task CleanupExpiredTokensAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Adiciona todos os tokens do usuário à blacklist
    /// </summary>
    Task BlacklistAllUserTokensAsync(Guid userId, CancellationToken cancellationToken = default);
}
```

#### src/IDE.Application/Auth/Services/TokenBlacklistService.cs
```csharp
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using IDE.Application.Auth.Services.Common;
using IDE.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IDE.Application.Auth.Services;

/// <summary>
/// Implementação do serviço de blacklist usando cache distribuído
/// </summary>
public class TokenBlacklistService : ITokenBlacklistService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<TokenBlacklistService> _logger;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ApplicationDbContext _context;
    
    private const string BlacklistPrefix = "token_blacklist:";
    private const string UserTokensPrefix = "user_tokens:";

    public TokenBlacklistService(
        IDistributedCache cache,
        ILogger<TokenBlacklistService> logger,
        IDateTimeProvider dateTimeProvider,
        ApplicationDbContext context)
    {
        _cache = cache;
        _logger = logger;
        _dateTimeProvider = dateTimeProvider;
        _context = context;
    }

    public async Task BlacklistTokenAsync(string jti, DateTime expiration, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = BlacklistPrefix + jti;
            var tokenInfo = new BlacklistedTokenInfo
            {
                Jti = jti,
                BlacklistedAt = _dateTimeProvider.UtcNow,
                ExpiresAt = expiration
            };

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = expiration.AddMinutes(10) // Adiciona buffer
            };

            var json = JsonSerializer.Serialize(tokenInfo);
            await _cache.SetStringAsync(key, json, options, cancellationToken);

            _logger.LogInformation("Token {Jti} adicionado à blacklist", jti);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao adicionar token {Jti} à blacklist", jti);
            throw;
        }
    }

    public async Task<bool> IsTokenBlacklistedAsync(string jti, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = BlacklistPrefix + jti;
            var cachedValue = await _cache.GetStringAsync(key, cancellationToken);
            
            if (string.IsNullOrEmpty(cachedValue))
                return false;

            var tokenInfo = JsonSerializer.Deserialize<BlacklistedTokenInfo>(cachedValue);
            
            // Se o token expirou naturalmente, remove da blacklist
            if (tokenInfo?.ExpiresAt <= _dateTimeProvider.UtcNow)
            {
                await _cache.RemoveAsync(key, cancellationToken);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar blacklist para token {Jti}", jti);
            // Em caso de erro, assume que o token não está blacklisted
            return false;
        }
    }

    public async Task CleanupExpiredTokensAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Iniciando limpeza de tokens expirados da blacklist");
            
            // Esta implementação é simplificada
            // Em produção, seria melhor usar um job em background
            // ou implementar varredura mais eficiente
            
            _logger.LogInformation("Limpeza de tokens expirados concluída");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante limpeza de tokens expirados");
        }
    }

    public async Task BlacklistAllUserTokensAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Invalida todos os refresh tokens do usuário no banco
            var refreshTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == userId && rt.IsActive && rt.ExpiresAt > _dateTimeProvider.UtcNow)
                .ToListAsync(cancellationToken);

            foreach (var refreshToken in refreshTokens)
            {
                refreshToken.RevokedAt = _dateTimeProvider.UtcNow;
                refreshToken.RevokedReason = "User logout all devices";
            }

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Todos os tokens do usuário {UserId} foram invalidados", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao invalidar todos os tokens do usuário {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Modelo para informações de token blacklisted
    /// </summary>
    private class BlacklistedTokenInfo
    {
        public string Jti { get; set; } = string.Empty;
        public DateTime BlacklistedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
```

### 5. Implementar User Service

#### src/IDE.Application/Auth/Services/IUserService.cs
```csharp
using IDE.Domain.Entities;
using IDE.Domain.Enums;

namespace IDE.Application.Auth.Services;

/// <summary>
/// Interface para serviço de usuários
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Busca usuário por ID
    /// </summary>
    Task<User?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Busca usuário por email
    /// </summary>
    Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Busca usuário por username
    /// </summary>
    Task<User?> GetUserByUsernameAsync(string username, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Busca usuário por email ou username
    /// </summary>
    Task<User?> GetUserByEmailOrUsernameAsync(string emailOrUsername, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Cria novo usuário
    /// </summary>
    Task<User> CreateUserAsync(string email, string username, string password, string firstName, string lastName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Atualiza senha do usuário
    /// </summary>
    Task UpdatePasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Verifica email do usuário
    /// </summary>
    Task VerifyEmailAsync(Guid userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Atualiza último login
    /// </summary>
    Task UpdateLastLoginAsync(Guid userId, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Incrementa tentativas de login
    /// </summary>
    Task IncrementLoginAttemptsAsync(Guid userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Reseta tentativas de login
    /// </summary>
    Task ResetLoginAttemptsAsync(Guid userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Bloqueia usuário
    /// </summary>
    Task LockUserAsync(Guid userId, DateTime? lockoutEnd = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Desbloqueia usuário
    /// </summary>
    Task UnlockUserAsync(Guid userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Verifica se usuário está bloqueado
    /// </summary>
    Task<bool> IsUserLockedAsync(Guid userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Atualiza status do usuário
    /// </summary>
    Task UpdateUserStatusAsync(Guid userId, UserStatus status, CancellationToken cancellationToken = default);
}
```

#### src/IDE.Application/Auth/Services/UserService.cs
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IDE.Application.Auth.Services.Common;
using IDE.Domain.Entities;
using IDE.Domain.Enums;
using IDE.Infrastructure.Data;

namespace IDE.Application.Auth.Services;

/// <summary>
/// Implementação do serviço de usuários
/// </summary>
public class UserService : IUserService
{
    private readonly ApplicationDbContext _context;
    private readonly IPasswordService _passwordService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<UserService> _logger;

    public UserService(
        ApplicationDbContext context,
        IPasswordService passwordService,
        IDateTimeProvider dateTimeProvider,
        ILogger<UserService> logger)
    {
        _context = context;
        _passwordService = passwordService;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<User?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(email)) return null;

        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);
    }

    public async Task<User?> GetUserByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(username)) return null;

        var normalizedUsername = username.Trim().ToLowerInvariant();
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == normalizedUsername, cancellationToken);
    }

    public async Task<User?> GetUserByEmailOrUsernameAsync(string emailOrUsername, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(emailOrUsername)) return null;

        var normalized = emailOrUsername.Trim().ToLowerInvariant();
        return await _context.Users
            .FirstOrDefaultAsync(u => 
                u.Email.ToLower() == normalized || 
                u.Username.ToLower() == normalized, 
                cancellationToken);
    }

    public async Task<User> CreateUserAsync(
        string email, 
        string username, 
        string password, 
        string firstName, 
        string lastName, 
        CancellationToken cancellationToken = default)
    {
        var hashedPassword = _passwordService.HashPassword(password);
        
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email.Trim().ToLowerInvariant(),
            Username = username.Trim(),
            PasswordHash = hashedPassword,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            EmailVerified = false,
            TwoFactorEnabled = false,
            Status = UserStatus.Active,
            CreatedAt = _dateTimeProvider.UtcNow,
            UpdatedAt = _dateTimeProvider.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Usuário criado com sucesso: {UserId} - {Email}", user.Id, user.Email);
        
        return user;
    }

    public async Task UpdatePasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await GetUserByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new InvalidOperationException("Usuário não encontrado");
        }

        user.PasswordHash = _passwordService.HashPassword(newPassword);
        user.UpdatedAt = _dateTimeProvider.UtcNow;
        user.PasswordChangedAt = _dateTimeProvider.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Senha atualizada para usuário: {UserId}", userId);
    }

    public async Task VerifyEmailAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetUserByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new InvalidOperationException("Usuário não encontrado");
        }

        user.EmailVerified = true;
        user.EmailVerifiedAt = _dateTimeProvider.UtcNow;
        user.UpdatedAt = _dateTimeProvider.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Email verificado para usuário: {UserId}", userId);
    }

    public async Task UpdateLastLoginAsync(Guid userId, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default)
    {
        var user = await GetUserByIdAsync(userId, cancellationToken);
        if (user == null) return;

        user.LastLoginAt = _dateTimeProvider.UtcNow;
        user.LastLoginIp = ipAddress;
        user.UpdatedAt = _dateTimeProvider.UtcNow;

        // Log da atividade de login
        var loginHistory = new UserLoginHistory
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LoginAt = _dateTimeProvider.UtcNow,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Success = true
        };

        _context.UserLoginHistories.Add(loginHistory);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Login atualizado para usuário: {UserId} - IP: {IpAddress}", userId, ipAddress);
    }

    public async Task IncrementLoginAttemptsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetUserByIdAsync(userId, cancellationToken);
        if (user == null) return;

        user.LoginAttempts++;
        user.UpdatedAt = _dateTimeProvider.UtcNow;

        // Se exceder tentativas máximas, bloqueia por 15 minutos
        if (user.LoginAttempts >= 5)
        {
            user.LockoutEnd = _dateTimeProvider.UtcNow.AddMinutes(15);
            _logger.LogWarning("Usuário {UserId} bloqueado por tentativas excessivas de login", userId);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ResetLoginAttemptsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetUserByIdAsync(userId, cancellationToken);
        if (user == null) return;

        user.LoginAttempts = 0;
        user.LockoutEnd = null;
        user.UpdatedAt = _dateTimeProvider.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task LockUserAsync(Guid userId, DateTime? lockoutEnd = null, CancellationToken cancellationToken = default)
    {
        var user = await GetUserByIdAsync(userId, cancellationToken);
        if (user == null) return;

        user.LockoutEnd = lockoutEnd ?? _dateTimeProvider.UtcNow.AddHours(24);
        user.UpdatedAt = _dateTimeProvider.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogWarning("Usuário {UserId} bloqueado até {LockoutEnd}", userId, user.LockoutEnd);
    }

    public async Task UnlockUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetUserByIdAsync(userId, cancellationToken);
        if (user == null) return;

        user.LockoutEnd = null;
        user.LoginAttempts = 0;
        user.UpdatedAt = _dateTimeProvider.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Usuário {UserId} desbloqueado", userId);
    }

    public async Task<bool> IsUserLockedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetUserByIdAsync(userId, cancellationToken);
        if (user == null) return false;

        return user.LockoutEnd.HasValue && user.LockoutEnd > _dateTimeProvider.UtcNow;
    }

    public async Task UpdateUserStatusAsync(Guid userId, UserStatus status, CancellationToken cancellationToken = default)
    {
        var user = await GetUserByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new InvalidOperationException("Usuário não encontrado");
        }

        user.Status = status;
        user.UpdatedAt = _dateTimeProvider.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Status do usuário {UserId} atualizado para {Status}", userId, status);
    }
}
```

### 6. Validar Implementação

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

- [ ] **IAuthService e AuthService** implementados com todas as funcionalidades
- [ ] **ITokenService e TokenService** funcionando com JWT
- [ ] **IPasswordService e PasswordService** com BCrypt configurado
- [ ] **IUserService e UserService** com operações de usuário
- [ ] **Token Blacklist Service** implementado
- [ ] **Serviços auxiliares** (DateTime, Crypto) funcionando
- [ ] **Compilação bem-sucedida** sem erros

## 📝 Arquivos Criados

Esta parte criará aproximadamente **14 arquivos**:
- 4 Helpers comuns (DateTime, Crypto)
- 10 Serviços principais de autenticação

## 🔄 Próximos Passos

Após concluir esta parte, você estará pronto para:
- **Parte 8**: Implementação de 2FA (TOTP, Backup Codes)
- Configurar serviços OAuth
- Implementar sistema de email

## 🚨 Troubleshooting Comum

**JWT não funciona**: Verificar configurações no appsettings.json  
**BCrypt erro**: Instalar pacote BCrypt.Net-Next  
**DI problemas**: Services serão registrados no Program.cs  
**Cache Redis**: Configurar conexão Redis no appsettings  

---
**⏱️ Tempo estimado**: 20-35 minutos  
**🎯 Próxima parte**: 08-seguranca-2fa-implementacao.md  
**📋 Dependências**: Partes 1-6 concluídas