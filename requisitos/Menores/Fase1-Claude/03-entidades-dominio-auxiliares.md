# Parte 3: Entidades de Domínio Auxiliares

## 📋 Visão Geral
**Duração**: 15-20 minutos  
**Complexidade**: Baixa  
**Dependências**: Parte 1 (Setup) + Parte 2 (Entidades Core)

Esta parte completa as entidades de domínio restantes, focando nas entidades de suporte como ApiKey, UserLoginHistory, EmailTemplate e configurações do sistema.

## 🎯 Objetivos
- ✅ Completar entidade ApiKey com todas as funcionalidades de segurança
- ✅ Implementar UserLoginHistory para auditoria de logins
- ✅ Criar EmailTemplate para sistema de email
- ✅ Implementar SystemConfiguration para configurações dinâmicas
- ✅ Criar PlanLimits para controle de planos
- ✅ Implementar SecurityConfiguration para políticas de segurança

## 📁 Arquivos a serem Criados/Atualizados

```
src/IDE.Domain/Entities/
├── ApiKey.cs (atualizar)
├── UserLoginHistory.cs (atualizar)
├── EmailTemplate.cs (novo)
├── SystemConfiguration.cs (novo)
├── PlanLimits.cs (novo)
└── SecurityConfiguration.cs (novo)
```

## 🚀 Execução Passo a Passo

### 1. Completar Entidade ApiKey

#### src/IDE.Domain/Entities/ApiKey.cs
```csharp
using System.ComponentModel.DataAnnotations;

namespace IDE.Domain.Entities;

/// <summary>
/// API Key para autenticação programática
/// </summary>
public class ApiKey
{
    /// <summary>
    /// Identificador único da API Key
    /// </summary>
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Nome descritivo da API Key
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Chave pública (prefixo sk_ + 32 chars) - visível ao usuário
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Hash da chave completa para validação (BCrypt)
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>
    /// Data de expiração da API Key
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Indica se a API Key está ativa
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Data de criação da API Key
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Data da última utilização
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// IP do último uso
    /// </summary>
    [MaxLength(45)] // IPv6 max length
    public string LastUsedIp { get; set; } = string.Empty;

    /// <summary>
    /// Contador de uso da API Key
    /// </summary>
    [Range(0, int.MaxValue)]
    public int UsageCount { get; set; } = 0;

    /// <summary>
    /// Escopo de permissões (JSON com permissões específicas)
    /// </summary>
    [MaxLength(1000)]
    public string Scopes { get; set; } = "[]"; // JSON array

    // Relacionamento com usuário

    /// <summary>
    /// ID do usuário proprietário da API Key
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Usuário proprietário da API Key
    /// </summary>
    public virtual User User { get; set; } = null!;

    // Propriedades computadas

    /// <summary>
    /// Indica se a API Key está expirada
    /// </summary>
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    /// <summary>
    /// Indica se a API Key é válida (ativa e não expirada)
    /// </summary>
    public bool IsValid => IsActive && !IsExpired;

    /// <summary>
    /// Tempo restante até a expiração
    /// </summary>
    public TimeSpan TimeUntilExpiration => ExpiresAt - DateTime.UtcNow;

    /// <summary>
    /// Indica se está próxima da expiração (menos de 7 dias)
    /// </summary>
    public bool IsNearExpiration => IsValid && TimeUntilExpiration.TotalDays < 7;

    // Métodos auxiliares

    /// <summary>
    /// Desativa a API Key
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }

    /// <summary>
    /// Reativa a API Key (se não expirada)
    /// </summary>
    public void Reactivate()
    {
        if (!IsExpired)
            IsActive = true;
    }

    /// <summary>
    /// Registra o uso da API Key
    /// </summary>
    /// <param name="ipAddress">IP que utilizou a chave</param>
    public void RecordUsage(string ipAddress)
    {
        LastUsedAt = DateTime.UtcNow;
        LastUsedIp = ipAddress ?? string.Empty;
        UsageCount++;
    }

    /// <summary>
    /// Cria uma nova API Key
    /// </summary>
    /// <param name="userId">ID do usuário</param>
    /// <param name="name">Nome da chave</param>
    /// <param name="expirationDays">Dias até expiração (padrão: 90)</param>
    /// <returns>Nova API Key</returns>
    public static ApiKey Create(Guid userId, string name, int expirationDays = 90)
    {
        var keyValue = GenerateApiKey();
        
        return new ApiKey
        {
            UserId = userId,
            Name = name,
            Key = keyValue,
            KeyHash = HashKey(keyValue),
            ExpiresAt = DateTime.UtcNow.AddDays(expirationDays)
        };
    }

    /// <summary>
    /// Gera uma nova API Key no formato sk_{32_chars}
    /// </summary>
    /// <returns>API Key gerada</returns>
    private static string GenerateApiKey()
    {
        var randomBytes = new byte[24]; // 24 bytes = 32 chars em base64
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        
        var key = Convert.ToBase64String(randomBytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "")
            .Substring(0, 32);
            
        return $"sk_{key}";
    }

    /// <summary>
    /// Cria hash da API Key para validação
    /// </summary>
    /// <param name="key">Chave para hash</param>
    /// <returns>Hash BCrypt da chave</returns>
    private static string HashKey(string key)
    {
        // Note: BCrypt será usado quando disponível na infraestrutura
        // Por enquanto, simulamos com hash simples
        return Convert.ToBase64String(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(key)));
    }
}
```

### 2. Completar UserLoginHistory

#### src/IDE.Domain/Entities/UserLoginHistory.cs
```csharp
using System.ComponentModel.DataAnnotations;

namespace IDE.Domain.Entities;

/// <summary>
/// Histórico de tentativas de login dos usuários
/// </summary>
public class UserLoginHistory
{
    /// <summary>
    /// Identificador único do registro
    /// </summary>
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Data e hora da tentativa de login
    /// </summary>
    public DateTime LoginAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Endereço IP de origem
    /// </summary>
    [Required]
    [MaxLength(45)] // IPv6 max length
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// User Agent do browser/aplicação
    /// </summary>
    [MaxLength(500)]
    public string UserAgent { get; set; } = string.Empty;

    /// <summary>
    /// País de origem (baseado no IP)
    /// </summary>
    [MaxLength(100)]
    public string Country { get; set; } = string.Empty;

    /// <summary>
    /// Cidade de origem (baseado no IP)
    /// </summary>
    [MaxLength(100)]
    public string City { get; set; } = string.Empty;

    /// <summary>
    /// Indica se o login foi bem-sucedido
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Motivo da falha (se aplicável)
    /// </summary>
    [MaxLength(255)]
    public string FailureReason { get; set; } = string.Empty;

    /// <summary>
    /// Método de login utilizado
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string LoginMethod { get; set; } = string.Empty; // Password, OAuth, ApiKey, TwoFactor

    /// <summary>
    /// Informações adicionais sobre o dispositivo
    /// </summary>
    [MaxLength(500)]
    public string DeviceInfo { get; set; } = string.Empty;

    /// <summary>
    /// Indica se foi detectado como suspeito
    /// </summary>
    public bool IsSuspicious { get; set; } = false;

    /// <summary>
    /// Tempo gasto na tentativa de login (ms)
    /// </summary>
    [Range(0, int.MaxValue)]
    public int LoginDurationMs { get; set; } = 0;

    // Relacionamento com usuário

    /// <summary>
    /// ID do usuário (pode ser null se login falhou por usuário inexistente)
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Usuário relacionado ao login
    /// </summary>
    public virtual User? User { get; set; }

    // Propriedades computadas

    /// <summary>
    /// Indica se é um login de um novo dispositivo/localização
    /// </summary>
    public bool IsFromNewLocation { get; set; } = false;

    /// <summary>
    /// Score de risco da tentativa (0-100)
    /// </summary>
    [Range(0, 100)]
    public int RiskScore { get; set; } = 0;

    // Métodos auxiliares

    /// <summary>
    /// Cria um registro de login bem-sucedido
    /// </summary>
    /// <param name="userId">ID do usuário</param>
    /// <param name="ipAddress">IP de origem</param>
    /// <param name="userAgent">User Agent</param>
    /// <param name="loginMethod">Método utilizado</param>
    /// <returns>Novo registro de histórico</returns>
    public static UserLoginHistory CreateSuccess(
        Guid userId, 
        string ipAddress, 
        string userAgent, 
        string loginMethod)
    {
        return new UserLoginHistory
        {
            UserId = userId,
            IpAddress = ipAddress,
            UserAgent = userAgent ?? string.Empty,
            LoginMethod = loginMethod,
            IsSuccess = true
        };
    }

    /// <summary>
    /// Cria um registro de login falhado
    /// </summary>
    /// <param name="ipAddress">IP de origem</param>
    /// <param name="userAgent">User Agent</param>
    /// <param name="loginMethod">Método tentado</param>
    /// <param name="failureReason">Motivo da falha</param>
    /// <param name="userId">ID do usuário (se conhecido)</param>
    /// <returns>Novo registro de histórico</returns>
    public static UserLoginHistory CreateFailure(
        string ipAddress,
        string userAgent,
        string loginMethod,
        string failureReason,
        Guid? userId = null)
    {
        return new UserLoginHistory
        {
            UserId = userId,
            IpAddress = ipAddress,
            UserAgent = userAgent ?? string.Empty,
            LoginMethod = loginMethod,
            FailureReason = failureReason,
            IsSuccess = false
        };
    }

    /// <summary>
    /// Calcula score de risco baseado em diversos fatores
    /// </summary>
    public void CalculateRiskScore()
    {
        var score = 0;

        // Localização desconhecida
        if (IsFromNewLocation) score += 30;

        // Tentativa falhada
        if (!IsSuccess) score += 20;

        // Login fora do horário normal (exemplo: madrugada)
        if (LoginAt.Hour < 6 || LoginAt.Hour > 22) score += 10;

        // User Agent suspeito (muito simples ou muito específico)
        if (string.IsNullOrEmpty(UserAgent) || UserAgent.Length < 10) score += 15;

        // IP suspeito (seria implementado com serviços de geolocalização)
        if (IsSuspicious) score += 40;

        RiskScore = Math.Min(score, 100);
    }
}
```

### 3. Criar EmailTemplate

#### src/IDE.Domain/Entities/EmailTemplate.cs
```csharp
using System.ComponentModel.DataAnnotations;

namespace IDE.Domain.Entities;

/// <summary>
/// Template de email para diferentes tipos de comunicação
/// </summary>
public class EmailTemplate
{
    /// <summary>
    /// Identificador único do template
    /// </summary>
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Nome identificador do template
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty; // Welcome, EmailVerification, PasswordReset, etc.

    /// <summary>
    /// Assunto do email (pode conter placeholders)
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Corpo do email em HTML (pode conter placeholders)
    /// </summary>
    [Required]
    public string HtmlBody { get; set; } = string.Empty;

    /// <summary>
    /// Corpo do email em texto puro (pode conter placeholders)
    /// </summary>
    public string TextBody { get; set; } = string.Empty;

    /// <summary>
    /// Idioma do template
    /// </summary>
    [Required]
    [MaxLength(10)]
    public string Language { get; set; } = "pt-BR";

    /// <summary>
    /// Indica se o template está ativo
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Categoria do template (Auth, Notification, Marketing)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Prioridade do template (1-5, sendo 1 mais alta)
    /// </summary>
    [Range(1, 5)]
    public int Priority { get; set; } = 3;

    /// <summary>
    /// Data de criação do template
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Data da última atualização
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Versão do template para controle de mudanças
    /// </summary>
    [Range(1, int.MaxValue)]
    public int Version { get; set; } = 1;

    /// <summary>
    /// Lista de variáveis disponíveis no template (JSON)
    /// </summary>
    public string AvailableVariables { get; set; } = "[]";

    // Propriedades computadas

    /// <summary>
    /// Indica se tem versão em texto
    /// </summary>
    public bool HasTextVersion => !string.IsNullOrEmpty(TextBody);

    /// <summary>
    /// Conta de variáveis no template
    /// </summary>
    public int VariableCount => 
        HtmlBody.Split("{{").Length - 1 + Subject.Split("{{").Length - 1;

    // Métodos auxiliares

    /// <summary>
    /// Substitui variáveis no template
    /// </summary>
    /// <param name="variables">Dicionário de variáveis</param>
    /// <returns>Template com variáveis substituídas</returns>
    public EmailTemplate ReplaceVariables(Dictionary<string, string> variables)
    {
        var processedTemplate = new EmailTemplate
        {
            Id = Id,
            Name = Name,
            Subject = Subject,
            HtmlBody = HtmlBody,
            TextBody = TextBody,
            Language = Language,
            IsActive = IsActive,
            Category = Category,
            Priority = Priority,
            Version = Version
        };

        foreach (var variable in variables)
        {
            var placeholder = $"{{{{{variable.Key}}}}}";
            processedTemplate.Subject = processedTemplate.Subject.Replace(placeholder, variable.Value);
            processedTemplate.HtmlBody = processedTemplate.HtmlBody.Replace(placeholder, variable.Value);
            processedTemplate.TextBody = processedTemplate.TextBody.Replace(placeholder, variable.Value);
        }

        return processedTemplate;
    }

    /// <summary>
    /// Valida se o template contém todas as variáveis obrigatórias
    /// </summary>
    /// <param name="requiredVariables">Lista de variáveis obrigatórias</param>
    /// <returns>True se válido</returns>
    public bool ValidateRequiredVariables(List<string> requiredVariables)
    {
        var templateContent = $"{Subject} {HtmlBody} {TextBody}";
        return requiredVariables.All(variable => 
            templateContent.Contains($"{{{{{variable}}}}}"));
    }

    /// <summary>
    /// Cria template padrão de boas-vindas
    /// </summary>
    public static EmailTemplate CreateWelcomeTemplate()
    {
        return new EmailTemplate
        {
            Name = "Welcome",
            Subject = "Bem-vindo ao {{AppName}}, {{FirstName}}!",
            HtmlBody = @"
                <h1>Bem-vindo, {{FirstName}}!</h1>
                <p>Sua conta foi criada com sucesso em {{AppName}}.</p>
                <p>Email: {{Email}}</p>
                <p>Data de criação: {{CreatedAt}}</p>
                <hr>
                <p>Equipe {{AppName}}</p>",
            TextBody = @"
                Bem-vindo, {{FirstName}}!
                
                Sua conta foi criada com sucesso em {{AppName}}.
                Email: {{Email}}
                Data de criação: {{CreatedAt}}
                
                Equipe {{AppName}}",
            Category = "Auth",
            Priority = 2,
            AvailableVariables = "[\"AppName\", \"FirstName\", \"Email\", \"CreatedAt\"]"
        };
    }

    /// <summary>
    /// Cria template padrão de verificação de email
    /// </summary>
    public static EmailTemplate CreateEmailVerificationTemplate()
    {
        return new EmailTemplate
        {
            Name = "EmailVerification",
            Subject = "Verifique seu email - {{AppName}}",
            HtmlBody = @"
                <h1>Verificação de Email</h1>
                <p>Olá, {{FirstName}}!</p>
                <p>Clique no link abaixo para verificar seu email:</p>
                <a href='{{VerificationUrl}}'>Verificar Email</a>
                <p>Este link expira em {{ExpirationTime}}.</p>
                <hr>
                <p>Se você não criou esta conta, ignore este email.</p>
                <p>Equipe {{AppName}}</p>",
            TextBody = @"
                Verificação de Email
                
                Olá, {{FirstName}}!
                
                Acesse o link abaixo para verificar seu email:
                {{VerificationUrl}}
                
                Este link expira em {{ExpirationTime}}.
                
                Se você não criou esta conta, ignore este email.
                
                Equipe {{AppName}}",
            Category = "Auth",
            Priority = 1,
            AvailableVariables = "[\"AppName\", \"FirstName\", \"VerificationUrl\", \"ExpirationTime\"]"
        };
    }

    /// <summary>
    /// Cria template padrão de reset de senha
    /// </summary>
    public static EmailTemplate CreatePasswordResetTemplate()
    {
        return new EmailTemplate
        {
            Name = "PasswordReset",
            Subject = "Reset de senha - {{AppName}}",
            HtmlBody = @"
                <h1>Reset de Senha</h1>
                <p>Olá, {{FirstName}}!</p>
                <p>Você solicitou um reset de senha. Clique no link abaixo:</p>
                <a href='{{ResetUrl}}'>Redefinir Senha</a>
                <p>Este link expira em {{ExpirationTime}}.</p>
                <hr>
                <p>Se você não solicitou este reset, ignore este email.</p>
                <p>Equipe {{AppName}}</p>",
            TextBody = @"
                Reset de Senha
                
                Olá, {{FirstName}}!
                
                Você solicitou um reset de senha. Acesse o link:
                {{ResetUrl}}
                
                Este link expira em {{ExpirationTime}}.
                
                Se você não solicitou este reset, ignore este email.
                
                Equipe {{AppName}}",
            Category = "Auth",
            Priority = 1,
            AvailableVariables = "[\"AppName\", \"FirstName\", \"ResetUrl\", \"ExpirationTime\"]"
        };
    }
}
```

### 4. Criar SystemConfiguration

#### src/IDE.Domain/Entities/SystemConfiguration.cs
```csharp
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using IDE.Domain.Enums;

namespace IDE.Domain.Entities;

/// <summary>
/// Configurações dinâmicas do sistema
/// </summary>
public class SystemConfiguration
{
    /// <summary>
    /// Identificador único da configuração
    /// </summary>
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Chave da configuração (única)
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Valor da configuração
    /// </summary>
    [Required]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Descrição da configuração
    /// </summary>
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Tipo da configuração
    /// </summary>
    public ConfigType Type { get; set; } = ConfigType.String;

    /// <summary>
    /// Categoria da configuração
    /// </summary>
    [MaxLength(50)]
    public string Category { get; set; } = "General";

    /// <summary>
    /// Indica se é uma configuração sensível (senhas, tokens)
    /// </summary>
    public bool IsSensitive { get; set; } = false;

    /// <summary>
    /// Indica se pode ser modificada em runtime
    /// </summary>
    public bool IsReadOnly { get; set; } = false;

    /// <summary>
    /// Valor padrão da configuração
    /// </summary>
    public string DefaultValue { get; set; } = string.Empty;

    /// <summary>
    /// Data da última atualização
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Usuário que fez a última alteração
    /// </summary>
    public string UpdatedBy { get; set; } = "System";

    // Métodos auxiliares

    /// <summary>
    /// Obtém o valor como string
    /// </summary>
    public string GetStringValue() => Value;

    /// <summary>
    /// Obtém o valor como inteiro
    /// </summary>
    public int GetIntValue() => Type == ConfigType.Integer ? int.Parse(Value) : 0;

    /// <summary>
    /// Obtém o valor como booleano
    /// </summary>
    public bool GetBoolValue() => Type == ConfigType.Boolean && bool.Parse(Value);

    /// <summary>
    /// Obtém o valor como objeto JSON
    /// </summary>
    public T? GetJsonValue<T>() where T : class
    {
        if (Type != ConfigType.Json) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(Value);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Define valor string
    /// </summary>
    public void SetValue(string value, string updatedBy = "System")
    {
        if (IsReadOnly) return;
        
        Value = value;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    /// <summary>
    /// Define valor inteiro
    /// </summary>
    public void SetValue(int value, string updatedBy = "System")
    {
        if (IsReadOnly || Type != ConfigType.Integer) return;
        
        SetValue(value.ToString(), updatedBy);
    }

    /// <summary>
    /// Define valor booleano
    /// </summary>
    public void SetValue(bool value, string updatedBy = "System")
    {
        if (IsReadOnly || Type != ConfigType.Boolean) return;
        
        SetValue(value.ToString(), updatedBy);
    }

    /// <summary>
    /// Define valor JSON
    /// </summary>
    public void SetJsonValue<T>(T value, string updatedBy = "System") where T : class
    {
        if (IsReadOnly || Type != ConfigType.Json) return;
        
        var json = JsonSerializer.Serialize(value);
        SetValue(json, updatedBy);
    }

    /// <summary>
    /// Reseta para o valor padrão
    /// </summary>
    public void ResetToDefault(string updatedBy = "System")
    {
        if (IsReadOnly) return;
        
        SetValue(DefaultValue, updatedBy);
    }

    /// <summary>
    /// Cria configurações padrão do sistema
    /// </summary>
    public static List<SystemConfiguration> CreateDefaults()
    {
        return new List<SystemConfiguration>
        {
            new() 
            {
                Key = "App.Name",
                Value = "IDE Colaborativo",
                Description = "Nome da aplicação",
                Type = ConfigType.String,
                Category = "Application",
                DefaultValue = "IDE Colaborativo"
            },
            new()
            {
                Key = "App.Version",
                Value = "1.0.0",
                Description = "Versão da aplicação",
                Type = ConfigType.String,
                Category = "Application",
                IsReadOnly = true,
                DefaultValue = "1.0.0"
            },
            new()
            {
                Key = "Auth.MaxFailedAttempts",
                Value = "5",
                Description = "Número máximo de tentativas de login falhadas",
                Type = ConfigType.Integer,
                Category = "Authentication",
                DefaultValue = "5"
            },
            new()
            {
                Key = "Auth.LockoutDurationMinutes",
                Value = "15",
                Description = "Duração do lockout em minutos",
                Type = ConfigType.Integer,
                Category = "Authentication",
                DefaultValue = "15"
            },
            new()
            {
                Key = "Email.DefaultProvider",
                Value = "SendGrid",
                Description = "Provedor de email padrão",
                Type = ConfigType.String,
                Category = "Email",
                DefaultValue = "SendGrid"
            },
            new()
            {
                Key = "Security.RequireEmailVerification",
                Value = "false",
                Description = "Obrigar verificação de email para login",
                Type = ConfigType.Boolean,
                Category = "Security",
                DefaultValue = "false"
            }
        };
    }
}
```

### 5. Criar PlanLimits

#### src/IDE.Domain/Entities/PlanLimits.cs
```csharp
using System.ComponentModel.DataAnnotations;
using IDE.Domain.Enums;

namespace IDE.Domain.Entities;

/// <summary>
/// Limites e restrições por plano de usuário
/// </summary>
public class PlanLimits
{
    /// <summary>
    /// Identificador único
    /// </summary>
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Plano ao qual os limites se aplicam
    /// </summary>
    public UserPlan Plan { get; set; }

    /// <summary>
    /// Número máximo de workspaces
    /// </summary>
    [Range(0, int.MaxValue)]
    public int MaxWorkspaces { get; set; } = 10;

    /// <summary>
    /// Tamanho máximo de storage por workspace (bytes)
    /// </summary>
    [Range(0, long.MaxValue)]
    public long MaxStoragePerWorkspace { get; set; } = 10 * 1024 * 1024; // 10MB

    /// <summary>
    /// Tamanho máximo de um item individual (bytes)
    /// </summary>
    [Range(0, long.MaxValue)]
    public long MaxItemSize { get; set; } = 5 * 1024 * 1024; // 5MB

    /// <summary>
    /// Número máximo de colaboradores por workspace
    /// </summary>
    [Range(0, int.MaxValue)]
    public int MaxCollaboratorsPerWorkspace { get; set; } = 5;

    /// <summary>
    /// Permite uso de API Keys
    /// </summary>
    public bool CanUseApiKeys { get; set; } = false;

    /// <summary>
    /// Permite exportação de workspaces
    /// </summary>
    public bool CanExportWorkspaces { get; set; } = false;

    /// <summary>
    /// Permite compartilhamento público
    /// </summary>
    public bool CanSharePublicly { get; set; } = false;

    /// <summary>
    /// Número máximo de API Keys
    /// </summary>
    [Range(0, int.MaxValue)]
    public int MaxApiKeys { get; set; } = 0;

    /// <summary>
    /// Rate limit para requests por minuto
    /// </summary>
    [Range(0, int.MaxValue)]
    public int RateLimitPerMinute { get; set; } = 1000;

    /// <summary>
    /// Permite integração com terceiros
    /// </summary>
    public bool CanIntegrateThirdParty { get; set; } = false;

    /// <summary>
    /// Suporte prioritário
    /// </summary>
    public bool HasPrioritySupport { get; set; } = false;

    /// <summary>
    /// Backup automático
    /// </summary>
    public bool HasAutomaticBackup { get; set; } = false;

    /// <summary>
    /// Histórico de versões (dias)
    /// </summary>
    [Range(0, int.MaxValue)]
    public int VersionHistoryDays { get; set; } = 7;

    /// <summary>
    /// Data de criação
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Data de última atualização
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Métodos auxiliares

    /// <summary>
    /// Verifica se o plano permite uma quantidade específica de workspaces
    /// </summary>
    public bool CanCreateWorkspace(int currentCount) => currentCount < MaxWorkspaces;

    /// <summary>
    /// Verifica se o plano permite adicionar colaboradores
    /// </summary>
    public bool CanAddCollaborator(int currentCount) => currentCount < MaxCollaboratorsPerWorkspace;

    /// <summary>
    /// Verifica se um arquivo pode ser upload considerando o limite de tamanho
    /// </summary>
    public bool CanUploadFile(long fileSize) => fileSize <= MaxItemSize;

    /// <summary>
    /// Verifica se o workspace tem espaço para mais arquivos
    /// </summary>
    public bool HasStorageSpace(long currentUsage, long newFileSize) => 
        (currentUsage + newFileSize) <= MaxStoragePerWorkspace;

    /// <summary>
    /// Formata o limite de storage de forma legível
    /// </summary>
    public string GetFormattedStorageLimit()
    {
        if (MaxStoragePerWorkspace >= 1024 * 1024 * 1024) // GB
            return $"{MaxStoragePerWorkspace / (1024 * 1024 * 1024)} GB";
        if (MaxStoragePerWorkspace >= 1024 * 1024) // MB
            return $"{MaxStoragePerWorkspace / (1024 * 1024)} MB";
        return $"{MaxStoragePerWorkspace / 1024} KB";
    }

    /// <summary>
    /// Formata o limite de item de forma legível
    /// </summary>
    public string GetFormattedItemSizeLimit()
    {
        if (MaxItemSize >= 1024 * 1024 * 1024) // GB
            return $"{MaxItemSize / (1024 * 1024 * 1024)} GB";
        if (MaxItemSize >= 1024 * 1024) // MB
            return $"{MaxItemSize / (1024 * 1024)} MB";
        return $"{MaxItemSize / 1024} KB";
    }

    /// <summary>
    /// Cria limites padrão para todos os planos
    /// </summary>
    public static List<PlanLimits> CreateDefaults()
    {
        return new List<PlanLimits>
        {
            // Free Plan
            new()
            {
                Plan = UserPlan.Free,
                MaxWorkspaces = 3,
                MaxStoragePerWorkspace = 10 * 1024 * 1024, // 10MB
                MaxItemSize = 2 * 1024 * 1024, // 2MB
                MaxCollaboratorsPerWorkspace = 2,
                CanUseApiKeys = false,
                CanExportWorkspaces = false,
                CanSharePublicly = false,
                MaxApiKeys = 0,
                RateLimitPerMinute = 500,
                CanIntegrateThirdParty = false,
                HasPrioritySupport = false,
                HasAutomaticBackup = false,
                VersionHistoryDays = 3
            },
            // Premium Plan
            new()
            {
                Plan = UserPlan.Premium,
                MaxWorkspaces = 15,
                MaxStoragePerWorkspace = 100 * 1024 * 1024, // 100MB
                MaxItemSize = 10 * 1024 * 1024, // 10MB
                MaxCollaboratorsPerWorkspace = 10,
                CanUseApiKeys = true,
                CanExportWorkspaces = true,
                CanSharePublicly = true,
                MaxApiKeys = 5,
                RateLimitPerMinute = 2000,
                CanIntegrateThirdParty = true,
                HasPrioritySupport = false,
                HasAutomaticBackup = true,
                VersionHistoryDays = 30
            },
            // Enterprise Plan
            new()
            {
                Plan = UserPlan.Enterprise,
                MaxWorkspaces = int.MaxValue, // Unlimited
                MaxStoragePerWorkspace = 1024 * 1024 * 1024, // 1GB
                MaxItemSize = 50 * 1024 * 1024, // 50MB
                MaxCollaboratorsPerWorkspace = int.MaxValue, // Unlimited
                CanUseApiKeys = true,
                CanExportWorkspaces = true,
                CanSharePublicly = true,
                MaxApiKeys = 20,
                RateLimitPerMinute = 10000,
                CanIntegrateThirdParty = true,
                HasPrioritySupport = true,
                HasAutomaticBackup = true,
                VersionHistoryDays = 365
            }
        };
    }
}
```

### 6. Criar SecurityConfiguration

#### src/IDE.Domain/Entities/SecurityConfiguration.cs
```csharp
using System.ComponentModel.DataAnnotations;

namespace IDE.Domain.Entities;

/// <summary>
/// Configurações de segurança do sistema
/// </summary>
public class SecurityConfiguration
{
    /// <summary>
    /// Identificador único
    /// </summary>
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Número máximo de tentativas de login falhadas
    /// </summary>
    [Range(1, 20)]
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>
    /// Duração do lockout em minutos
    /// </summary>
    [Range(1, 1440)] // Max 24 hours
    public int LockoutDurationMinutes { get; set; } = 15;

    /// <summary>
    /// Incrementar duração do lockout a cada tentativa
    /// </summary>
    public bool LockoutIncrement { get; set; } = true;

    /// <summary>
    /// Resetar tentativas falhadas após X horas de inatividade
    /// </summary>
    [Range(1, 168)] // Max 7 days
    public int ResetFailedAttemptsAfterHours { get; set; } = 24;

    /// <summary>
    /// Expiração do token de reset de senha (minutos)
    /// </summary>
    [Range(5, 1440)] // 5 minutes to 24 hours
    public int PasswordResetTokenExpirationMinutes { get; set; } = 30;

    /// <summary>
    /// Expiração do token de verificação de email (horas)
    /// </summary>
    [Range(1, 168)] // 1 hour to 7 days
    public int EmailVerificationTokenExpirationHours { get; set; } = 24;

    /// <summary>
    /// Cooldown entre reenvios de email (minutos)
    /// </summary>
    [Range(1, 60)]
    public int ResendCooldownMinutes { get; set; } = 5;

    /// <summary>
    /// Máximo de tentativas de reenvio por hora
    /// </summary>
    [Range(1, 10)]
    public int MaxResendAttempts { get; set; } = 3;

    /// <summary>
    /// Exigir verificação de email para fazer login
    /// </summary>
    public bool RequireVerificationForLogin { get; set; } = false;

    /// <summary>
    /// Força logout de todos os dispositivos ao alterar senha
    /// </summary>
    public bool ForceLogoutOnPasswordChange { get; set; } = true;

    /// <summary>
    /// Notificar login de novo dispositivo por email
    /// </summary>
    public bool NotifyNewDeviceLogin { get; set; } = true;

    /// <summary>
    /// Tempo de vida do JWT em minutos
    /// </summary>
    [Range(5, 1440)] // 5 minutes to 24 hours
    public int JwtExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// Tempo de vida do Refresh Token em dias
    /// </summary>
    [Range(1, 365)] // 1 day to 1 year
    public int RefreshTokenExpirationDays { get; set; } = 7;

    /// <summary>
    /// Permitir múltiplos refresh tokens por usuário
    /// </summary>
    public bool AllowMultipleRefreshTokens { get; set; } = true;

    /// <summary>
    /// Máximo de refresh tokens ativos por usuário
    /// </summary>
    [Range(1, 20)]
    public int MaxRefreshTokensPerUser { get; set; } = 5;

    /// <summary>
    /// Habilitar auditoria de login
    /// </summary>
    public bool EnableLoginAudit { get; set; } = true;

    /// <summary>
    /// Manter logs de auditoria por X dias
    /// </summary>
    [Range(30, 2555)] // 30 days to 7 years
    public int AuditLogRetentionDays { get; set; } = 90;

    /// <summary>
    /// Data de criação
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Data de última atualização
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Usuário que fez a última alteração
    /// </summary>
    [MaxLength(100)]
    public string UpdatedBy { get; set; } = "System";

    // Métodos auxiliares

    /// <summary>
    /// Obtém duração do lockout com incremento
    /// </summary>
    public TimeSpan GetLockoutDuration(int attemptCount)
    {
        if (!LockoutIncrement)
            return TimeSpan.FromMinutes(LockoutDurationMinutes);

        // Incremento exponencial: 15, 30, 60, 120, etc.
        var multiplier = Math.Pow(2, Math.Min(attemptCount - MaxFailedAttempts, 4));
        var minutes = (int)(LockoutDurationMinutes * multiplier);
        
        return TimeSpan.FromMinutes(Math.Min(minutes, 1440)); // Max 24 hours
    }

    /// <summary>
    /// Verifica se deve resetar tentativas falhadas
    /// </summary>
    public bool ShouldResetFailedAttempts(DateTime lastFailedAttempt)
    {
        return DateTime.UtcNow - lastFailedAttempt > TimeSpan.FromHours(ResetFailedAttemptsAfterHours);
    }

    /// <summary>
    /// Verifica se pode reenviar email
    /// </summary>
    public bool CanResendEmail(DateTime? lastSentAt, int todayCount)
    {
        if (todayCount >= MaxResendAttempts) return false;
        
        if (lastSentAt.HasValue)
        {
            var cooldownExpired = DateTime.UtcNow - lastSentAt.Value > TimeSpan.FromMinutes(ResendCooldownMinutes);
            return cooldownExpired;
        }

        return true;
    }

    /// <summary>
    /// Cria configuração padrão
    /// </summary>
    public static SecurityConfiguration CreateDefault()
    {
        return new SecurityConfiguration(); // Usa valores padrão das propriedades
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

- [ ] **ApiKey** completamente implementada com segurança
- [ ] **UserLoginHistory** para auditoria completa
- [ ] **EmailTemplate** com sistema de variáveis
- [ ] **SystemConfiguration** para configurações dinâmicas
- [ ] **PlanLimits** com limites por plano
- [ ] **SecurityConfiguration** com políticas de segurança
- [ ] **Compilação bem-sucedida** sem erros ou warnings
- [ ] **Métodos auxiliares** funcionando corretamente

## 📝 Arquivos Criados

Esta parte criará/atualizará aproximadamente **6 arquivos**:
- ApiKey.cs (atualizado)
- UserLoginHistory.cs (atualizado)
- EmailTemplate.cs (novo)
- SystemConfiguration.cs (novo)
- PlanLimits.cs (novo)
- SecurityConfiguration.cs (novo)

## 🔄 Próximos Passos

Após concluir esta parte, você estará pronto para:
- **Parte 4**: Entity Framework e Configuração
- Configurar DbContext com todas as entidades
- Criar migrations e seed data

## 🚨 Troubleshooting Comum

**Erros de compilação**: Verifique todos os namespaces e using statements  
**Problemas de relacionamento**: Serão resolvidos na configuração do EF  
**Validações**: Os Data Annotations estão prontos para o EF  

---
**⏱️ Tempo estimado**: 15-20 minutos  
**🎯 Próxima parte**: 04-entity-framework-configuracao.md  
**📋 Dependências**: Partes 1 e 2 concluídas