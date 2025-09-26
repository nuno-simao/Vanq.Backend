# Parte 6: Validações e Regras de Negócio

## 📋 Visão Geral
**Duração**: 15-25 minutos  
**Complexidade**: Baixa-Média  
**Dependências**: Partes 1-5 (Setup + Entidades + EF + DTOs)

Esta parte implementa todas as validações usando FluentValidation, incluindo regras de negócio personalizadas, validações de senha segura e verificações de domínio.

## 🎯 Objetivos
- ✅ Implementar validators para todos os requests
- ✅ Criar regras de senha segura personalizadas
- ✅ Implementar validações de email e username únicos
- ✅ Configurar validações de API Keys
- ✅ Criar validators para 2FA
- ✅ Implementar validações contextuais

## 📁 Arquivos a serem Criados

```
src/IDE.Application/Auth/Validators/
├── RegisterRequestValidator.cs
├── LoginRequestValidator.cs
├── PasswordResetRequestValidator.cs
├── PasswordResetConfirmRequestValidator.cs
├── EmailVerificationRequestValidator.cs
├── ApiKeyCreateRequestValidator.cs
├── TwoFactorRequestValidators.cs
├── OAuthLoginRequestValidator.cs
└── Common/
    ├── PasswordValidator.cs
    ├── EmailValidator.cs
    └── ValidationHelpers.cs
```

## 🚀 Execução Passo a Passo

### 1. Criar Validadores de Senha e Email Comuns

#### src/IDE.Application/Auth/Validators/Common/PasswordValidator.cs
```csharp
using FluentValidation;

namespace IDE.Application.Auth.Validators.Common;

/// <summary>
/// Validador especializado para senhas
/// </summary>
public static class PasswordValidator
{
    /// <summary>
    /// Regras básicas de senha
    /// </summary>
    public static IRuleBuilderOptions<T, string> Password<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty()
            .WithMessage("Senha é obrigatória")
            .MinimumLength(8)
            .WithMessage("Senha deve ter pelo menos 8 caracteres")
            .MaximumLength(128)
            .WithMessage("Senha deve ter no máximo 128 caracteres")
            .Matches(@"^(?=.*[a-z])")
            .WithMessage("Senha deve conter pelo menos uma letra minúscula")
            .Matches(@"^(?=.*[A-Z])")
            .WithMessage("Senha deve conter pelo menos uma letra maiúscula")
            .Matches(@"^(?=.*\d)")
            .WithMessage("Senha deve conter pelo menos um número")
            .Matches(@"^(?=.*[@$!%*?&])")
            .WithMessage("Senha deve conter pelo menos um caractere especial (@$!%*?&)")
            .Must(NotBeCommonPassword)
            .WithMessage("Esta senha é muito comum. Escolha uma senha mais segura")
            .Must(NotContainSequentialCharacters)
            .WithMessage("Senha não deve conter sequências de caracteres (ex: 123, abc)")
            .Must(NotContainRepeatedCharacters)
            .WithMessage("Senha não deve conter muitos caracteres repetidos");
    }

    /// <summary>
    /// Validação de confirmação de senha
    /// </summary>
    public static IRuleBuilderOptions<T, string> PasswordConfirmation<T>(
        this IRuleBuilder<T, string> ruleBuilder, 
        Func<T, string> passwordSelector)
    {
        return ruleBuilder
            .NotEmpty()
            .WithMessage("Confirmação de senha é obrigatória")
            .Equal(passwordSelector)
            .WithMessage("Senhas não coincidem");
    }

    /// <summary>
    /// Verifica se não é uma senha comum
    /// </summary>
    private static bool NotBeCommonPassword(string password)
    {
        if (string.IsNullOrEmpty(password)) return true;

        var commonPasswords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Top 50 senhas mais comuns
            "password", "123456", "123456789", "12345678", "12345", "qwerty",
            "abc123", "password123", "123123", "welcome", "admin", "letmein",
            "monkey", "dragon", "master", "sunshine", "iloveyou", "football",
            "baseball", "trustno1", "666666", "123321", "mustang", "access",
            "shadow", "passw0rd", "123qwe", "football1", "123abc", "password1",
            "qwerty123", "welcome123", "admin123", "user123", "test123",
            "Password123!", "123456789!", "Qwerty123!", "Password1!",
            "Admin123!", "Welcome123!", "Test123!", "User123!",
            "Abc123456!", "Password@123", "Admin@123", "User@123",
            // Senhas em português
            "senha123", "brasil123", "futebol", "corinthians", "flamengo"
        };

        return !commonPasswords.Contains(password);
    }

    /// <summary>
    /// Verifica sequências de caracteres
    /// </summary>
    private static bool NotContainSequentialCharacters(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 3) return true;

        var sequences = new[]
        {
            "0123456789", "abcdefghijklmnopqrstuvwxyz", "qwertyuiopasdfghjklzxcvbnm"
        };

        for (int i = 0; i <= password.Length - 3; i++)
        {
            var substring = password.Substring(i, 3).ToLowerInvariant();
            
            foreach (var sequence in sequences)
            {
                if (sequence.Contains(substring))
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Verifica caracteres repetidos excessivos
    /// </summary>
    private static bool NotContainRepeatedCharacters(string password)
    {
        if (string.IsNullOrEmpty(password)) return true;

        int maxRepeated = 0;
        int currentRepeated = 1;
        
        for (int i = 1; i < password.Length; i++)
        {
            if (password[i] == password[i - 1])
            {
                currentRepeated++;
            }
            else
            {
                maxRepeated = Math.Max(maxRepeated, currentRepeated);
                currentRepeated = 1;
            }
        }
        
        maxRepeated = Math.Max(maxRepeated, currentRepeated);
        return maxRepeated <= 2; // Máximo 2 caracteres iguais seguidos
    }

    /// <summary>
    /// Calcula força da senha (0-100)
    /// </summary>
    public static int CalculatePasswordStrength(string password)
    {
        if (string.IsNullOrEmpty(password)) return 0;

        int score = 0;

        // Comprimento
        if (password.Length >= 8) score += 10;
        if (password.Length >= 12) score += 10;
        if (password.Length >= 16) score += 10;

        // Caracteres diferentes
        if (password.Any(char.IsLower)) score += 10;
        if (password.Any(char.IsUpper)) score += 10;
        if (password.Any(char.IsDigit)) score += 10;
        if (password.Any(c => "@$!%*?&".Contains(c))) score += 10;

        // Diversidade
        var uniqueChars = password.Distinct().Count();
        if (uniqueChars >= password.Length * 0.7) score += 10;

        // Não é comum
        if (NotBeCommonPassword(password)) score += 10;

        // Não tem sequências
        if (NotContainSequentialCharacters(password)) score += 10;

        return Math.Min(score, 100);
    }
}
```

#### src/IDE.Application/Auth/Validators/Common/EmailValidator.cs
```csharp
using FluentValidation;
using IDE.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IDE.Application.Auth.Validators.Common;

/// <summary>
/// Validador especializado para emails
/// </summary>
public static class EmailValidator
{
    /// <summary>
    /// Regras básicas de email
    /// </summary>
    public static IRuleBuilderOptions<T, string> Email<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty()
            .WithMessage("Email é obrigatório")
            .EmailAddress()
            .WithMessage("Formato de email inválido")
            .MaximumLength(254)
            .WithMessage("Email deve ter no máximo 254 caracteres")
            .Must(NotBeDisposableEmail)
            .WithMessage("Emails temporários/descartáveis não são permitidos")
            .Must(HaveValidDomain)
            .WithMessage("Domínio do email é inválido");
    }

    /// <summary>
    /// Validação de email único no sistema
    /// </summary>
    public static IRuleBuilderOptions<T, string> UniqueEmail<T>(
        this IRuleBuilder<T, string> ruleBuilder, 
        ApplicationDbContext context,
        Guid? excludeUserId = null)
    {
        return ruleBuilder
            .Email()
            .MustAsync(async (email, cancellationToken) =>
            {
                if (string.IsNullOrEmpty(email)) return true;

                var normalizedEmail = email.Trim().ToLowerInvariant();
                var query = context.Users.Where(u => u.Email.ToLower() == normalizedEmail);
                
                if (excludeUserId.HasValue)
                {
                    query = query.Where(u => u.Id != excludeUserId.Value);
                }

                return !await query.AnyAsync(cancellationToken);
            })
            .WithMessage("Este email já está em uso");
    }

    /// <summary>
    /// Verifica se não é email descartável
    /// </summary>
    private static bool NotBeDisposableEmail(string email)
    {
        if (string.IsNullOrEmpty(email)) return true;

        var domain = email.Split('@').LastOrDefault()?.ToLowerInvariant();
        if (string.IsNullOrEmpty(domain)) return true;

        var disposableDomains = new HashSet<string>
        {
            // Principais serviços de email temporário
            "10minutemail.com", "guerrillamail.com", "mailinator.com", "temp-mail.org",
            "throwaway.email", "yopmail.com", "tempmail.plus", "maildrop.cc",
            "mailsac.com", "sharklasers.com", "mohmal.com", "emkei.cz",
            "tempmail.plus", "tempmailo.com", "dispostable.com", "fakemailgenerator.com",
            "mintemail.com", "emailondeck.com", "spambox.us", "trashmail.com",
            // Adicionar mais conforme necessário
        };

        return !disposableDomains.Contains(domain);
    }

    /// <summary>
    /// Valida domínio do email
    /// </summary>
    private static bool HaveValidDomain(string email)
    {
        if (string.IsNullOrEmpty(email)) return true;

        var parts = email.Split('@');
        if (parts.Length != 2) return false;

        var domain = parts[1];
        
        // Verificações básicas do domínio
        if (string.IsNullOrEmpty(domain)) return false;
        if (domain.Length > 253) return false;
        if (domain.StartsWith(".") || domain.EndsWith(".")) return false;
        if (domain.Contains("..")) return false;
        
        // Deve ter pelo menos um ponto
        if (!domain.Contains('.')) return false;
        
        // Verificar TLD
        var tld = domain.Split('.').LastOrDefault();
        if (string.IsNullOrEmpty(tld) || tld.Length < 2) return false;

        return true;
    }
}

/// <summary>
/// Validador para usernames
/// </summary>
public static class UsernameValidator
{
    /// <summary>
    /// Regras básicas de username
    /// </summary>
    public static IRuleBuilderOptions<T, string> Username<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty()
            .WithMessage("Nome de usuário é obrigatório")
            .MinimumLength(3)
            .WithMessage("Nome de usuário deve ter pelo menos 3 caracteres")
            .MaximumLength(50)
            .WithMessage("Nome de usuário deve ter no máximo 50 caracteres")
            .Matches(@"^[a-zA-Z0-9_.-]+$")
            .WithMessage("Nome de usuário pode conter apenas letras, números, underscore, hífen e ponto")
            .Must(NotStartOrEndWithSpecialChars)
            .WithMessage("Nome de usuário não pode começar ou terminar com caracteres especiais")
            .Must(NotBeReservedWord)
            .WithMessage("Este nome de usuário está reservado");
    }

    /// <summary>
    /// Validação de username único
    /// </summary>
    public static IRuleBuilderOptions<T, string> UniqueUsername<T>(
        this IRuleBuilder<T, string> ruleBuilder, 
        ApplicationDbContext context,
        Guid? excludeUserId = null)
    {
        return ruleBuilder
            .Username()
            .MustAsync(async (username, cancellationToken) =>
            {
                if (string.IsNullOrEmpty(username)) return true;

                var normalizedUsername = username.Trim().ToLowerInvariant();
                var query = context.Users.Where(u => u.Username.ToLower() == normalizedUsername);
                
                if (excludeUserId.HasValue)
                {
                    query = query.Where(u => u.Id != excludeUserId.Value);
                }

                return !await query.AnyAsync(cancellationToken);
            })
            .WithMessage("Este nome de usuário já está em uso");
    }

    /// <summary>
    /// Não pode começar ou terminar com caracteres especiais
    /// </summary>
    private static bool NotStartOrEndWithSpecialChars(string username)
    {
        if (string.IsNullOrEmpty(username)) return true;

        var specialChars = new[] { '_', '.', '-' };
        return !specialChars.Contains(username.First()) && 
               !specialChars.Contains(username.Last());
    }

    /// <summary>
    /// Verifica se não é palavra reservada
    /// </summary>
    private static bool NotBeReservedWord(string username)
    {
        if (string.IsNullOrEmpty(username)) return true;

        var reservedWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "admin", "administrator", "root", "api", "www", "mail", "email",
            "support", "help", "info", "contact", "about", "terms", "privacy",
            "login", "register", "signup", "signin", "logout", "profile",
            "dashboard", "settings", "config", "system", "null", "undefined",
            "true", "false", "test", "demo", "sample", "example",
            // Palavras em português
            "administrador", "suporte", "ajuda", "contato", "sobre", "termos",
            "privacidade", "entrar", "cadastro", "perfil", "configuracoes"
        };

        return !reservedWords.Contains(username);
    }
}
```

#### src/IDE.Application/Auth/Validators/Common/ValidationHelpers.cs
```csharp
using FluentValidation;

namespace IDE.Application.Auth.Validators.Common;

/// <summary>
/// Helpers comuns para validação
/// </summary>
public static class ValidationHelpers
{
    /// <summary>
    /// Validação de token (formato base64 URL-safe)
    /// </summary>
    public static IRuleBuilderOptions<T, string> Token<T>(
        this IRuleBuilder<T, string> ruleBuilder, 
        string tokenName = "Token")
    {
        return ruleBuilder
            .NotEmpty()
            .WithMessage($"{tokenName} é obrigatório")
            .MinimumLength(16)
            .WithMessage($"{tokenName} deve ter pelo menos 16 caracteres")
            .MaximumLength(255)
            .WithMessage($"{tokenName} deve ter no máximo 255 caracteres")
            .Matches(@"^[A-Za-z0-9_-]+$")
            .WithMessage($"{tokenName} contém caracteres inválidos");
    }

    /// <summary>
    /// Validação de código TOTP
    /// </summary>
    public static IRuleBuilderOptions<T, string> TotpCode<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty()
            .WithMessage("Código de verificação é obrigatório")
            .Length(6)
            .WithMessage("Código deve ter exatamente 6 dígitos")
            .Matches(@"^\d{6}$")
            .WithMessage("Código deve conter apenas números");
    }

    /// <summary>
    /// Validação de código de recuperação
    /// </summary>
    public static IRuleBuilderOptions<T, string> RecoveryCode<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty()
            .WithMessage("Código de recuperação é obrigatório")
            .Length(8, 12)
            .WithMessage("Código de recuperação deve ter entre 8 e 12 caracteres")
            .Matches(@"^[A-Za-z0-9]+$")
            .WithMessage("Código de recuperação contém caracteres inválidos");
    }

    /// <summary>
    /// Validação de nome de API Key
    /// </summary>
    public static IRuleBuilderOptions<T, string> ApiKeyName<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty()
            .WithMessage("Nome da API Key é obrigatório")
            .MinimumLength(3)
            .WithMessage("Nome deve ter pelo menos 3 caracteres")
            .MaximumLength(100)
            .WithMessage("Nome deve ter no máximo 100 caracteres")
            .Matches(@"^[a-zA-Z0-9\s_.-]+$")
            .WithMessage("Nome pode conter apenas letras, números, espaços e alguns caracteres especiais");
    }

    /// <summary>
    /// Validação de data futura
    /// </summary>
    public static IRuleBuilderOptions<T, DateTime?> FutureDate<T>(
        this IRuleBuilder<T, DateTime?> ruleBuilder,
        string fieldName = "Data")
    {
        return ruleBuilder
            .Must(date => !date.HasValue || date.Value > DateTime.UtcNow)
            .WithMessage($"{fieldName} deve ser uma data futura");
    }

    /// <summary>
    /// Validação de data dentro de um período
    /// </summary>
    public static IRuleBuilderOptions<T, DateTime?> WithinPeriod<T>(
        this IRuleBuilder<T, DateTime?> ruleBuilder,
        TimeSpan maxPeriod,
        string fieldName = "Data")
    {
        return ruleBuilder
            .Must(date => !date.HasValue || date.Value <= DateTime.UtcNow.Add(maxPeriod))
            .WithMessage($"{fieldName} não pode ser superior a {maxPeriod.Days} dias no futuro");
    }

    /// <summary>
    /// Validação de IP address
    /// </summary>
    public static IRuleBuilderOptions<T, string> IpAddress<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .Matches(@"^(?:[0-9]{1,3}\.){3}[0-9]{1,3}$|^([0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}$")
            .WithMessage("Formato de endereço IP inválido");
    }

    /// <summary>
    /// Validação de User Agent
    /// </summary>
    public static IRuleBuilderOptions<T, string> UserAgent<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .MaximumLength(500)
            .WithMessage("User Agent deve ter no máximo 500 caracteres")
            .Must(NotBeEmpty)
            .WithMessage("User Agent não pode estar vazio");
    }

    private static bool NotBeEmpty(string userAgent)
    {
        return !string.IsNullOrWhiteSpace(userAgent);
    }

    /// <summary>
    /// Validação personalizada com contexto
    /// </summary>
    public static IRuleBuilderOptions<T, TProperty> WithContext<T, TProperty>(
        this IRuleBuilderOptions<T, TProperty> rule,
        string contextKey,
        object contextValue)
    {
        return rule.Configure(config => 
        {
            config.MessageBuilder = context =>
            {
                context.MessageFormatter.AppendArgument(contextKey, contextValue);
                return context.GetDefaultMessage();
            };
        });
    }
}
```

### 2. Criar Validators para Requests Principais

#### src/IDE.Application/Auth/Validators/RegisterRequestValidator.cs
```csharp
using FluentValidation;
using IDE.Application.Auth.Requests;
using IDE.Application.Auth.Validators.Common;
using IDE.Infrastructure.Data;

namespace IDE.Application.Auth.Validators;

/// <summary>
/// Validator para registro de usuário
/// </summary>
public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    private readonly ApplicationDbContext _context;

    public RegisterRequestValidator(ApplicationDbContext context)
    {
        _context = context;

        // Email
        RuleFor(x => x.Email)
            .UniqueEmail(_context)
            .WithName("Email");

        // Username
        RuleFor(x => x.Username)
            .UniqueUsername(_context)
            .WithName("Nome de usuário");

        // Password
        RuleFor(x => x.Password)
            .Password()
            .WithName("Senha");

        // Password confirmation
        RuleFor(x => x.PasswordConfirm)
            .PasswordConfirmation(x => x.Password)
            .WithName("Confirmação de senha");

        // First name
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .WithMessage("Primeiro nome é obrigatório")
            .MinimumLength(2)
            .WithMessage("Primeiro nome deve ter pelo menos 2 caracteres")
            .MaximumLength(100)
            .WithMessage("Primeiro nome deve ter no máximo 100 caracteres")
            .Matches(@"^[a-zA-ZÀ-ÿ\s]+$")
            .WithMessage("Primeiro nome pode conter apenas letras e espaços");

        // Last name
        RuleFor(x => x.LastName)
            .NotEmpty()
            .WithMessage("Último nome é obrigatório")
            .MinimumLength(2)
            .WithMessage("Último nome deve ter pelo menos 2 caracteres")
            .MaximumLength(100)
            .WithMessage("Último nome deve ter no máximo 100 caracteres")
            .Matches(@"^[a-zA-ZÀ-ÿ\s]+$")
            .WithMessage("Último nome pode conter apenas letras e espaços");

        // Terms acceptance
        RuleFor(x => x.AcceptTerms)
            .Equal(true)
            .WithMessage("Você deve aceitar os termos de uso");

        // Marketing acceptance (optional)
        RuleFor(x => x.AcceptMarketing)
            .NotNull()
            .WithMessage("Especifique se deseja receber emails de marketing");

        // Captcha token (when required)
        When(x => !string.IsNullOrEmpty(x.CaptchaToken), () =>
        {
            RuleFor(x => x.CaptchaToken)
                .MinimumLength(10)
                .WithMessage("Token de captcha inválido");
        });

        // Invite code (when provided)
        When(x => !string.IsNullOrEmpty(x.InviteCode), () =>
        {
            RuleFor(x => x.InviteCode)
                .MinimumLength(6)
                .MaximumLength(20)
                .WithMessage("Código de convite deve ter entre 6 e 20 caracteres")
                .Matches(@"^[A-Za-z0-9]+$")
                .WithMessage("Código de convite contém caracteres inválidos");
        });
    }
}
```

#### src/IDE.Application/Auth/Validators/LoginRequestValidator.cs
```csharp
using FluentValidation;
using IDE.Application.Auth.Requests;
using IDE.Application.Auth.Validators.Common;

namespace IDE.Application.Auth.Validators;

/// <summary>
/// Validator para login de usuário
/// </summary>
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        // Email or username
        RuleFor(x => x.EmailOrUsername)
            .NotEmpty()
            .WithMessage("Email ou nome de usuário é obrigatório")
            .MinimumLength(3)
            .WithMessage("Email ou nome de usuário deve ter pelo menos 3 caracteres")
            .MaximumLength(254)
            .WithMessage("Email ou nome de usuário deve ter no máximo 254 caracteres");

        // Password
        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Senha é obrigatória")
            .MinimumLength(1)
            .WithMessage("Senha não pode estar vazia")
            .MaximumLength(128)
            .WithMessage("Senha deve ter no máximo 128 caracteres");

        // Device info
        RuleFor(x => x.DeviceInfo)
            .MaximumLength(500)
            .WithMessage("Informações do dispositivo devem ter no máximo 500 caracteres");

        // Two factor code (when provided)
        When(x => !string.IsNullOrEmpty(x.TwoFactorCode), () =>
        {
            RuleFor(x => x.TwoFactorCode)
                .TotpCode()
                .WithName("Código de dois fatores");
        });

        // Two factor token (when provided)
        When(x => !string.IsNullOrEmpty(x.TwoFactorToken), () =>
        {
            RuleFor(x => x.TwoFactorToken)
                .Token("Token de dois fatores")
                .WithName("Token de dois fatores");
        });

        // Recovery code (when provided)
        When(x => !string.IsNullOrEmpty(x.RecoveryCode), () =>
        {
            RuleFor(x => x.RecoveryCode)
                .RecoveryCode()
                .WithName("Código de recuperação");
        });

        // Captcha token (when provided)
        When(x => !string.IsNullOrEmpty(x.CaptchaToken), () =>
        {
            RuleFor(x => x.CaptchaToken)
                .MinimumLength(10)
                .WithMessage("Token de captcha inválido");
        });

        // Business rules
        RuleFor(x => x)
            .Must(HaveOnlyOneTwoFactorMethod)
            .WithMessage("Forneça apenas um método de autenticação de dois fatores")
            .WithName("Autenticação");
    }

    /// <summary>
    /// Usuário deve fornecer apenas um método de 2FA
    /// </summary>
    private static bool HaveOnlyOneTwoFactorMethod(LoginRequest request)
    {
        var methods = new[]
        {
            !string.IsNullOrEmpty(request.TwoFactorCode),
            !string.IsNullOrEmpty(request.RecoveryCode)
        };

        return methods.Count(m => m) <= 1;
    }
}
```

#### src/IDE.Application/Auth/Validators/PasswordResetRequestValidator.cs
```csharp
using FluentValidation;
using IDE.Application.Auth.Requests;
using IDE.Application.Auth.Validators.Common;

namespace IDE.Application.Auth.Validators;

/// <summary>
/// Validator para solicitação de reset de senha
/// </summary>
public class PasswordResetRequestValidator : AbstractValidator<PasswordResetRequest>
{
    public PasswordResetRequestValidator()
    {
        // Email
        RuleFor(x => x.Email)
            .Email()
            .WithName("Email");

        // Captcha token (when provided)
        When(x => !string.IsNullOrEmpty(x.CaptchaToken), () =>
        {
            RuleFor(x => x.CaptchaToken)
                .MinimumLength(10)
                .WithMessage("Token de captcha inválido");
        });
    }
}

/// <summary>
/// Validator para confirmação de reset de senha
/// </summary>
public class PasswordResetConfirmRequestValidator : AbstractValidator<PasswordResetConfirmRequest>
{
    public PasswordResetConfirmRequestValidator()
    {
        // Token
        RuleFor(x => x.Token)
            .Token("Token de reset")
            .WithName("Token");

        // New password
        RuleFor(x => x.NewPassword)
            .Password()
            .WithName("Nova senha");

        // Password confirmation
        RuleFor(x => x.NewPasswordConfirm)
            .PasswordConfirmation(x => x.NewPassword)
            .WithName("Confirmação da nova senha");
    }
}
```

#### src/IDE.Application/Auth/Validators/EmailVerificationRequestValidator.cs
```csharp
using FluentValidation;
using IDE.Application.Auth.Requests;
using IDE.Application.Auth.Validators.Common;

namespace IDE.Application.Auth.Validators;

/// <summary>
/// Validator para verificação de email
/// </summary>
public class EmailVerificationRequestValidator : AbstractValidator<EmailVerificationRequest>
{
    public EmailVerificationRequestValidator()
    {
        RuleFor(x => x.Token)
            .Token("Token de verificação")
            .WithName("Token");
    }
}

/// <summary>
/// Validator para reenvio de email de verificação
/// </summary>
public class ResendEmailVerificationRequestValidator : AbstractValidator<ResendEmailVerificationRequest>
{
    public ResendEmailVerificationRequestValidator()
    {
        RuleFor(x => x.Email)
            .Email()
            .WithName("Email");
    }
}
```

#### src/IDE.Application/Auth/Validators/ApiKeyCreateRequestValidator.cs
```csharp
using FluentValidation;
using IDE.Application.Auth.Requests;
using IDE.Application.Auth.Validators.Common;

namespace IDE.Application.Auth.Validators;

/// <summary>
/// Validator para criação de API Key
/// </summary>
public class ApiKeyCreateRequestValidator : AbstractValidator<ApiKeyCreateRequest>
{
    public ApiKeyCreateRequestValidator()
    {
        // Name
        RuleFor(x => x.Name)
            .ApiKeyName()
            .WithName("Nome da API Key");

        // Expires at
        RuleFor(x => x.ExpiresAt)
            .FutureDate("Data de expiração")
            .WithinPeriod(TimeSpan.FromDays(365), "Data de expiração")
            .WithName("Data de expiração");

        // Scopes
        RuleFor(x => x.Scopes)
            .NotNull()
            .WithMessage("Escopos são obrigatórios")
            .Must(HaveValidScopes)
            .WithMessage("Escopos contêm valores inválidos");

        // Description
        When(x => !string.IsNullOrEmpty(x.Description), () =>
        {
            RuleFor(x => x.Description)
                .MaximumLength(500)
                .WithMessage("Descrição deve ter no máximo 500 caracteres");
        });
    }

    /// <summary>
    /// Valida escopos da API Key
    /// </summary>
    private static bool HaveValidScopes(List<string> scopes)
    {
        if (scopes == null) return false;
        if (scopes.Count == 0) return false;

        var validScopes = new HashSet<string>
        {
            "read", "write", "delete", "admin",
            "workspaces:read", "workspaces:write", "workspaces:delete",
            "users:read", "users:write",
            "files:read", "files:write", "files:delete"
        };

        return scopes.All(scope => validScopes.Contains(scope));
    }
}

/// <summary>
/// Validator para atualização de API Key
/// </summary>
public class ApiKeyUpdateRequestValidator : AbstractValidator<ApiKeyUpdateRequest>
{
    public ApiKeyUpdateRequestValidator()
    {
        // Name (when provided)
        When(x => !string.IsNullOrEmpty(x.Name), () =>
        {
            RuleFor(x => x.Name)
                .ApiKeyName()
                .WithName("Nome da API Key");
        });

        // Scopes (when provided)
        When(x => x.Scopes != null, () =>
        {
            RuleFor(x => x.Scopes)
                .Must(HaveValidScopes)
                .WithMessage("Escopos contêm valores inválidos");
        });
    }

    private static bool HaveValidScopes(List<string>? scopes)
    {
        if (scopes == null) return true;
        if (scopes.Count == 0) return false;

        var validScopes = new HashSet<string>
        {
            "read", "write", "delete", "admin",
            "workspaces:read", "workspaces:write", "workspaces:delete",
            "users:read", "users:write",
            "files:read", "files:write", "files:delete"
        };

        return scopes.All(scope => validScopes.Contains(scope));
    }
}
```

#### src/IDE.Application/Auth/Validators/TwoFactorRequestValidators.cs
```csharp
using FluentValidation;
using IDE.Application.Auth.Requests;
using IDE.Application.Auth.Validators.Common;
using IDE.Domain.Enums;

namespace IDE.Application.Auth.Validators;

/// <summary>
/// Validator para setup de 2FA
/// </summary>
public class TwoFactorSetupRequestValidator : AbstractValidator<TwoFactorSetupRequest>
{
    public TwoFactorSetupRequestValidator()
    {
        RuleFor(x => x.Method)
            .IsInEnum()
            .WithMessage("Método de dois fatores inválido")
            .NotEqual(TwoFactorMethod.None)
            .WithMessage("Deve especificar um método de dois fatores válido");
    }
}

/// <summary>
/// Validator para habilitar 2FA
/// </summary>
public class EnableTwoFactorRequestValidator : AbstractValidator<EnableTwoFactorRequest>
{
    public EnableTwoFactorRequestValidator()
    {
        // Method
        RuleFor(x => x.Method)
            .IsInEnum()
            .WithMessage("Método de dois fatores inválido")
            .NotEqual(TwoFactorMethod.None)
            .WithMessage("Deve especificar um método de dois fatores válido");

        // Code
        RuleFor(x => x.Code)
            .TotpCode()
            .WithName("Código de verificação");

        // Current password
        RuleFor(x => x.CurrentPassword)
            .NotEmpty()
            .WithMessage("Senha atual é obrigatória")
            .MinimumLength(1)
            .WithMessage("Senha atual não pode estar vazia");
    }
}

/// <summary>
/// Validator para desabilitar 2FA
/// </summary>
public class DisableTwoFactorRequestValidator : AbstractValidator<DisableTwoFactorRequest>
{
    public DisableTwoFactorRequestValidator()
    {
        // Code (TOTP or recovery)
        RuleFor(x => x.Code)
            .NotEmpty()
            .WithMessage("Código é obrigatório")
            .Must(BeValidCode)
            .WithMessage("Código deve ser um TOTP de 6 dígitos ou código de recuperação");

        // Current password
        RuleFor(x => x.CurrentPassword)
            .NotEmpty()
            .WithMessage("Senha atual é obrigatória")
            .MinimumLength(1)
            .WithMessage("Senha atual não pode estar vazia");
    }

    /// <summary>
    /// Valida se é código TOTP ou de recuperação válido
    /// </summary>
    private static bool BeValidCode(string code)
    {
        if (string.IsNullOrEmpty(code)) return false;

        // TOTP code (6 digits)
        if (code.Length == 6 && code.All(char.IsDigit)) return true;

        // Recovery code (8-12 alphanumeric)
        if (code.Length >= 8 && code.Length <= 12 && code.All(char.IsLetterOrDigit)) return true;

        return false;
    }
}
```

#### src/IDE.Application/Auth/Validators/OAuthLoginRequestValidator.cs
```csharp
using FluentValidation;
using IDE.Application.Auth.Requests;

namespace IDE.Application.Auth.Validators;

/// <summary>
/// Validator para login OAuth
/// </summary>
public class OAuthLoginRequestValidator : AbstractValidator<OAuthLoginRequest>
{
    public OAuthLoginRequestValidator()
    {
        // Provider
        RuleFor(x => x.Provider)
            .NotEmpty()
            .WithMessage("Provedor OAuth é obrigatório")
            .Must(BeValidProvider)
            .WithMessage("Provedor OAuth não suportado");

        // Code
        RuleFor(x => x.Code)
            .NotEmpty()
            .WithMessage("Código de autorização é obrigatório")
            .MinimumLength(10)
            .WithMessage("Código de autorização inválido")
            .MaximumLength(2000)
            .WithMessage("Código de autorização muito longo");

        // Redirect URI
        RuleFor(x => x.RedirectUri)
            .NotEmpty()
            .WithMessage("URI de redirecionamento é obrigatória")
            .Must(BeValidUri)
            .WithMessage("URI de redirecionamento inválida");

        // State (when provided)
        When(x => !string.IsNullOrEmpty(x.State), () =>
        {
            RuleFor(x => x.State)
                .MinimumLength(8)
                .WithMessage("State deve ter pelo menos 8 caracteres")
                .MaximumLength(255)
                .WithMessage("State deve ter no máximo 255 caracteres");
        });

        // Device info
        RuleFor(x => x.DeviceInfo)
            .MaximumLength(500)
            .WithMessage("Informações do dispositivo devem ter no máximo 500 caracteres");
    }

    /// <summary>
    /// Valida se o provedor é suportado
    /// </summary>
    private static bool BeValidProvider(string provider)
    {
        if (string.IsNullOrEmpty(provider)) return false;

        var supportedProviders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Google", "GitHub", "Microsoft", "Facebook", "Twitter"
        };

        return supportedProviders.Contains(provider);
    }

    /// <summary>
    /// Valida URI de redirecionamento
    /// </summary>
    private static bool BeValidUri(string uri)
    {
        if (string.IsNullOrEmpty(uri)) return false;

        return Uri.TryCreate(uri, UriKind.Absolute, out var result) &&
               (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
    }
}

/// <summary>
/// Validator para refresh token
/// </summary>
public class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        // Refresh token
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .WithMessage("Refresh token é obrigatório")
            .MinimumLength(16)
            .WithMessage("Refresh token inválido")
            .MaximumLength(255)
            .WithMessage("Refresh token muito longo");

        // Expired access token (when provided)
        When(x => !string.IsNullOrEmpty(x.ExpiredAccessToken), () =>
        {
            RuleFor(x => x.ExpiredAccessToken)
                .MinimumLength(16)
                .WithMessage("Access token inválido")
                .MaximumLength(2000)
                .WithMessage("Access token muito longo");
        });

        // Device info
        RuleFor(x => x.DeviceInfo)
            .MaximumLength(500)
            .WithMessage("Informações do dispositivo devem ter no máximo 500 caracteres");
    }
}
```

### 3. Validar Implementação

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

- [ ] **Validators para todos os requests** implementados
- [ ] **Regras de senha segura** funcionando corretamente
- [ ] **Validações de email e username únicos** configuradas
- [ ] **Validators de 2FA** completos
- [ ] **Validações de API Keys** implementadas
- [ ] **Helpers de validação** reutilizáveis criados
- [ ] **Compilação bem-sucedida** sem erros

## 📝 Arquivos Criados

Esta parte criará aproximadamente **12 arquivos**:
- 3 Helpers comuns de validação
- 9 Validators específicos para requests

## 🔄 Próximos Passos

Após concluir esta parte, você estará pronto para:
- **Parte 7**: Serviços de Autenticação Base
- Implementar IAuthService e AuthService
- Configurar JWT Token Generator

## 🚨 Troubleshooting Comum

**Erros de compilação**: Verifique se todos os using statements estão corretos  
**Validações não funcionando**: FluentValidation será configurado no DI  
**Referências circulares**: ApplicationDbContext será injetado via DI  

---
**⏱️ Tempo estimado**: 15-25 minutos  
**🎯 Próxima parte**: 07-servicos-autenticacao-base.md  
**📋 Dependências**: Partes 1-5 concluídas