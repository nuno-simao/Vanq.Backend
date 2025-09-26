# Fase 3 (Parte 6) – Hub SignalR (Conflitos, OT Avançado e Auxiliares)

> Complemento da Parte 5. Aqui ficam resolução de conflitos, locking de itens, métodos auxiliares, menções e sharding.

## 1. Resolução de Conflitos (Visão)
Quando múltiplos usuários enviam operações contemporâneas sobre a mesma região lógica do texto:
- Conflitos simples: transform automátic a (merge posicional) → segue broadcast
- Conflitos complexos: requer decisão do usuário (lista de opções)
- Conflitos críticos: item é bloqueado até intervenção/manual review

## 2. Método HandleConflict
Responsável por classificar e comunicar o conflito.
```csharp
private async Task HandleConflict(string itemId, OperationTransformResult transformResult, Guid userId)
{
    var conflictDto = new ConflictResolutionDto { /* mapeamento de transformResult */ };
    await _auditService.LogAsync(AuditAction.ResolveConflict, "item", itemId, userId, /* details */);
    await _metricsService.IncrementAsync("conflicts_detected", 1, new { itemId, type = transformResult.ConflictType.ToString() });

    if (transformResult.ConflictType == ConflictType.Simple)
    {
        var resolvedOperation = await _otService.ResolveConflictAsync(conflictDto, ResolutionStrategy.AutomaticMerge);
        await Clients.Caller.SendAsync("ConflictResolved", new { ConflictId = conflictDto.Id, Resolution = "automatic" });
        await _metricsService.IncrementAsync("conflicts_auto_resolved", 1);
    }
    else if (transformResult.ConflictType == ConflictType.Complex)
    {
        await Clients.Caller.SendAsync("ConflictDetected", new { Conflict = conflictDto, RequiresUserChoice = true });
        await _metricsService.IncrementAsync("conflicts_user_choice", 1);
    }
    else // Critical
    {
        await _otService.LockItemAsync(Guid.Parse(itemId), userId);
        await Clients.Group($"item_{itemId}").SendAsync("ItemLocked", new { Reason = "Critical conflict" });
        await _metricsService.IncrementAsync("conflicts_critical", 1);
    }
}
```

## 3. ResolveConflict
Recebe escolha do cliente para estratégia aplicada e faz broadcast do resultado se bem-sucedido:
```csharp
public async Task ResolveConflict(string itemId, string conflictId, ResolutionStrategy strategy, TextOperationDto resolvedOperation)
{
    var result = await _otService.ResolveConflictAsync(Guid.Parse(conflictId), strategy, resolvedOperation, GetUserId());
    if (result.Success)
    {
        await _otService.ApplyOperationAsync(Guid.Parse(itemId), result.ResolvedOperation);
        await Clients.Group($"item_{itemId}").SendAsync("ConflictResolved", new { ConflictId = conflictId, Strategy = strategy.ToString() });
        await _metricsService.IncrementAsync("conflicts_resolved", 1, new { strategy = strategy.ToString() });
    }
}
```

## 4. Menções no Chat (ProcessMessageMentions)
Analisa conteúdo da mensagem por padrão `@username` e gera notificações direcionadas.
```csharp
private async Task ProcessMessageMentions(ChatMessageDto message, Guid workspaceId)
{
    var mentionPattern = @"@(\w+)";
    var matches = Regex.Matches(message.Content, mentionPattern);
    foreach (Match m in matches)
    {
        var username = m.Groups[1].Value;
        var mentionedUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (mentionedUser != null && await HasWorkspaceAccess(workspaceId, mentionedUser.Id))
        {
            await _notificationService.CreateNotificationAsync(new CreateNotificationRequest { /* ... */ });
            await _metricsService.IncrementAsync("chat_mentions", 1);
        }
    }
}
```

## 5. Auxiliares de Segurança & Utilidade
| Método | Função |
|--------|--------|
| `GetUserId()` | Extrai claim `id` do usuário autenticado |
| `HasWorkspaceAccess()` | Verifica permissão mínima (Reader/Editor) |
| `GetUserPlanAsync()` | Usado para rate limiting baseado em plano |
| `GetSystemParameterAsync()` | Recupera parâmetros dinâmicos (limites) |
| `GetUserColor()` | Determinístico para identidade visual em cursores |

## 6. Sharding Dinâmico
Implementação simples via hash do ID do workspace:
```csharp
private string GetShardGroup(string workspaceId)
{
    var shardCount = GetShardCount();
    var index = Math.Abs(workspaceId.GetHashCode()) % shardCount;
    return $"workspace_{workspaceId}_shard_{index}";
}
```
Permite evolução futura para balancear carga entre múltiplos nós / clusters.

## 7. Bloqueio de Itens (Critical Conflicts)
`LockItemAsync` no serviço OT impede novas operações até resolução registrada. Recomendado adicionar TTL de lock ou comando administrativo de override.

## 8. Estratégia de Cores por Usuário
`GetUserColor(userId)` seleciona uma cor fixa de paleta predefinida — reduz flicker visual e comunicação de estado entre clientes.

## 9. Latência e Métricas Avançadas
Eventos críticos registram:
- `edit_latency` (tempo de processamento da operação + transform)
- `conflicts_detected` e subdivisões por tipo
- `conflicts_resolved` segmentado por estratégia

## 10. Boas Práticas de Cliente na Resolução de Conflitos
| Cenário | Ação Cliente |
|---------|--------------|
| Conflito simples auto-resolvido | Aplicar operação transformada e continuar buffer |
| Conflito complexo | Abrir modal com diff pré-calculado |
| Conflito crítico | Exibir mensagem “Documento bloqueado” + polling leve |

## 11. Observações de Evolução
- Migrar OT para estratégia híbrida (OT + CRDT) para latência distribuída multi-região.
- Adicionar prioridade de usuário (owners podem destravar itens críticos manualmente).
- Expandir menções para suportar grupos `@all`, `@here` com limites.

## 12. Próxima Leitura
Avance para a **Parte 7 – Serviços Núcleo (Presença, OT, Chat, Notificações)**.

---
_Parte 6 concluída. Próximo: 03-07-servicos-nucleo.md_
