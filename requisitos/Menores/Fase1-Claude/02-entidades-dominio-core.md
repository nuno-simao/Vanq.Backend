# Parte 2: Entidades de Domínio Core

## 📋 Visão Geral
**Duração**: 20-25 minutos  
**Complexidade**: Baixa-Média  
**Dependências**: Parte 1 (Setup Inicial)

Esta parte implementa as entidades principais do domínio, focando especificamente na entidade User e RefreshToken, que são a base do sistema de autenticação.

## 🎯 Objetivos
- ✅ Implementar entidade User completa com todas as propriedades de segurança
- ✅ Criar entidade RefreshToken para JWT
- ✅ Definir enums essenciais (UserPlan, TwoFactorMethod, etc.)
- ✅ Estabelecer relacionamentos entre entidades
- ✅ Configurar validações básicas via Data Annotations

## 📁 Arquivos a serem Criados

```
src/IDE.Domain/
├── Entities/
│   ├── User.cs
│   └── RefreshToken.cs
├── Enums/
│   ├── UserPlan.cs
│   ├── TwoFactorMethod.cs
│   └── EmailProvider.cs
└── ValueObjects/
    ├── Email.cs
    └── Password.cs
```

## 🚀 Execução Passo a Passo

### 1. Criar Enums Essenciais

#### src/IDE.Domain/Enums/UserPlan.cs
```csharp
using System.ComponentModel;

namespace IDE.Domain.Enums;

/// <summary>
/// Planos de usuário disponíveis no sistema
/// </summary>
public enum UserPlan
{
    [Description("Plano gratuito com funcionalidades básicas")]
    Free = 0,
    
    [Description("Plano premium com recursos avançados")]
    Premium = 1,
    
    [Description("Plano empresarial com recursos completos")]
    Enterprise = 2
}
```

#### src/IDE.Domain/Enums/TwoFactorMethod.cs
```csharp
using System.ComponentModel;

namespace IDE.Domain.Enums;

/// <summary>
/// Métodos de autenticação de dois fatores suportados
/// </summary>
public enum TwoFactorMethod
{
    [Description("Sem autenticação de dois fatores")]
    None = 0,
    
    [Description("Time-based One-Time Password (TOTP)")]
    Totp = 1,
    
    [Description("Código via email")]
    Email = 2
}
```

#### src/IDE.Domain/Enums/EmailProvider.cs
```csharp
using System.ComponentModel;

namespace IDE.Domain.Enums;

/// <summary>
/// Provedores de email suportados pelo sistema
/// </summary>
public enum EmailProvider
{
    [Description("SendGrid - Serviço de email transacional")]
    SendGrid = 0,
    
    [Description("Gmail - Servidor SMTP do Google")]
    Gmail = 1,
    
    [Description("Outlook - Servidor SMTP da Microsoft")]
    Outlook = 2,
    
    [Description("SMTP genérico")]
    Smtp = 3,
    
    [Description("Provider mock para desenvolvimento")]
    Mock = 4
}
```

#### src/IDE.Domain/Enums/ConfigType.cs
```csharp
using System.ComponentModel;

namespace IDE.Domain.Enums;

/// <summary>
/// Tipos de configuração do sistema
/// </summary>
public enum ConfigType
{
    [Description("Valor textual")]
    String = 0,
    
    [Description("Valor numérico inteiro")]
    Integer = 1,
    
    [Description("Valor booleano")]
    Boolean = 2,
    
    [Description("Objeto JSON")]
    Json = 3
}
```

### 2. Criar Value Objects

#### src/IDE.Domain/ValueObjects/Email.cs
```csharp
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace IDE.Domain.ValueObjects;

/// <summary>
/// Value Object para representar um email válido
/// </summary>
public class Email : IEquatable<Email>
{
    private static readonly Regex EmailRegex = new(
        @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Value { get; private set; }

    private Email(string value)
    {
        Value = value;
    }

    public static Email Create(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email não pode ser nulo ou vazio", nameof(email));

        email = email.Trim().ToLowerInvariant();

        if (!EmailRegex.IsMatch(email))
            throw new ArgumentException("Format de email inválido", nameof(email));

        if (email.Length > 254) // RFC 5321 limit
            throw new ArgumentException("Email excede o tamanho máximo permitido", nameof(email));

        return new Email(email);
    }

    public bool Equals(Email? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Value == other.Value;
    }

    public override bool Equals(object? obj) => Equals(obj as Email);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => Value;

    public static implicit operator string(Email email) => email.Value;
    public static explicit operator Email(string email) => Create(email);
}
```

#### src/IDE.Domain/ValueObjects/Password.cs
```csharp
using System.Text.RegularExpressions;

namespace IDE.Domain.ValueObjects;

/// <summary>
/// Value Object para representar uma senha segura
/// </summary>
public class Password
{
    private static readonly Regex PasswordRegex = new(
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$",
        RegexOptions.Compiled);

    public string Value { get; private set; }

    private Password(string value)
    {
        Value = value;
    }

    public static Password Create(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Senha não pode ser nula ou vazia", nameof(password));

        if (password.Length < 8)
            throw new ArgumentException("Senha deve ter pelo menos 8 caracteres", nameof(password));

        if (password.Length > 128)
            throw new ArgumentException("Senha excede o tamanho máximo permitido", nameof(password));

        if (!PasswordRegex.IsMatch(password))
            throw new ArgumentException(
                "Senha deve conter pelo menos: 1 minúscula, 1 maiúscula, 1 número e 1 caractere especial",
                nameof(password));

        // Verificar se não é uma senha comum
        if (IsCommonPassword(password))
            throw new ArgumentException("Esta senha é muito comum. Escolha uma senha mais segura", nameof(password));

        return new Password(password);
    }

    private static bool IsCommonPassword(string password)
    {
        // Lista das senhas mais comuns (simplificada)
        var commonPasswords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Password123!", "123456789", "Qwerty123!", "Password1!",
            "Admin123!", "Welcome123!", "Test123!", "User123!",
            "Abc123456!", "Password@123", "Admin@123", "User@123"
        };

        return commonPasswords.Contains(password);
    }

    public override string ToString() => new string('*', Math.Min(Value.Length, 8));
    public static implicit operator string(Password password) => password.Value;
}
```

### 3. Criar Entidades Core

#### src/IDE.Domain/Entities/User.cs
```csharp
using System.ComponentModel.DataAnnotations;
using IDE.Domain.Enums;

namespace IDE.Domain.Entities;

/// <summary>
/// Entidade principal que representa um usuário do sistema
/// </summary>
public class User
{
    /// <summary>
    /// Identificador único do usuário
    /// </summary>
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Email do usuário (único no sistema)
    /// </summary>
    [Required]
    [MaxLength(254)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Nome de usuário (único no sistema)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Hash da senha do usuário (BCrypt)
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Primeiro nome do usuário
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Último nome do usuário
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// URL do avatar do usuário
    /// </summary>
    [MaxLength(500)]
    public string Avatar { get; set; } = string.Empty;

    /// <summary>
    /// Provedor do avatar (OAuth, Upload, Default)
    /// </summary>
    [MaxLength(20)]
    public string AvatarProvider { get; set; } = "Default";

    /// <summary>
    /// Indica se o email foi verificado
    /// </summary>
    public bool EmailVerified { get; set; } = false;

    /// <summary>
    /// Data e hora da verificação do email
    /// </summary>
    public DateTime? EmailVerifiedAt { get; set; }

    /// <summary>
    /// Token para verificação de email
    /// </summary>
    [MaxLength(255)]
    public string EmailVerificationToken { get; set; } = string.Empty;

    /// <summary>
    /// Data de expiração do token de verificação de email
    /// </summary>
    public DateTime? EmailVerificationTokenExpiresAt { get; set; }

    /// <summary>
    /// Token para reset de senha
    /// </summary>
    [MaxLength(255)]
    public string PasswordResetToken { get; set; } = string.Empty;

    /// <summary>
    /// Data de expiração do token de reset de senha
    /// </summary>
    public DateTime? PasswordResetTokenExpiresAt { get; set; }

    /// <summary>
    /// Número de tentativas de login falhadas consecutivas
    /// </summary>
    [Range(0, int.MaxValue)]
    public int FailedLoginAttempts { get; set; } = 0;

    /// <summary>
    /// Data até quando a conta está bloqueada por tentativas excessivas
    /// </summary>
    public DateTime? LockedOutUntil { get; set; }

    /// <summary>
    /// Indica se a autenticação de dois fatores está habilitada
    /// </summary>
    public bool TwoFactorEnabled { get; set; } = false;

    /// <summary>
    /// Segredo para TOTP (Time-based One-Time Password)
    /// </summary>
    [MaxLength(255)]
    public string TwoFactorSecret { get; set; } = string.Empty;

    /// <summary>
    /// Método de autenticação de dois fatores escolhido pelo usuário
    /// </summary>
    public TwoFactorMethod TwoFactorMethod { get; set; } = TwoFactorMethod.None;

    /// <summary>
    /// Plano do usuário no sistema
    /// </summary>
    public UserPlan Plan { get; set; } = UserPlan.Free;

    /// <summary>
    /// Data de criação da conta
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Data do último login
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// IP do último login
    /// </summary>
    [MaxLength(45)] // IPv6 max length
    public string LastLoginIp { get; set; } = string.Empty;

    /// <summary>
    /// User Agent do último login
    /// </summary>
    [MaxLength(500)]
    public string LastLoginUserAgent { get; set; } = string.Empty;

    // Relacionamentos (navegação)

    /// <summary>
    /// Lista de refresh tokens válidos para este usuário
    /// </summary>
    public virtual List<RefreshToken> RefreshTokens { get; set; } = new();

    /// <summary>
    /// Lista de API Keys criadas por este usuário
    /// </summary>
    public virtual List<ApiKey> ApiKeys { get; set; } = new();

    /// <summary>
    /// Histórico de logins do usuário
    /// </summary>
    public virtual List<UserLoginHistory> LoginHistory { get; set; } = new();

    // Propriedades computadas

    /// <summary>
    /// Nome completo do usuário
    /// </summary>
    public string FullName => $"{FirstName} {LastName}".Trim();

    /// <summary>
    /// Indica se a conta está atualmente bloqueada
    /// </summary>
    public bool IsLockedOut => LockedOutUntil.HasValue && LockedOutUntil > DateTime.UtcNow;

    /// <summary>
    /// Indica se o token de verificação de email ainda é válido
    /// </summary>
    public bool IsEmailVerificationTokenValid => 
        !string.IsNullOrEmpty(EmailVerificationToken) && 
        EmailVerificationTokenExpiresAt.HasValue && 
        EmailVerificationTokenExpiresAt > DateTime.UtcNow;

    /// <summary>
    /// Indica se o token de reset de senha ainda é válido
    /// </summary>
    public bool IsPasswordResetTokenValid => 
        !string.IsNullOrEmpty(PasswordResetToken) && 
        PasswordResetTokenExpiresAt.HasValue && 
        PasswordResetTokenExpiresAt > DateTime.UtcNow;

    // Métodos auxiliares

    /// <summary>
    /// Remove todos os refresh tokens expirados
    /// </summary>
    public void RemoveExpiredRefreshTokens()
    {
        RefreshTokens.RemoveAll(rt => rt.IsExpired);
    }

    /// <summary>
    /// Limpa dados sensíveis relacionados a reset de senha
    /// </summary>
    public void ClearPasswordResetData()
    {
        PasswordResetToken = string.Empty;
        PasswordResetTokenExpiresAt = null;
    }

    /// <summary>
    /// Limpa dados relacionados à verificação de email
    /// </summary>
    public void ClearEmailVerificationData()
    {
        EmailVerificationToken = string.Empty;
        EmailVerificationTokenExpiresAt = null;
    }

    /// <summary>
    /// Reseta contador de tentativas de login falhadas
    /// </summary>
    public void ResetFailedLoginAttempts()
    {
        FailedLoginAttempts = 0;
        LockedOutUntil = null;
    }

    /// <summary>
    /// Incrementa contador de tentativas de login falhadas
    /// </summary>
    public void IncrementFailedLoginAttempts()
    {
        FailedLoginAttempts++;
    }

    /// <summary>
    /// Bloqueia a conta por um período determinado
    /// </summary>
    /// <param name="lockoutDuration">Duração do bloqueio</param>
    public void LockAccount(TimeSpan lockoutDuration)
    {
        LockedOutUntil = DateTime.UtcNow.Add(lockoutDuration);
    }

    /// <summary>
    /// Atualiza informações do último login
    /// </summary>
    /// <param name="ipAddress">IP do login</param>
    /// <param name="userAgent">User Agent do browser</param>
    public void UpdateLastLogin(string ipAddress, string userAgent)
    {
        LastLoginAt = DateTime.UtcNow;
        LastLoginIp = ipAddress ?? string.Empty;
        LastLoginUserAgent = userAgent ?? string.Empty;
    }
}
```

#### src/IDE.Domain/Entities/RefreshToken.cs
```csharp
using System.ComponentModel.DataAnnotations;

namespace IDE.Domain.Entities;

/// <summary>
/// Token de refresh para renovação de tokens JWT
/// </summary>
public class RefreshToken
{
    /// <summary>
    /// Identificador único do refresh token
    /// </summary>
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Valor do token (hash seguro)
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Data de expiração do token
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Indica se o token foi revogado
    /// </summary>
    public bool IsRevoked { get; set; } = false;

    /// <summary>
    /// Data da revogação do token
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// Informações do dispositivo que gerou o token
    /// </summary>
    [MaxLength(500)]
    public string DeviceInfo { get; set; } = string.Empty;

    /// <summary>
    /// IP do dispositivo que criou o token
    /// </summary>
    [MaxLength(45)] // IPv6 max length
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// User Agent do dispositivo
    /// </summary>
    [MaxLength(500)]
    public string UserAgent { get; set; } = string.Empty;

    /// <summary>
    /// Data de criação do token
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Data da última utilização do token
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    // Relacionamento com usuário

    /// <summary>
    /// ID do usuário proprietário do token
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Usuário proprietário do token
    /// </summary>
    public virtual User User { get; set; } = null!;

    // Propriedades computadas

    /// <summary>
    /// Indica se o token está expirado
    /// </summary>
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    /// <summary>
    /// Indica se o token é válido (não expirado e não revogado)
    /// </summary>
    public bool IsValid => !IsExpired && !IsRevoked;

    /// <summary>
    /// Tempo restante até a expiração
    /// </summary>
    public TimeSpan TimeUntilExpiration => ExpiresAt - DateTime.UtcNow;

    /// <summary>
    /// Indica se o token está próximo da expiração (menos de 1 dia)
    /// </summary>
    public bool IsNearExpiration => TimeUntilExpiration.TotalDays < 1;

    // Métodos auxiliares

    /// <summary>
    /// Revoga o token
    /// </summary>
    /// <param name="reason">Motivo da revogação (opcional)</param>
    public void Revoke(string reason = "")
    {
        IsRevoked = true;
        RevokedAt = DateTime.UtcNow;
        // Note: reason poderia ser armazenado se necessário
    }

    /// <summary>
    /// Atualiza a data da última utilização
    /// </summary>
    public void UpdateLastUsed()
    {
        LastUsedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Cria um novo refresh token com configurações padrão
    /// </summary>
    /// <param name="userId">ID do usuário</param>
    /// <param name="deviceInfo">Informações do dispositivo</param>
    /// <param name="ipAddress">IP do dispositivo</param>
    /// <param name="userAgent">User Agent</param>
    /// <param name="expirationDays">Dias até expiração (padrão: 7)</param>
    /// <returns>Novo refresh token</returns>
    public static RefreshToken Create(
        Guid userId, 
        string deviceInfo, 
        string ipAddress, 
        string userAgent, 
        int expirationDays = 7)
    {
        var token = new RefreshToken
        {
            Token = GenerateSecureToken(),
            UserId = userId,
            DeviceInfo = deviceInfo ?? string.Empty,
            IpAddress = ipAddress ?? string.Empty,
            UserAgent = userAgent ?? string.Empty,
            ExpiresAt = DateTime.UtcNow.AddDays(expirationDays)
        };

        return token;
    }

    /// <summary>
    /// Gera um token seguro aleatório
    /// </summary>
    /// <returns>Token seguro em Base64</returns>
    private static string GenerateSecureToken()
    {
        var randomBytes = new byte[64];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}
```

### 4. Placeholder para Entidades Futuras

Como algumas entidades serão implementadas nas próximas partes, vamos criar as classes básicas:

#### src/IDE.Domain/Entities/ApiKey.cs
```csharp
using System.ComponentModel.DataAnnotations;

namespace IDE.Domain.Entities;

/// <summary>
/// API Key para autenticação programática (será implementada na Parte 3)
/// </summary>
public class ApiKey
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid UserId { get; set; }
    
    public virtual User User { get; set; } = null!;
    
    // Outras propriedades serão implementadas na Parte 3
}
```

#### src/IDE.Domain/Entities/UserLoginHistory.cs
```csharp
using System.ComponentModel.DataAnnotations;

namespace IDE.Domain.Entities;

/// <summary>
/// Histórico de login dos usuários (será implementada na Parte 3)
/// </summary>
public class UserLoginHistory
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid UserId { get; set; }
    
    public virtual User User { get; set; } = null!;
    
    // Outras propriedades serão implementadas na Parte 3
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

- [ ] **Entidade User** completa com todas as propriedades e métodos
- [ ] **Entidade RefreshToken** implementada com relacionamentos
- [ ] **Enums essenciais** criados (UserPlan, TwoFactorMethod, etc.)
- [ ] **Value Objects** Email e Password funcionando
- [ ] **Compilação bem-sucedida** sem erros ou warnings
- [ ] **Relacionamentos** definidos entre User e RefreshToken
- [ ] **Data Annotations** aplicadas corretamente

## 📝 Arquivos Criados

Esta parte criará aproximadamente **9 arquivos**:
- 2 Entidades principais (User.cs, RefreshToken.cs)
- 2 Entidades placeholder (ApiKey.cs, UserLoginHistory.cs)
- 4 Enums (UserPlan.cs, TwoFactorMethod.cs, EmailProvider.cs, ConfigType.cs)
- 2 Value Objects (Email.cs, Password.cs)

## 🔄 Próximos Passos

Após concluir esta parte, você estará pronto para:
- **Parte 3**: Entidades de Domínio Auxiliares
- Implementar entidades como ApiKey, UserLoginHistory, etc.
- Configurar relacionamentos mais complexos

## 🚨 Troubleshooting Comum

**Erros de namespace**: Verifique se todos os `using` estão corretos  
**Erros de relacionamento**: Os relacionamentos virtuais serão configurados no Entity Framework  
**Validação de Value Objects**: Teste os Value Objects isoladamente se necessário  

---
**⏱️ Tempo estimado**: 20-25 minutos  
**🎯 Próxima parte**: 03-entidades-dominio-auxiliares.md  
**📋 Dependência**: Parte 1 concluída