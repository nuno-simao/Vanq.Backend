---
spec:
  id: SPEC-0010
  type: feature
  version: 0.1.0
  status: draft          # draft | reviewing | approved | deprecated
  owner: nuno-simao
  created: 2025-09-30
  updated: 2025-09-30
  priority: high
  quality_order: [observability, reliability, performance, security, delivery_speed, cost]
  tags: [metrics, observability, telemetry]
---

# 1. Objetivo
Introduzir um plano de métricas padronizado na Vanq API utilizando APIs nativas de métricas (.NET `System.Diagnostics.Metrics`/`IMeterFactory`), permitindo monitorar indicadores chave (auth, performance, erros) e expondo dados para ferramentas de observabilidade.

# 2. Escopo
## 2.1 In
- Configurar meter provider global e registrar `Meter`s nomeados por domínio (`Vanq.Auth`, `Vanq.Infrastructure`, etc.).
- Instrumentar contadores, histogramas e gauges críticos (ex.: cadastros, logins, latência de endpoints, erros).
- Expor endpoint `/metrics` (OpenMetrics/Prometheus) ou integrar com exporter existente.
- Suporte a tags padronizadas (status, rota, ambiente).
- Documentação de métricas disponíveis e como consumi-las.

## 2.2 Out
- Implementar dashboards (Grafana, AppInsights) — apenas indicar integração.
- Monitoramento de logs (já coberto em spec de logging).
- Traçar dependências externas (poderá ser abordado em spec de tracing).

## 2.3 Não Fazer
- Reintroduzir bibliotecas externas de métricas (preferir nativas; open-source exporters oficiais se necessário).
- Expor dados sensíveis nas labels/tags.

# 3. Requisitos Funcionais
| ID | Descrição | Criticidade (MUST/SHOULD/MAY) |
|----|-----------|------------------------------|
| REQ-01 | Configurar `IMeterFactory` central e registrar meters para Auth, Infra e API. | MUST |
| REQ-02 | Criar contador `auth_user_registration_total` com labels `status`. | MUST |
| REQ-03 | Instrumentar latência (`Histogram<double>`) para `/auth/register` e demais endpoints críticos. | MUST |
| REQ-04 | Expor endpoint `/metrics` (Prometheus/OpenMetrics) controlado por feature flag e autenticação opcional. | MUST |
| REQ-05 | Documentar métricas (nome, descrição, labels) em README e `/docs`. | SHOULD |
| REQ-06 | Disponibilizar utilitário/abstração para registrar métricas sem duplicação (ex.: `IMetricsService`). | SHOULD |
| REQ-07 | Permitir habilitar/desabilitar grupos de métricas via configuração (ex.: métricas detalhadas em dev). | MAY |

# 4. Requisitos Não Funcionais (Prioridades Relevantes)
| ID | Categoria | Descrição | Métrica / Aceite |
|----|-----------|-----------|------------------|
| NFR-01 | Performance | Overhead de coleta deve ser baixo (< 5% latência). | Bench dev |
| NFR-02 | Observabilidade | 100% dos fluxos críticos possuem métrica (cadastro, login, refresh). | Auditoria |
| NFR-03 | Segurança | Endpoint `/metrics` protegido em ambientes não públicos (token ou IP allowlist). | Teste segurança |
| NFR-04 | Confiabilidade | Falha no exporter não derruba API (fail-open). | Teste falha |

# 5. Regras de Negócio
| ID | Descrição |
|----|-----------|
| BR-01 | Métricas devem usar namespace `vanq_` prefixado (ex.: `vanq_auth_user_registration_total`).
| BR-02 | Labels obrigatórias: `environment`, `status`, `route` (quando aplicável).
| BR-03 | Não incluir dados pessoais em labels/valores.

# 6. Novas Entidades
Sem entidades de domínio; criar classes de configuração/helpers.

| ID | Nome | Propósito | Observações |
|----|------|-----------|-------------|
| ENT-01 | MetricsOptions | Centralizar configuração de exporters, habilitação e segurança. | Config.

## 6.1 Campos (Somente Entidades Novas)
| Entidade | Campo | Tipo | Nullable | Regra / Constraint |
|----------|-------|------|----------|--------------------|
| MetricsOptions | EnablePrometheus | bool | Não | Default true (dev) |
| MetricsOptions | MetricsPort | int | Sim | Porta custom para scraping |
| MetricsOptions | RequireAuth | bool | Não | Default false |
| MetricsOptions | AllowedIPs | string[] | Sim | Restrição opcional |
| MetricsOptions | EnabledMeters | string[] | Sim | Lista de meters habilitados |

# 7. Impactos Arquiteturais
| Camada | Alterações | Notas |
|--------|------------|-------|
| Domain | Nenhum impacto direto. | |
| Application | Serviços devem usar `IMetricsService` para registrar eventos. | Injeção via DI.
| Infrastructure | Configurar exporter (ex.: `AddPrometheusExporter`) e meters. | Revisar `ServiceCollectionExtensions`.
| API | Atualizar `Program.cs` para mapear `/metrics` e proteger endpoint. | Documentar.

# 8. API (Se aplicável)
| ID | Método | Rota | Auth | REQs | Sucesso | Erros |
|----|--------|------|------|------|---------|-------|
| API-01 | GET | /metrics | Opcional (depende de config) | REQ-04 | 200 text/plain OpenMetrics | 401,403 (quando protegido) |

# 9. Segurança & Performance
- Segurança: proteger `/metrics` em produção (token, network ACL); mascarar dados sensíveis.
- Performance: evitar métricas com alta cardinalidade (labels controladas); usar `Meter` spiders por domínio.
- Observabilidade: oferecer instruções para coletar com Prometheus/OTel collector; logs para falhas no exporter.

# 10. i18n
Não aplicável (nomenclatura padrão em inglês). Documentação deve explicar termos em português/inglês quando necessário.

# 11. Feature Flags
| ID | Nome | Escopo | Estratégia | Fallback |
|----|------|--------|------------|----------|
| FLAG-01 | metrics-enabled | Infra | Ativa/desativa exporter globalmente. | Desligado → métricas desativadas |
| FLAG-02 | metrics-detailed-auth | Application | Habilita métricas detalhadas de auth. | Desligado → registra apenas contadores gerais |

# 12. Tarefas
| ID | Descrição | Dependências | REQs |
|----|-----------|--------------|------|
| TASK-01 | Adicionar configuração de métricas (`MetricsOptions`) em `appsettings`. | - | REQ-05,REQ-07 |
| TASK-02 | Registrar `IMeterFactory` e meters por domínio (`Vanq.Auth`, etc.). | TASK-01 | REQ-01 |
| TASK-03 | Criar `IMetricsService` e implementações (`MetricsService`). | TASK-02 | REQ-06 |
| TASK-04 | Instrumentar AuthService (registro, login, refresh) com contadores e latência. | TASK-03 | REQ-02,REQ-03 |
| TASK-05 | Configurar exporter `/metrics` com autenticação opcional. | TASK-02 | REQ-04 |
| TASK-06 | Adicionar métricas de infraestrutura (ex.: Refresh token store). | TASK-03 | NFR-02 |
| TASK-07 | Documentar métricas produzidas e instruções de scraping. | TASK-05 | REQ-05 |
| TASK-08 | Criar testes avaliando registros de métricas e proteção do endpoint. | TASK-04 | NFR-01, NFR-03 |

# 13. Critérios de Aceite
| REQ | Critério |
|-----|----------|
| REQ-01 | `IMeterFactory` configurado; meters registrados no DI.
| REQ-02 | `vanq_auth_user_registration_total{status="success"}` incrementa após cadastro válido.
| REQ-03 | Histogram de latência (`vanq_auth_register_duration_seconds`) coleta dados em requests.
| REQ-04 | `/metrics` retorna dados em formato Prometheus; quando protegido, solicita auth.
| REQ-05 | README/docs descrevem métricas, labels e como habilitar.

# 14. Testes (Mapa Resumido)
| TEST | Tipo | Cobre REQ | Descrição |
|------|------|-----------|-----------|
| TEST-01 | Integration | REQ-02 | Executa cadastro e verifica incremento do contador via `MeterListener`.
| TEST-02 | Unit | REQ-03 | Middleware/serviço registra latências simuladas.
| TEST-03 | Integration | REQ-04 | Verifica `/metrics` com e sem autenticação/flag.
| TEST-04 | Unit | REQ-06 | `MetricsService` aplica tags corretas.
| TEST-05 | Integration | NFR-01 | Benchmark comparando latência com métricas habilitadas vs desabilitadas.

# 15. Decisões
| ID | Contexto | Decisão | Alternativas | Consequência |
|----|----------|--------|--------------|--------------|
| DEC-01 | Biblioteca | Usar `System.Diagnostics.Metrics` nativo + exporters oficiais. | libs terceiras | Menos dependência.
| DEC-02 | Exporter | Prometheus/OpenMetrics via `AspNetCore.Metering`. | Application Insights direto | Flexibilidade cross-stack.
| DEC-03 | Naming | Prefixar `vanq_` + domínio | nomes genéricos | Aderência a padrões observability.

# 16. Pendências / Questões
| ID | Pergunta | Responsável | Status |
|----|----------|-------------|--------|
| QST-01 | Qual ferramenta de observabilidade será usada (Prometheus, Azure Monitor)? | owner | Aberto |
| QST-02 | Precisamos limitar acesso ao `/metrics` apenas em rede interna? | owner | Aberto |
| QST-03 | Métricas de camada DB (Npgsql) devem ser adicionadas neste escopo? | owner | Aberto |

# 17. Prompt Copilot (Resumo)
Copilot: Implementar SPEC-0010 configurando metrics via `IMeterFactory`, instrumentando Auth (contadores, latência), expondo `/metrics` com proteção opcional, adicionando options, feature flags e documentação. Garantir testes e guidelines.

Fim.
