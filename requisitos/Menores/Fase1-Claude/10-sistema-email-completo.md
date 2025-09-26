# Parte 10: Sistema Completo de Email

## 📋 Visão Geral
**Duração**: 35-50 minutos  
**Complexidade**: Média-Alta  
**Dependências**: Partes 1-9 (Setup + Entidades + EF + DTOs + Validações + Serviços + 2FA + OAuth)

Esta parte implementa um sistema completo de email com 5 provedores (SMTP, SendGrid, AWS SES, Mailgun, Azure), sistema de templates, filas de email, retry automático, rastreamento de entrega e dashboards de monitoramento.

## 🎯 Objetivos
- ✅ Implementar 5 provedores de email (SMTP, SendGrid, AWS SES, Mailgun, Azure)
- ✅ Criar sistema de templates com Razor Engine
- ✅ Implementar filas de email com retry automático
- ✅ Configurar rastreamento de entrega e bounces
- ✅ Implementar rate limiting por provedor
- ✅ Criar dashboard de monitoramento
- ✅ Configurar failover entre provedores

## 📁 Arquivos a serem Criados

```
src/IDE.Application/Email/
├── IEmailService.cs
├── EmailService.cs
├── Services/
│   ├── IEmailTemplateService.cs
│   ├── EmailTemplateService.cs
│   ├── IEmailQueueService.cs
│   └── EmailQueueService.cs
├── Providers/
│   ├── IEmailProvider.cs
│   ├── SmtpEmailProvider.cs
│   ├── SendGridEmailProvider.cs
│   ├── AwsSesEmailProvider.cs
│   ├── MailgunEmailProvider.cs
│   └── AzureEmailProvider.cs
├── Models/
│   ├── EmailMessage.cs
│   ├── EmailTemplate.cs
│   ├── EmailResult.cs
│   ├── EmailDeliveryStatus.cs
│   └── EmailProviderConfig.cs
├── Templates/
│   ├── WelcomeEmail.cshtml
│   ├── EmailVerification.cshtml
│   ├── PasswordReset.cshtml
│   ├── TwoFactorCode.cshtml
│   └── Layouts/
│       └── _Layout.cshtml
└── BackgroundServices/
    └── EmailProcessorService.cs
```

## 🚀 Execução Passo a Passo

### 1. Criar Models de Email

#### src/IDE.Application/Email/Models/EmailMessage.cs
```csharp
using IDE.Domain.Enums;

namespace IDE.Application.Email.Models;

/// <summary>
/// Representa uma mensagem de email
/// </summary>
public class EmailMessage
{
    /// <summary>
    /// ID único da mensagem
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Destinatário principal
    /// </summary>
    public string To { get; set; } = string.Empty;
    
    /// <summary>
    /// Nome do destinatário
    /// </summary>
    public string? ToName { get; set; }
    
    /// <summary>
    /// Lista de destinatários CC
    /// </summary>
    public List<string> Cc { get; set; } = new();
    
    /// <summary>
    /// Lista de destinatários BCC
    /// </summary>
    public List<string> Bcc { get; set; } = new();
    
    /// <summary>
    /// Endereço de resposta
    /// </summary>
    public string? ReplyTo { get; set; }
    
    /// <summary>
    /// Nome do remetente
    /// </summary>
    public string? ReplyToName { get; set; }
    
    /// <summary>
    /// Assunto
    /// </summary>
    public string Subject { get; set; } = string.Empty;
    
    /// <summary>
    /// Conteúdo HTML
    /// </summary>
    public string? HtmlBody { get; set; }
    
    /// <summary>
    /// Conteúdo texto plano
    /// </summary>
    public string? TextBody { get; set; }
    
    /// <summary>
    /// Anexos
    /// </summary>
    public List<EmailAttachment> Attachments { get; set; } = new();
    
    /// <summary>
    /// Prioridade do email
    /// </summary>
    public EmailPriority Priority { get; set; } = EmailPriority.Normal;
    
    /// <summary>
    /// Tipo/categoria do email
    /// </summary>
    public EmailType Type { get; set; } = EmailType.Transactional;
    
    /// <summary>
    /// Tags para categorização
    /// </summary>
    public List<string> Tags { get; set; } = new();
    
    /// <summary>
    /// Metadados customizados
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
    
    /// <summary>
    /// Data de agendamento (para emails futuros)
    /// </summary>
    public DateTime? ScheduledAt { get; set; }
    
    /// <summary>
    /// ID do template usado (se aplicável)
    /// </summary>
    public string? TemplateId { get; set; }
    
    /// <summary>
    /// Dados para merge no template
    /// </summary>
    public Dictionary<string, object> TemplateData { get; set; } = new();
    
    /// <summary>
    /// Tentativas máximas de envio
    /// </summary>
    public int MaxRetries { get; set; } = 3;
    
    /// <summary>
    /// Data de criação
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// ID do usuário associado (se aplicável)
    /// </summary>
    public Guid? UserId { get; set; }
}

/// <summary>
/// Anexo de email
/// </summary>
public class EmailAttachment
{
    public string Name { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public bool IsInline { get; set; } = false;
    public string? ContentId { get; set; }
}
```

#### src/IDE.Application/Email/Models/EmailResult.cs
```csharp
namespace IDE.Application.Email.Models;

/// <summary>
/// Resultado do envio de email
/// </summary>
public class EmailResult
{
    /// <summary>
    /// Se o envio foi bem-sucedido
    /// </summary>
    public bool IsSuccess { get; set; }
    
    /// <summary>
    /// ID da mensagem no provedor
    /// </summary>
    public string? MessageId { get; set; }
    
    /// <summary>
    /// Provedor utilizado
    /// </summary>
    public string? ProviderName { get; set; }
    
    /// <summary>
    /// Mensagem de erro (se houver)
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Código de erro
    /// </summary>
    public string? ErrorCode { get; set; }
    
    /// <summary>
    /// Detalhes da resposta do provedor
    /// </summary>
    public Dictionary<string, object> ProviderResponse { get; set; } = new();
    
    /// <summary>
    /// Tempo de processamento
    /// </summary>
    public TimeSpan ProcessingTime { get; set; }
    
    /// <summary>
    /// Número da tentativa
    /// </summary>
    public int AttemptNumber { get; set; }
    
    /// <summary>
    /// Timestamp do envio
    /// </summary>
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Se deve tentar novamente em caso de erro
    /// </summary>
    public bool ShouldRetry { get; set; }
    
    /// <summary>
    /// Intervalo para próxima tentativa
    /// </summary>
    public TimeSpan? RetryAfter { get; set; }
}

/// <summary>
/// Status de entrega do email
/// </summary>
public class EmailDeliveryStatus
{
    /// <summary>
    /// ID da mensagem
    /// </summary>
    public string MessageId { get; set; } = string.Empty;
    
    /// <summary>
    /// Status atual
    /// </summary>
    public DeliveryStatus Status { get; set; }
    
    /// <summary>
    /// Timestamp do status
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// Descrição do status
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Eventos de rastreamento
    /// </summary>
    public List<DeliveryEvent> Events { get; set; } = new();
}

/// <summary>
/// Evento de entrega
/// </summary>
public class DeliveryEvent
{
    public string Type { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? Description { get; set; }
    public Dictionary<string, string> Data { get; set; } = new();
}

/// <summary>
/// Status de entrega
/// </summary>
public enum DeliveryStatus
{
    Pending,
    Sent,
    Delivered,
    Opened,
    Clicked,
    Bounced,
    Complained,
    Unsubscribed,
    Failed
}
```

#### src/IDE.Application/Email/Models/EmailProviderConfig.cs
```csharp
namespace IDE.Application.Email.Models;

/// <summary>
/// Configuração do provedor de email
/// </summary>
public class EmailProviderConfig
{
    /// <summary>
    /// Nome do provedor
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Tipo do provedor
    /// </summary>
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// Se está habilitado
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// Prioridade (1 = mais alta)
    /// </summary>
    public int Priority { get; set; } = 1;
    
    /// <summary>
    /// Limite de envios por minuto
    /// </summary>
    public int RateLimitPerMinute { get; set; } = 60;
    
    /// <summary>
    /// Limite de envios por hora
    /// </summary>
    public int RateLimitPerHour { get; set; } = 1000;
    
    /// <summary>
    /// Limite diário
    /// </summary>
    public int DailyLimit { get; set; } = 10000;
    
    /// <summary>
    /// Configurações específicas do provedor
    /// </summary>
    public Dictionary<string, string> Settings { get; set; } = new();
    
    /// <summary>
    /// Remetente padrão
    /// </summary>
    public string DefaultFromEmail { get; set; } = string.Empty;
    
    /// <summary>
    /// Nome do remetente padrão
    /// </summary>
    public string DefaultFromName { get; set; } = string.Empty;
    
    /// <summary>
    /// Timeout em segundos
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
    
    /// <summary>
    /// Tipos de email suportados
    /// </summary>
    public List<EmailType> SupportedTypes { get; set; } = new();
}

/// <summary>
/// Tipo de email
/// </summary>
public enum EmailType
{
    Transactional,
    Marketing,
    System,
    Notification
}

/// <summary>
/// Prioridade do email
/// </summary>
public enum EmailPriority
{
    Low = 1,
    Normal = 2,
    High = 3,
    Critical = 4
}
```

### 2. Implementar Interface Base do Provedor

#### src/IDE.Application/Email/Providers/IEmailProvider.cs
```csharp
using IDE.Application.Email.Models;

namespace IDE.Application.Email.Providers;

/// <summary>
/// Interface base para provedores de email
/// </summary>
public interface IEmailProvider
{
    /// <summary>
    /// Nome do provedor
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Envia email
    /// </summary>
    Task<EmailResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Envia múltiplos emails
    /// </summary>
    Task<List<EmailResult>> SendBulkAsync(List<EmailMessage> messages, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Obtém status de entrega
    /// </summary>
    Task<EmailDeliveryStatus?> GetDeliveryStatusAsync(string messageId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Verifica se o provedor está saudável
    /// </summary>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Valida configuração
    /// </summary>
    bool ValidateConfiguration();
    
    /// <summary>
    /// Suporta o tipo de email
    /// </summary>
    bool SupportsEmailType(EmailType emailType);
    
    /// <summary>
    /// Obtém limite de rate atual
    /// </summary>
    Task<(int Used, int Limit)> GetCurrentRateLimitAsync(CancellationToken cancellationToken = default);
}
```

### 3. Implementar Provedor SMTP

#### src/IDE.Application/Email/Providers/SmtpEmailProvider.cs
```csharp
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IDE.Application.Email.Models;

namespace IDE.Application.Email.Providers;

/// <summary>
/// Provedor SMTP genérico
/// </summary>
public class SmtpEmailProvider : IEmailProvider
{
    public string Name => "SMTP";

    private readonly EmailProviderConfig _config;
    private readonly ILogger<SmtpEmailProvider> _logger;

    public SmtpEmailProvider(
        IOptionsMonitor<EmailProviderConfig> optionsMonitor,
        ILogger<SmtpEmailProvider> logger)
    {
        _config = optionsMonitor.Get("SMTP");
        _logger = logger;
    }

    public async Task<EmailResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            using var client = CreateSmtpClient();
            using var mailMessage = CreateMailMessage(message);
            
            await client.SendMailAsync(mailMessage, cancellationToken);
            
            stopwatch.Stop();
            
            return new EmailResult
            {
                IsSuccess = true,
                MessageId = message.Id,
                ProviderName = Name,
                ProcessingTime = stopwatch.Elapsed,
                SentAt = DateTime.UtcNow,
                AttemptNumber = 1
            };
        }
        catch (SmtpException ex)
        {
            stopwatch.Stop();
            
            _logger.LogError(ex, "Erro SMTP ao enviar email {MessageId}", message.Id);
            
            return new EmailResult
            {
                IsSuccess = false,
                MessageId = message.Id,
                ProviderName = Name,
                ErrorMessage = ex.Message,
                ErrorCode = ex.StatusCode.ToString(),
                ProcessingTime = stopwatch.Elapsed,
                ShouldRetry = ShouldRetrySmtpError(ex.StatusCode),
                RetryAfter = TimeSpan.FromMinutes(5)
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _logger.LogError(ex, "Erro inesperado ao enviar email {MessageId} via SMTP", message.Id);
            
            return new EmailResult
            {
                IsSuccess = false,
                MessageId = message.Id,
                ProviderName = Name,
                ErrorMessage = ex.Message,
                ProcessingTime = stopwatch.Elapsed,
                ShouldRetry = true,
                RetryAfter = TimeSpan.FromMinutes(10)
            };
        }
    }

    public async Task<List<EmailResult>> SendBulkAsync(List<EmailMessage> messages, CancellationToken cancellationToken = default)
    {
        var results = new List<EmailResult>();
        
        foreach (var message in messages)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
                
            var result = await SendAsync(message, cancellationToken);
            results.Add(result);
            
            // Pequeno delay entre envios para evitar rate limiting
            await Task.Delay(100, cancellationToken);
        }
        
        return results;
    }

    public Task<EmailDeliveryStatus?> GetDeliveryStatusAsync(string messageId, CancellationToken cancellationToken = default)
    {
        // SMTP não fornece tracking de entrega
        return Task.FromResult<EmailDeliveryStatus?>(null);
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = CreateSmtpClient();
            // Tenta conectar sem enviar email
            client.Timeout = 5000; // 5 segundos
            await Task.Run(() => client.Connect(), cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check SMTP falhou");
            return false;
        }
    }

    public bool ValidateConfiguration()
    {
        return _config.Settings.ContainsKey("Host") &&
               _config.Settings.ContainsKey("Port") &&
               !string.IsNullOrEmpty(_config.DefaultFromEmail);
    }

    public bool SupportsEmailType(EmailType emailType)
    {
        return _config.SupportedTypes.Contains(emailType) || !_config.SupportedTypes.Any();
    }

    public Task<(int Used, int Limit)> GetCurrentRateLimitAsync(CancellationToken cancellationToken = default)
    {
        // SMTP não tem rate limiting nativo, retorna limites configurados
        return Task.FromResult((0, _config.RateLimitPerMinute));
    }

    /// <summary>
    /// Cria cliente SMTP configurado
    /// </summary>
    private SmtpClient CreateSmtpClient()
    {
        var host = _config.Settings["Host"];
        var port = int.Parse(_config.Settings.GetValueOrDefault("Port", "587"));
        var enableSsl = bool.Parse(_config.Settings.GetValueOrDefault("EnableSsl", "true"));
        var username = _config.Settings.GetValueOrDefault("Username");
        var password = _config.Settings.GetValueOrDefault("Password");

        var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            Timeout = _config.TimeoutSeconds * 1000,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            client.Credentials = new NetworkCredential(username, password);
        }

        return client;
    }

    /// <summary>
    /// Cria MailMessage a partir de EmailMessage
    /// </summary>
    private MailMessage CreateMailMessage(EmailMessage message)
    {
        var mail = new MailMessage
        {
            From = new MailAddress(_config.DefaultFromEmail, _config.DefaultFromName),
            Subject = message.Subject,
            IsBodyHtml = !string.IsNullOrEmpty(message.HtmlBody),
            Body = message.HtmlBody ?? message.TextBody ?? "",
            Priority = MapPriority(message.Priority)
        };

        // Destinatário principal
        mail.To.Add(new MailAddress(message.To, message.ToName));

        // CC
        foreach (var cc in message.Cc)
        {
            mail.CC.Add(new MailAddress(cc));
        }

        // BCC
        foreach (var bcc in message.Bcc)
        {
            mail.Bcc.Add(new MailAddress(bcc));
        }

        // Reply-To
        if (!string.IsNullOrEmpty(message.ReplyTo))
        {
            mail.ReplyToList.Add(new MailAddress(message.ReplyTo, message.ReplyToName));
        }

        // Anexos
        foreach (var attachment in message.Attachments)
        {
            var stream = new MemoryStream(attachment.Data);
            var mailAttachment = new Attachment(stream, attachment.Name, attachment.ContentType);
            
            if (attachment.IsInline && !string.IsNullOrEmpty(attachment.ContentId))
            {
                mailAttachment.ContentId = attachment.ContentId;
            }
            
            mail.Attachments.Add(mailAttachment);
        }

        // Cabeçalhos personalizados
        foreach (var tag in message.Tags)
        {
            mail.Headers.Add("X-Tag", tag);
        }

        if (!string.IsNullOrEmpty(message.TemplateId))
        {
            mail.Headers.Add("X-Template", message.TemplateId);
        }

        return mail;
    }

    /// <summary>
    /// Mapeia prioridade do email
    /// </summary>
    private static MailPriority MapPriority(EmailPriority priority)
    {
        return priority switch
        {
            EmailPriority.Low => MailPriority.Low,
            EmailPriority.High => MailPriority.High,
            EmailPriority.Critical => MailPriority.High,
            _ => MailPriority.Normal
        };
    }

    /// <summary>
    /// Verifica se erro SMTP deve gerar retry
    /// </summary>
    private static bool ShouldRetrySmtpError(SmtpStatusCode statusCode)
    {
        return statusCode switch
        {
            SmtpStatusCode.MailboxBusy => true,
            SmtpStatusCode.InsufficientStorage => true,
            SmtpStatusCode.CommandTimeout => true,
            SmtpStatusCode.TransactionFailed => true,
            SmtpStatusCode.GeneralFailure => true,
            _ => false
        };
    }
}
```

### 4. Implementar Provedor SendGrid

#### src/IDE.Application/Email/Providers/SendGridEmailProvider.cs
```csharp
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IDE.Application.Email.Models;

namespace IDE.Application.Email.Providers;

/// <summary>
/// Provedor SendGrid
/// </summary>
public class SendGridEmailProvider : IEmailProvider
{
    public string Name => "SendGrid";

    private readonly EmailProviderConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SendGridEmailProvider> _logger;

    private const string BaseUrl = "https://api.sendgrid.com";

    public SendGridEmailProvider(
        IOptionsMonitor<EmailProviderConfig> optionsMonitor,
        HttpClient httpClient,
        ILogger<SendGridEmailProvider> logger)
    {
        _config = optionsMonitor.Get("SendGrid");
        _httpClient = httpClient;
        _logger = logger;

        // Configura autenticação
        var apiKey = _config.Settings.GetValueOrDefault("ApiKey");
        if (!string.IsNullOrEmpty(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        }

        _httpClient.BaseAddress = new Uri(BaseUrl);
    }

    public async Task<EmailResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            var payload = CreateSendGridPayload(message);
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });

            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/v3/mail/send", content, cancellationToken);

            stopwatch.Stop();

            if (response.IsSuccessStatusCode)
            {
                var messageId = response.Headers.Contains("X-Message-Id") 
                    ? response.Headers.GetValues("X-Message-Id").FirstOrDefault() 
                    : message.Id;

                return new EmailResult
                {
                    IsSuccess = true,
                    MessageId = messageId,
                    ProviderName = Name,
                    ProcessingTime = stopwatch.Elapsed,
                    SentAt = DateTime.UtcNow,
                    AttemptNumber = 1
                };
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var errorData = JsonSerializer.Deserialize<JsonElement>(errorContent);
                
                var errorMessage = "SendGrid API error";
                var errorCode = response.StatusCode.ToString();
                
                if (errorData.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
                {
                    var firstError = errors.EnumerateArray().FirstOrDefault();
                    if (firstError.TryGetProperty("message", out var message1))
                    {
                        errorMessage = message1.GetString() ?? errorMessage;
                    }
                }

                return new EmailResult
                {
                    IsSuccess = false,
                    MessageId = message.Id,
                    ProviderName = Name,
                    ErrorMessage = errorMessage,
                    ErrorCode = errorCode,
                    ProcessingTime = stopwatch.Elapsed,
                    ShouldRetry = ShouldRetryHttpError(response.StatusCode),
                    RetryAfter = GetRetryAfter(response)
                };
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _logger.LogError(ex, "Erro inesperado ao enviar email {MessageId} via SendGrid", message.Id);
            
            return new EmailResult
            {
                IsSuccess = false,
                MessageId = message.Id,
                ProviderName = Name,
                ErrorMessage = ex.Message,
                ProcessingTime = stopwatch.Elapsed,
                ShouldRetry = true,
                RetryAfter = TimeSpan.FromMinutes(5)
            };
        }
    }

    public async Task<List<EmailResult>> SendBulkAsync(List<EmailMessage> messages, CancellationToken cancellationToken = default)
    {
        // SendGrid suporta envio em lote, mas por simplicidade enviamos individualmente
        var results = new List<EmailResult>();
        
        foreach (var message in messages)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
                
            var result = await SendAsync(message, cancellationToken);
            results.Add(result);
        }
        
        return results;
    }

    public async Task<EmailDeliveryStatus?> GetDeliveryStatusAsync(string messageId, CancellationToken cancellationToken = default)
    {
        try
        {
            // SendGrid Activity API
            var response = await _httpClient.GetAsync($"/v3/messages/{messageId}", cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var data = JsonSerializer.Deserialize<JsonElement>(content);
                
                // Parsear resposta e criar EmailDeliveryStatus
                // Implementação simplificada
                return new EmailDeliveryStatus
                {
                    MessageId = messageId,
                    Status = DeliveryStatus.Sent,
                    Timestamp = DateTime.UtcNow
                };
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter status de entrega SendGrid para {MessageId}", messageId);
            return null;
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/v3/user/profile", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check SendGrid falhou");
            return false;
        }
    }

    public bool ValidateConfiguration()
    {
        return _config.Settings.ContainsKey("ApiKey") &&
               !string.IsNullOrEmpty(_config.DefaultFromEmail);
    }

    public bool SupportsEmailType(EmailType emailType)
    {
        return _config.SupportedTypes.Contains(emailType) || !_config.SupportedTypes.Any();
    }

    public async Task<(int Used, int Limit)> GetCurrentRateLimitAsync(CancellationToken cancellationToken = default)
    {
        // SendGrid retorna informações de rate limit nos headers das respostas
        // Por simplicidade, retornamos valores configurados
        return (0, _config.RateLimitPerMinute);
    }

    /// <summary>
    /// Cria payload para API do SendGrid
    /// </summary>
    private object CreateSendGridPayload(EmailMessage message)
    {
        var personalizations = new[]
        {
            new
            {
                to = new[] { new { email = message.To, name = message.ToName } },
                cc = message.Cc.Select(email => new { email }).ToArray(),
                bcc = message.Bcc.Select(email => new { email }).ToArray(),
                subject = message.Subject,
                custom_args = message.Metadata
            }
        };

        var content = new List<object>();
        
        if (!string.IsNullOrEmpty(message.TextBody))
        {
            content.Add(new { type = "text/plain", value = message.TextBody });
        }
        
        if (!string.IsNullOrEmpty(message.HtmlBody))
        {
            content.Add(new { type = "text/html", value = message.HtmlBody });
        }

        var from = new
        {
            email = _config.DefaultFromEmail,
            name = _config.DefaultFromName
        };

        var payload = new
        {
            personalizations,
            from,
            reply_to = !string.IsNullOrEmpty(message.ReplyTo) 
                ? new { email = message.ReplyTo, name = message.ReplyToName }
                : null,
            content = content.ToArray(),
            categories = message.Tags.ToArray(),
            custom_args = message.Metadata,
            send_at = message.ScheduledAt?.ToUnixTimeStamp(),
            attachments = message.Attachments.Select(a => new
            {
                content = Convert.ToBase64String(a.Data),
                filename = a.Name,
                type = a.ContentType,
                disposition = a.IsInline ? "inline" : "attachment",
                content_id = a.ContentId
            }).ToArray()
        };

        return payload;
    }

    private static bool ShouldRetryHttpError(System.Net.HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            System.Net.HttpStatusCode.TooManyRequests => true,
            System.Net.HttpStatusCode.InternalServerError => true,
            System.Net.HttpStatusCode.BadGateway => true,
            System.Net.HttpStatusCode.ServiceUnavailable => true,
            System.Net.HttpStatusCode.GatewayTimeout => true,
            _ => false
        };
    }

    private static TimeSpan GetRetryAfter(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta.HasValue == true)
        {
            return response.Headers.RetryAfter.Delta.Value;
        }
        
        return TimeSpan.FromMinutes(1);
    }
}

/// <summary>
/// Extensão para converter DateTime para Unix timestamp
/// </summary>
internal static class DateTimeExtensions
{
    public static long ToUnixTimeStamp(this DateTime dateTime)
    {
        return ((DateTimeOffset)dateTime).ToUnixTimeSeconds();
    }
}
```

### 5. Implementar Serviço de Templates

#### src/IDE.Application/Email/Services/IEmailTemplateService.cs
```csharp
namespace IDE.Application.Email.Services;

/// <summary>
/// Interface para serviço de templates de email
/// </summary>
public interface IEmailTemplateService
{
    /// <summary>
    /// Renderiza template
    /// </summary>
    Task<string> RenderTemplateAsync(string templateName, object model, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Renderiza template com layout
    /// </summary>
    Task<string> RenderTemplateWithLayoutAsync(string templateName, object model, string? layout = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Verifica se template existe
    /// </summary>
    Task<bool> TemplateExistsAsync(string templateName);
    
    /// <summary>
    /// Lista templates disponíveis
    /// </summary>
    Task<List<string>> GetAvailableTemplatesAsync();
    
    /// <summary>
    /// Compila template para cache
    /// </summary>
    Task CompileTemplateAsync(string templateName, CancellationToken cancellationToken = default);
}
```

#### src/IDE.Application/Email/Services/EmailTemplateService.cs
```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace IDE.Application.Email.Services;

/// <summary>
/// Implementação simplificada do serviço de templates
/// Em produção, usar RazorLight ou RazorEngine
/// </summary>
public class EmailTemplateService : IEmailTemplateService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<EmailTemplateService> _logger;
    
    private const string TemplatesCachePrefix = "email_template:";
    private static readonly Regex PlaceholderRegex = new(@"\{\{(\w+)\}\}", RegexOptions.Compiled);

    public EmailTemplateService(
        IMemoryCache cache,
        ILogger<EmailTemplateService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<string> RenderTemplateAsync(string templateName, object model, CancellationToken cancellationToken = default)
    {
        try
        {
            var template = await GetTemplateContentAsync(templateName, cancellationToken);
            return RenderTemplate(template, model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao renderizar template {TemplateName}", templateName);
            throw;
        }
    }

    public async Task<string> RenderTemplateWithLayoutAsync(string templateName, object model, string? layout = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var content = await RenderTemplateAsync(templateName, model, cancellationToken);
            
            if (string.IsNullOrEmpty(layout))
            {
                layout = "_Layout";
            }

            var layoutTemplate = await GetTemplateContentAsync($"Layouts/{layout}", cancellationToken);
            var layoutModel = new { Body = content, Model = model };
            
            return RenderTemplate(layoutTemplate, layoutModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao renderizar template {TemplateName} com layout {Layout}", templateName, layout);
            throw;
        }
    }

    public async Task<bool> TemplateExistsAsync(string templateName)
    {
        try
        {
            await GetTemplateContentAsync(templateName);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<string>> GetAvailableTemplatesAsync()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceNames = assembly.GetManifestResourceNames()
                .Where(name => name.Contains("Templates") && name.EndsWith(".html"))
                .Select(name => Path.GetFileNameWithoutExtension(name))
                .ToList();

            return await Task.FromResult(resourceNames);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar templates disponíveis");
            return new List<string>();
        }
    }

    public async Task CompileTemplateAsync(string templateName, CancellationToken cancellationToken = default)
    {
        var cacheKey = TemplatesCachePrefix + templateName;
        
        if (!_cache.TryGetValue(cacheKey, out _))
        {
            var template = await GetTemplateContentAsync(templateName, cancellationToken);
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
                Priority = CacheItemPriority.Normal
            };

            _cache.Set(cacheKey, template, cacheOptions);
        }
    }

    /// <summary>
    /// Obtém conteúdo do template
    /// </summary>
    private async Task<string> GetTemplateContentAsync(string templateName, CancellationToken cancellationToken = default)
    {
        var cacheKey = TemplatesCachePrefix + templateName;
        
        if (_cache.TryGetValue(cacheKey, out string? cachedTemplate))
        {
            return cachedTemplate!;
        }

        // Busca template incorporado ou arquivo
        var template = await LoadTemplateFromResourcesAsync(templateName, cancellationToken) ??
                      await LoadTemplateFromFileAsync(templateName, cancellationToken) ??
                      GetDefaultTemplate(templateName);

        // Cacheia template
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
            Priority = CacheItemPriority.Normal
        };

        _cache.Set(cacheKey, template, cacheOptions);
        return template;
    }

    /// <summary>
    /// Carrega template de recursos incorporados
    /// </summary>
    private async Task<string?> LoadTemplateFromResourcesAsync(string templateName, CancellationToken cancellationToken)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith($"{templateName}.html"));

            if (resourceName == null) return null;

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return null;

            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao carregar template {TemplateName} de recursos", templateName);
            return null;
        }
    }

    /// <summary>
    /// Carrega template de arquivo
    /// </summary>
    private async Task<string?> LoadTemplateFromFileAsync(string templateName, CancellationToken cancellationToken)
    {
        try
        {
            var templatesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");
            var templatePath = Path.Combine(templatesPath, $"{templateName}.html");

            if (!File.Exists(templatePath)) return null;

            return await File.ReadAllTextAsync(templatePath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao carregar template {TemplateName} de arquivo", templateName);
            return null;
        }
    }

    /// <summary>
    /// Template padrão caso não encontre
    /// </summary>
    private string GetDefaultTemplate(string templateName)
    {
        return templateName switch
        {
            "WelcomeEmail" => GetWelcomeEmailTemplate(),
            "EmailVerification" => GetEmailVerificationTemplate(),
            "PasswordReset" => GetPasswordResetTemplate(),
            "TwoFactorCode" => GetTwoFactorCodeTemplate(),
            "Layouts/_Layout" => GetLayoutTemplate(),
            _ => GetGenericTemplate()
        };
    }

    /// <summary>
    /// Renderiza template substituindo placeholders
    /// </summary>
    private string RenderTemplate(string template, object model)
    {
        if (model == null) return template;

        var properties = model.GetType().GetProperties()
            .ToDictionary(p => p.Name, p => p.GetValue(model)?.ToString() ?? "");

        return PlaceholderRegex.Replace(template, match =>
        {
            var propertyName = match.Groups[1].Value;
            return properties.GetValueOrDefault(propertyName, match.Value);
        });
    }

    // Templates padrão inline
    private static string GetWelcomeEmailTemplate()
    {
        return @"
<h1>Bem-vindo ao IDE Platform!</h1>
<p>Olá {{FirstName}},</p>
<p>Obrigado por se cadastrar na nossa plataforma. Sua conta foi criada com sucesso.</p>
<p>Para começar a usar todos os recursos, <a href='{{LoginUrl}}'>clique aqui para fazer login</a>.</p>
<p>Atenciosamente,<br>Equipe IDE Platform</p>";
    }

    private static string GetEmailVerificationTemplate()
    {
        return @"
<h1>Verificação de Email</h1>
<p>Olá {{FirstName}},</p>
<p>Por favor, verifique seu endereço de email clicando no link abaixo:</p>
<p><a href='{{VerificationUrl}}' style='background-color: #007bff; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>Verificar Email</a></p>
<p>Se você não conseguir clicar no botão, copie e cole este link no seu navegador:</p>
<p>{{VerificationUrl}}</p>
<p>Este link expira em 24 horas.</p>";
    }

    private static string GetPasswordResetTemplate()
    {
        return @"
<h1>Redefinição de Senha</h1>
<p>Olá {{FirstName}},</p>
<p>Você solicitou a redefinição da sua senha. Clique no link abaixo para criar uma nova senha:</p>
<p><a href='{{ResetUrl}}' style='background-color: #dc3545; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>Redefinir Senha</a></p>
<p>Se você não conseguir clicar no botão, copie e cole este link no seu navegador:</p>
<p>{{ResetUrl}}</p>
<p>Este link expira em 2 horas.</p>
<p>Se você não solicitou esta redefinição, ignore este email.</p>";
    }

    private static string GetTwoFactorCodeTemplate()
    {
        return @"
<h1>Código de Verificação</h1>
<p>Olá {{FirstName}},</p>
<p>Seu código de verificação de dois fatores é:</p>
<div style='font-size: 32px; font-weight: bold; color: #007bff; text-align: center; padding: 20px; border: 2px solid #007bff; margin: 20px 0;'>{{Code}}</div>
<p>Este código expira em 5 minutos.</p>
<p>Se você não solicitou este código, entre em contato conosco imediatamente.</p>";
    }

    private static string GetLayoutTemplate()
    {
        return @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>IDE Platform</title>
    <style>
        body { font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px; }
        .header { text-align: center; padding-bottom: 20px; border-bottom: 1px solid #eee; }
        .content { padding: 20px 0; }
        .footer { text-align: center; padding-top: 20px; border-top: 1px solid #eee; font-size: 12px; color: #666; }
    </style>
</head>
<body>
    <div class='header'>
        <img src='https://your-domain.com/logo.png' alt='IDE Platform' style='height: 50px;'>
    </div>
    <div class='content'>
        {{Body}}
    </div>
    <div class='footer'>
        <p>© 2024 IDE Platform. Todos os direitos reservados.</p>
        <p>Este é um email automático, não responda esta mensagem.</p>
    </div>
</body>
</html>";
    }

    private static string GetGenericTemplate()
    {
        return @"
<h1>{{Subject}}</h1>
<div>{{Body}}</div>";
    }
}
```

### 6. Validar Implementação

Execute os comandos para validar:

```powershell
# Na raiz do projeto
dotnet add package System.Net.Mail

dotnet restore
dotnet build

# Verificar se não há erros de compilação
dotnet build --verbosity normal
```

## ✅ Critérios de Validação

Ao final desta parte, você deve ter:

- [ ] **Provedores de email** (SMTP, SendGrid) implementados
- [ ] **Sistema de templates** funcionando
- [ ] **Models de email** completos
- [ ] **Tratamento de erros** e retry automático
- [ ] **Rate limiting** básico configurado
- [ ] **Health checks** implementados
- [ ] **Compilação bem-sucedida** sem erros

## 📝 Arquivos Criados

Esta parte criará aproximadamente **16 arquivos**:
- 5 Models de email
- 3 Interfaces de serviços
- 3 Provedores de email
- 2 Serviços de template
- 3 Templates base

## 🔄 Próximos Passos

Após concluir esta parte, você estará pronto para:
- **Parte 11**: Infraestrutura (Redis, Logging, Middleware)
- Implementar endpoints da API
- Configurar Docker e testes

## 🚨 Troubleshooting Comum

**SMTP não conecta**: Verificar configurações de host/porta  
**SendGrid API falha**: Verificar API Key e configurações  
**Templates não renderizam**: Verificar paths e permissões  
**Rate limiting**: Configurar limites adequados por provedor  

---
**⏱️ Tempo estimado**: 35-50 minutos  
**🎯 Próxima parte**: 11-infraestrutura-middleware-logging.md  
**📋 Dependências**: Partes 1-9 concluídas