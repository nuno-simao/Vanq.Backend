# Fase 3 (Parte 1) – Contexto, Objetivos e Visão Geral

> Conjunto: Colaboração em Tempo Real – Backend .NET 8
> Esta é a primeira parte da divisão do documento original `03-fase-colaboracao-tempo-real.md`.

## 1. Contexto da Fase
A **Fase 3** introduz capacidades colaborativas em tempo real sobre a base consolidada nas Fases 1 (autenticação/segurança) e 2 (workspaces, itens e permissões). O objetivo é permitir múltiplos usuários trabalhando simultaneamente em um mesmo espaço, com sincronização consistente e auditável.

**Pré-requisito**: Fases 1 e 2 100% funcionais (auth, usuários, workspaces, itens, permissões, versionamento básico).

## 2. Objetivos Principais
✅ SignalR Hub autenticado e escalável  
✅ Edição colaborativa simultânea com Operational Transform (OT)  
✅ Chat por workspace com threads e menções  
✅ Notificações em tempo real (menções, entradas, alterações)  
✅ Gestão de presença e cursores múltiplos  
✅ Indicadores de digitação e seleção de texto  
✅ Snapshots + operações para versionamento híbrido  
✅ Métricas, auditoria e rate limiting para governança

## 3. Funcionalidades de Tempo Real (Resumo)
- **Edição Simultânea**: Broadcast de operações de texto transformadas (OT) + resolução de conflitos.
- **Chat em Workspace**: Mensagens, respostas, reações, menções e futura possibilidade de anexos.
- **Notificações**: Eventos de colaboração (convites, presença, conflitos, menções, mudanças de item/fase).
- **Presença**: Usuários ativos, status (online/away/busy/offline) e item atual que está sendo editado.
- **Cursores & Seleções**: Posição do cursor e seleção de texto de cada colaborador com cor identificadora.
- **Snapshots & Histórico**: Combinação de snapshots periódicos + operações incrementais para recuperação rápida.
- **Métricas & Auditoria**: Contadores de eventos, latências, logs de ações (conectar, editar, resolver conflitos, etc.).
- **Rate Limiting Dinâmico**: Limites por plano (Free/Pro/Enterprise) e por tipo de operação (edição, chat, cursor, presença).

## 4. Escopo Desta Parte
Esta Parte 1 NÃO entra ainda em detalhes de código. Serve como:
- Base conceitual para alinhamento entre backend/frontend/devops.
- Referência para priorização de entregas incrementais.
- Ponto de navegação para as demais partes.

## 5. Navegação das Partes
| Parte | Conteúdo |
|-------|----------|
| 1 | Contexto, objetivos (este arquivo) |
| 2 | Entidades e enums de colaboração |
| 3 | Configuração EF Core e parâmetros do sistema |
| 4 | DTOs e Requests |
| 5 | Hub SignalR – operações principais |
| 6 | Hub SignalR – conflitos, OT avançado, helpers |
| 7 | Serviços núcleo (presença, OT, chat, notificações) |
| 8 | Serviços cross-cutting (métricas, rate limiting, auditoria) |
| 9 | Endpoints REST + configuração Program.cs |
| 10 | Validação, checklist e próximos passos |

## 6. Dependências entre Partes (Resumo)
- Partes 2 e 3 fundamentam dados e infraestrutura para todas as demais.
- Parte 4 fornece contratos de comunicação (Hubs e REST).
- Partes 5–6 dependem das Partes 7–8 para regras e persistência.
- Parte 9 expõe endpoints para operações complementares ao hub.
- Parte 10 consolida validação e readiness.

## 7. Critérios de Sucesso Globais (serão detalhados na Parte 10)
1. Latência média aceitável nas operações de edição (< objetivo definido futuramente).  
2. Conflitos simples resolvidos automaticamente sem perda de conteúdo.  
3. Reconexões preservam consistência do documento.  
4. Auditoria reproduz linha do tempo de eventos críticos.  
5. Rate limiting impede abuso sem prejudicar uso legítimo.

## 8. Próxima Leitura Recomendada
Continue com a **Parte 2 – Modelo de Domínio (Entidades + Enums)**.

---
_Parte 1 concluída. Seguir para: 03-02-dominio-colaboracao.md_
