# Instruções de Uso do GitHub Copilot – Vanq.Backend

> Documento de orientação para maximizar a utilidade do GitHub Copilot neste repositório backend (.NET / C#) mantendo padrões arquiteturais, qualidade, segurança e consistência. Atualize sempre que arquitetura, dependências ou práticas mudarem.

---
## 1. Visão Geral do Projeto
- Nome: Vanq Backend
- Domínio / Problema: Plataforma Vanq – serviços de backend (ex: contas, pagamentos, operações de negócio). (Detalhar módulos reais quando definidos)
- Stakeholders principais: Equipe de engenharia, produto, QA, stakeholders de negócio
- Critérios de sucesso:
  - Estabilidade (SLO / baixa taxa de erro)
  - Latência consistente (P95 < 300ms em rotas críticas)
  - Evolutividade (facilidade de adicionar novos módulos)
  - Observabilidade completa (logs, métricas, traces)
- Principais módulos / bounded contexts (provisório – ajustar):
  - Accounts
  - Auth / Identity
  - Payments
  - Notifications
  - Integrations (provedores externos)
  - Shared (cross-cutting)

## 2. Stack Tecnológica
| Camada | Tecnologia / Framework | Versão (alvo) | Observações |
|--------|------------------------|---------------|-------------|
| Backend (API) | .NET 8 (ASP.NET Core) | 8.x | Minimal APIs ou Controllers (definir) |
| Domínio / ORM | EF Core | 8.x | Uso de Migrations e Value Objects |
| Banco de Dados | PostgreSQL | 16.x | Driver Npgsql |
| Mensageria | (Provável) RabbitMQ / Outbox Pattern | TODO | Outbox + Worker para publicação |
| Cache | Redis | 7.x | Para tokens / caching de leitura |
| Infraestrutura | Docker / Compose / (K8s futuramente) | - | IaC: TODO (Terraform?) |
| Observabilidade | Serilog + OpenTelemetry + Prometheus + Grafana | - | Tracing + métricas custom |
| Autenticação | JWT / OAuth2 (IdentityServer / Auth Provider externo) | TODO | Guardar config em `appsettings.*` |
| Testes | xUnit + FluentAssertions + Bogus + WireMock.Net | - | Integração + contrato |
| CI/CD | GitHub Actions | - | Pipelines multi-stage |

Dependências críticas (documentar):
- Serilog, Microsoft.FeatureManagement, Polly, Npgsql, EF Core, OpenTelemetry (Exporter OTLP), Mapster (ou AutoMapper) – confirmar escolhas.

## 3. Estrutura do Repositório (proposta)
```
.
├─ src/
│  ├─ Core/                (Entidades, VO, Regras de domínio, Eventos)
│  ├─ Application/         (UseCases / Commands / Queries / DTOs)
│  ├─ Infrastructure/      (EF, Repositórios, Mensageria, Implementações Ports)
│  ├─ Api/                 (Endpoints / Controllers / Filters / DI Setup)
│  ├─ Shared/              (Utilidades cross-cutting: Result, Errors, Time, Ids)
│  └─ Integrations/        (Adapters provedores externos)
├─ tests/
│  ├─ Unit/
│  ├─ Integration/
│  ├─ Contract/
│  └─ EndToEnd/
├─ docs/
├─ scripts/
└─ tools/
```
Função de cada diretório: Conforme comentários acima. Ajustar se divergente do real.

## 4. Princípios de Arquitetura
- Estilo: Clean Architecture + DDD (Monólito Modular evolutivo)
- Regras:
  - Camada de Domínio é pura (sem dependências de infra / UI)
  - Application orquestra casos de uso e publica eventos
  - Infraestrutura implementa interfaces (Ports) do domínio / application
  - API só contém mapeamento transporte ↔ DTO ↔ domínio
  - Comunicação entre módulos via serviços de domínio + eventos internos (Domain Events)
- Anti‑padrões a evitar: God Services, anêmico sem invariantes, lógica no Controller, acoplamento circular, repositórios “genéricos” excessivos.

## 5. Convenções de Código
- Idioma código / nomes: Inglês para tipos/pastas; Português aceitável em comentários de negócio críticos.
- Nomes de arquivos: Classes = PascalCase (`PaymentService.cs`); utilidades internas podem usar sufixos claros (`*Extensions.cs`).
- Pastas por contexto + subpastas por agregado.
- Exports: Não aplicável (C#). Evitar múltiplas classes públicas por arquivo.
- Limite recomendado: < 400 linhas por arquivo / < 25 linhas por método quando possível.
- Estilo: `EditorConfig` + `dotnet format` + Analyzers (StyleCop / Roslyn) – configurar se não existir.

## 6. Padrões de Projeto
| Caso | Padrão | Exemplo |
|------|--------|---------|
| Criação de Entidade | Factory / Método Estático | `Payment.Create()` valida invariantes |
| Orquestração | UseCase (Command Handler) | `RefundPaymentHandler` |
| Query Leitura | Query + Handler | `GetPaymentStatusQueryHandler` |
| Domínio complexo | Aggregate Root | `PaymentAggregate` controla estado/refunds |
| Integrações externas | Adapter + Port (Interface) | `IPaymentGatewayClient` + `StripePaymentGatewayAdapter` |
| Processamento assíncrono | Event Handler + Outbox | `PaymentRefundedEventHandler` |
| Resiliência | Polly Policies | Retry / Circuit Breaker |

Não criar abstrações sem pressão de mudança real.

## 7. Estratégia de Testes
| Tipo | Ferramenta | Escopo | Critério |
|------|------------|--------|---------|
| Unitário | xUnit + FluentAssertions | Domínio / UseCases puros | Cobertura > 80% Domínio |
| Integração | xUnit + Testcontainers (se adotado) | Repositórios / EF / Mensageria | Fluxos críticos cobertos |
| Contrato | WireMock.Net / Pact (se necessário) | APIs externas / Webhooks | Endpoints críticos verificados |
| End-to-End | (Playwright via API / K6 smoke) | Fluxos principais API | Smoke diário |
| Performance | K6 / BenchmarkDotNet (micro) | Endpoints sensíveis | P95 < 300ms |

Diretrizes:
- Testes determinísticos (evitar dependência de horário real / aleatório sem seed)
- Evitar mocks excessivos em domínio (prefira instanciar objetos reais)
- Nomear métodos: `Deve_<acao>_Quando_<condicao>()` ou `[Fact(DisplayName="... ")]` coerente.

## 8. Segurança e Privacidade
- Nunca commitar segredos (usar GitHub Secrets / .env.local ignorado)
- `.env.example` sempre atualizado sem valores reais
- Logs sem dados sensíveis (mascarar PII / tokens)
- Validação de entrada: FluentValidation ou própria; sanitizar HTML (se houver) – atualmente API JSON
- Dependências auditadas: `dotnet list package --vulnerable` na pipeline
- Autorização granular via Policies / Claims

## 9. Performance & Escalabilidade
- Evitar N+1: usar Include / projeções adequadas / CQRS leitura especializada
- Caching: Redis para sessões / resultados de leitura estáveis (TODO: definir chaves e TTL)
- P95 endpoints críticos < 300ms, cold start API < 2s em container
- Usar Async sempre que I/O bound
- Resiliência externa: Retry com jitter + Circuit Breaker (Polly)
- Paginação padrão para coleções (> 50 itens) – sem retorno massivo inteiro

## 10. Observabilidade
- Logging estruturado (Serilog JSON): nível default Information, reduzir ruído
- CorrelationId: header `X-Correlation-Id` → inserir se ausente
- Tracing distribuído: OpenTelemetry com export OTLP (TraceId em logs)
- Métricas: Requests por rota, latência, erros, DB time, fila pendente
- Dashboard padrão: Grafana (Painéis: Latência, Erros 5xx, Throughput, Uso de DB)

## 11. Fluxo de Desenvolvimento
1. Branch naming: `feat/<contexto>`, `fix/<contexto>`, `chore/<contexto>`, `refactor/<contexto>`
2. Commits semânticos (pt ou en, consistentes):
   - `feat: adicionar refund handler`
   - `fix: corrigir cálculo de taxa`
   - `refactor: simplificar agregate Payment`
   - `test: cobrir cenário de duplicidade`
   - `docs: atualizar instruções de migração`
   - `perf: otimizar consulta de pagamentos`
   - `build:` (pipelines / Docker)
   - `chore:` (infra / tooling)
3. Pull Request:
   - Descrição: porquê / o quê / como validar
   - Checklist: migração? feature flag? rollback? performance?
   - Requer >= 1 (ideal 2) revisões

## 12. Integração Contínua (CI/CD)
Pipeline (GitHub Actions):
1. Restore + Build
2. Analyzers / Lint (`dotnet format --verify-no-changes`)
3. Testes (unit + integração)
4. Verificação vulnerabilidades / SCA
5. Publish artefato + Docker image
6. Deploy Staging (tag pré-release) → Smoke → Deploy Produção (tag release)
- Versionamento: SemVer (MAJOR.MINOR.PATCH). Tags `v1.2.3`

## 13. Uso do GitHub Copilot (Objetivo)
Copilot deve acelerar:
- Esqueletos de UseCases / Handlers / DTOs
- Testes unitários repetitivos
- Geração inicial de Migrations (revisar)
- Adapters externos scaffolding
- Documentação técnica inicial em `docs/`
Não deve:
- Inventar regras de negócio
- Introduzir libs sem justificativa
- Implementar criptografia manual insegura
- Escrever lógica crítica sem revisão

## 14. Como Escrever Bons Prompts
Estrutura:
1. Contexto: local + objetivo
2. Restrições: performance, padrões, libs
3. Formato esperado: classe, teste, migration
4. Casos / exemplos
Exemplo bom:
```
Gerar Command + Handler em src/Application/Payments para refund.
Entrada: PaymentId (Guid), Reason (string). Validar que Payment está em estado `Captured` ou `Settled`. Disparar DomainEvent PaymentRefunded.
Criar teste unitário cobrindo: sucesso, estado inválido, valor já reembolsado.
```
Exemplo ruim:
```
Faz refund
```

## 15. Diretrizes de Comentários para Copilot
```csharp
// Objetivo: Normalizar CPF removendo máscara e validando dígitos.
// Requisitos: sem libs externas, lançar ValidationException custom.
// Saída: string de 11 dígitos.
// Também gerar método IsValidCpf.
```
Ou bloco detalhado com campos obrigatórios, descartados e validações.

## 16. Diretrizes por Linguagem (C#)
- Nullable Reference Types ON
- Evitar `async void` (exceto event handlers UI – não aplicável)
- Não usar `dynamic` sem necessidade
- Preferir Records para DTOs imutáveis
- Value Objects: sealed + validação invariantes + igualdade estrutural
- Lançar exceções específicas (ex: `DomainException`, `ValidationException`)

## 17. Estilo de Respostas Desejado do Copilot
- Funções puras onde possível
- Métodos curtos, clareza > micro-otimização
- Retornos explícitos (evitar efeitos colaterais ocultos)
- Comentários somente onde a intenção não é óbvia

## 18. Arquivos Importantes para Contexto
- `docs/architecture.md`
- `src/Core/` (Entidades / Value Objects)
- `src/Application/` (UseCases / Commands / Queries)
- `src/Infrastructure/` (EF / Adapters)
- `src/Api/Program.cs` ou `Startup.cs` (DI / Pipeline)

## 19. Feature Flags
- Biblioteca: Microsoft.FeatureManagement (confirmar)
- Convenção: `FF_<NOME_DESCRITIVO>` ex: `FF_REFUND_V2`
- Remover código legado só após: 100% ON + métricas estáveis + janela rollback encerrada

## 20. Migrações de Banco
- EF Core Migrations: `dotnet ef migrations add <Nome>`
- Conferir índices e chaves estrangeiras
- Não misturar alteração de schema crítica com carga de dados sem plano de rollback
- Scripts automatizados em `scripts/`

## 21. Erros & Exceções
- Exceções custom em `src/Shared/Errors` (ex: DomainException, NotFoundException, ValidationException)
- Não engolir stack: sempre logar + rethrow se necessário
- Mapeamento HTTP (exemplo):
  - ValidationException → 400
  - NotFoundException → 404
  - DomainException (regra negada) → 422
  - Unauthorized → 401 / Forbidden → 403
  - Demais → 500 (com Id de correlação no corpo)

## 22. Acessibilidade (N/A API)
- Se futuramente expor UI (Swagger custom), garantir descrições claras.

## 23. Internacionalização
- Strings de erro e mensagens técnicas em inglês
- Mensagens de negócio podem ter fallback pt-BR (definir estratégia se necessário)

## 24. Checklist Antes do Merge
- [ ] Build + testes verdes
- [ ] Cobertura domínio >= 80%
- [ ] Sem `TODO` críticos
- [ ] Sem `Console.WriteLine` / logs debug restantes
- [ ] Sem segredos / chaves expostas
- [ ] Migrations revisadas (se existirem)
- [ ] Documentação atualizada (arquitetura / endpoints)

## 25. Exemplos de Solicitações ao Copilot
Gerar teste:
```
Gerar teste unitário para RefundPaymentHandler cobrindo:
- refund válido
- estado inválido
- já reembolsado
```
Refactor:
```
Refatorar PaymentService.CalculateFees em src/Application/Payments:
- Reduzir branches
- Tornar puro
- Manter compatibilidade com testes existentes
```
Documentação:
```
Gerar seção docs/authentication.md explicando fluxo:
- Login OAuth2
- Emissão de JWT + Refresh Token
- Renovação e expiração
```

## 26. Anti‑Padrões a Evitar
- Controllers gordos (colocar lógica em UseCases)
- Repositório genérico para tudo sem necessidade
- Modificar estado interno fora do Aggregate Root
- Duplicar validação (ex: mesma regra em 3 camadas)
- Ignorar warnings de Analyzers

## 27. Atualizando Este Arquivo
- Revisar a cada Sprint ou mudança estrutural
- Alterações via PR separado com label `copilot-instructions`
- Manter histórico claro (commits explicativos)

## 28. Roadmap (Exemplo)
| Trimestre | Objetivo | Métrica |
|-----------|----------|---------|
| Q4 2025 | Observabilidade completa | 100% rotas com métricas |
| Q1 2026 | Modularização avançada | Separação clara contextos |

## 29. Referências Internas (Adicionar links reais)
- Guia arquitetura: docs/architecture.md
- Padrões exceção: docs/errors.md (TODO)
- Threat model: docs/security.md (TODO)
- Playbook incidentes: docs/incident-playbook.md (TODO)

## 30. Licenciamento & Compliance
- Licença: MIT (confirmar) / TODO
- Revisar uso de libs (verificar licenças permissivas)
- LGPD / GDPR: mascarar PII, retention policy (TODO)

---
## Anexo A: Template de Prompt
```
Contexto:
[descrição do módulo / arquivo]

Objetivo:
[o que precisa ser produzido]

Restrições:
[performance / padrões / libs]

Entrada / Saída:
[tipos / exemplos]

Formato Final:
[classe / handler / teste / doc]

Não Fazer:
[lista]
```

## Anexo B: Marcadores Úteis
- // TODO: curto prazo
- // FIXME: corrigir antes produção
- // NOTE: decisão deliberada
- // REVIEW: atenção do revisor

---
Mantenha este arquivo curto o suficiente (< ~600 linhas) e específico.

Última revisão: 2025-09-26
Responsável: nuno-simao