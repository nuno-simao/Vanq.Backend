# 📚 Documentação Técnica - Vanq.Backend

**Última Atualização:** 2025-10-01  
**Versão:** 1.0

---

## 📖 Índice

### 🎯 Planejamento e Implementação
- [**Ordem de Implementação (Detalhada)**](implementation-order-specs-1-10.md) - Análise completa de dependências e ordem sugerida para SPECs 1-10
- [**Ordem de Implementação (Resumo)**](implementation-order-summary.md) - Guia rápido executivo com checklists
- [**Roadmap de Implementação**](implementation-roadmap-specs-1-10.md) - Timeline, sprints e kanban board
- [**Matriz de Dependências**](dependencies-matrix-specs-1-10.md) - Mapeamento detalhado de dependências entre specs

### 🔧 Arquitetura e Funcionalidades
- [**Persistência (Database)**](persistence.md) - Guia de migrações, entidades e boas práticas EF Core
- [**Feature Flags**](feature-flags.md) - Sistema de feature flags nativo (SPEC-0006)
- [**RBAC Overview**](rbac-overview.md) - Sistema de controle de acesso baseado em roles (SPEC-0011)
- [**Feature Flags + RBAC Migration**](feature-flags-rbac-migration.md) - Guia de migração do antigo sistema RBAC para feature flags

### ✅ Relatórios de Validação
- [**SPEC-0006 Validation Report**](SPEC-0006-validation-report.md) - Validação de conformidade do sistema de Feature Flags
- [**SPEC-0011 Validation Report**](SPEC-0011-validation-report.md) - Validação de conformidade do sistema RBAC

---

## 🚀 Quick Start

### Para Desenvolvedores Novos

1. **Leia primeiro:**
   - [persistence.md](persistence.md) - Entenda a estrutura do banco
   - [feature-flags.md](feature-flags.md) - Sistema de toggles de funcionalidades
   - [rbac-overview.md](rbac-overview.md) - Sistema de permissões

2. **Antes de implementar uma nova SPEC:**
   - [implementation-order-summary.md](implementation-order-summary.md) - Veja se há dependências
   - [dependencies-matrix-specs-1-10.md](dependencies-matrix-specs-1-10.md) - Valide ordem de implementação

3. **Durante o desenvolvimento:**
   - Siga as guidelines em cada guia técnico
   - Use feature flags para todas as novas funcionalidades
   - Implemente testes conforme critérios de aceite das SPECs

---

## 📋 Guias de Implementação por SPEC

### SPECs Implementadas ✅

| SPEC | Nome | Guia | Status |
|------|------|------|--------|
| SPEC-0006 | Feature Flags | [feature-flags.md](feature-flags.md) | ✅ Implementado |
| SPEC-0011 | RBAC | [rbac-overview.md](rbac-overview.md) | ✅ Implementado |

### SPECs Planejadas 📅

| SPEC | Nome | Ordem Sugerida | Dependências | Guia |
|------|------|----------------|--------------|------|
| SPEC-0009 | Structured Logging | 1º | Nenhuma | Em planejamento |
| SPEC-0003 | Problem Details | 2º | SPEC-0009 (fraca) | Em planejamento |
| SPEC-0005 | Error Middleware | 3º | SPEC-0009, SPEC-0003 | Em planejamento |
| SPEC-0002 | CORS Support | 4º (paralelo) | Nenhuma | Em planejamento |
| SPEC-0004 | Health Checks | 5º (paralelo) | SPEC-0009 (fraca) | Em planejamento |
| SPEC-0008 | Rate Limiting | 6º | SPEC-0005, SPEC-0009 | Em planejamento |
| SPEC-0010 | Metrics/Telemetry | 7º (paralelo) | SPEC-0009, SPEC-0005 | Em planejamento |
| SPEC-0007 | System Parameters | 8º (paralelo) | Nenhuma | Em planejamento |
| SPEC-0001 | User Registration | 9º (paralelo) | Nenhuma | Em planejamento |

**Referência completa:** [implementation-order-summary.md](implementation-order-summary.md)

---

## 🎯 Roteiro de Implementação

### Fase 1: Fundação de Observabilidade (Semanas 1-2)
- ✅ **SPEC-0009:** Structured Logging
- ✅ **SPEC-0003:** Problem Details

**Entregável:** Logs estruturados + formato de erro padronizado

---

### Fase 2: Tratamento de Erros (Semanas 3-4)
- ✅ **SPEC-0005:** Error Handling Middleware
- ✅ **SPEC-0002:** CORS Support (paralelo)
- ✅ **SPEC-0004:** Health Checks (paralelo)

**Entregável:** Sistema robusto de tratamento de erros + CORS + Health checks

---

### Fase 3: Segurança e Métricas (Semanas 5-6)
- ✅ **SPEC-0008:** Rate Limiting
- ✅ **SPEC-0010:** Metrics (paralelo)

**Entregável:** Rate limiting ativo + telemetria completa

---

### Fase 4: Funcionalidades (Semanas 7-8)
- ✅ **SPEC-0007:** System Parameters (paralelo)
- ✅ **SPEC-0001:** User Registration (paralelo)

**Entregável:** Parâmetros de sistema + formalização de registro

---

## 🔗 Dependências entre Documentos

```
implementation-order-specs-1-10.md (Análise Detalhada)
         │
         ├──> implementation-order-summary.md (Resumo Executivo)
         │
         ├──> implementation-roadmap-specs-1-10.md (Timeline/Sprints)
         │
         └──> dependencies-matrix-specs-1-10.md (Matriz Técnica)

feature-flags.md + rbac-overview.md
         │
         └──> feature-flags-rbac-migration.md (Migração)

SPEC-0006-validation-report.md ──> feature-flags.md
SPEC-0011-validation-report.md ──> rbac-overview.md

persistence.md ──> Todos os guias (base de dados)
```

---

## 📊 Métricas de Documentação

| Métrica | Valor | Target | Status |
|---------|-------|--------|--------|
| Guias Técnicos | 4 | - | ✅ |
| Relatórios de Validação | 2 | - | ✅ |
| Guias de Planejamento | 4 | - | ✅ |
| Cobertura de SPECs Implementadas | 100% | 100% | ✅ |
| Cobertura de SPECs Planejadas | 100% | 100% | ✅ |

---

## 🛠️ Ferramentas e Convenções

### Estrutura de Documentação

```
docs/
├── README.md (este arquivo)
├── [feature-name].md (guias técnicos)
├── SPEC-[XXXX]-validation-report.md (validações)
├── implementation-*.md (planejamento)
└── dependencies-*.md (análises técnicas)
```

### Convenções de Nomenclatura

- **Guias Técnicos:** `kebab-case.md` (ex: `feature-flags.md`)
- **Relatórios:** `SPEC-XXXX-validation-report.md`
- **Planejamento:** `implementation-[tipo]-specs-[range].md`
- **Análises:** `[tipo]-matrix-specs-[range].md`

---

## 📝 Como Contribuir com Documentação

### Criando um Novo Guia Técnico

1. Use template apropriado de `templates/`
2. Siga estrutura de seções consistente:
   - Objetivo
   - Arquitetura
   - Uso (exemplos)
   - Configuração
   - Troubleshooting
   - Referências

3. Adicione ao índice deste README
4. Crie PR com label `documentation`

### Atualizando Guias Existentes

1. Mantenha versionamento no header
2. Adicione entrada no changelog do documento
3. Atualize data de "Última Revisão"
4. Revise links quebrados

### Criando Relatórios de Validação

1. Use template `templates/templates_validation_report.md`
2. Inclua evidências (código, testes, configs)
3. Marque todos os checklists
4. Adicione métricas de qualidade

---

## 🔍 Buscando Informação

### Por Funcionalidade

- **Feature Flags?** → [feature-flags.md](feature-flags.md)
- **Permissões/RBAC?** → [rbac-overview.md](rbac-overview.md)
- **Banco de Dados?** → [persistence.md](persistence.md)
- **Ordem de Implementação?** → [implementation-order-summary.md](implementation-order-summary.md)

### Por SPEC

- **SPEC Implementada?** → Veja seção "SPECs Implementadas"
- **SPEC Planejada?** → Veja seção "SPECs Planejadas" + [implementation-order-summary.md](implementation-order-summary.md)
- **Dependências de SPEC?** → [dependencies-matrix-specs-1-10.md](dependencies-matrix-specs-1-10.md)

### Por Fase de Desenvolvimento

- **Planejamento?** → Documentos `implementation-*`
- **Desenvolvimento?** → Guias técnicos (`feature-flags.md`, etc.)
- **Validação?** → Relatórios `SPEC-*-validation-report.md`

---

## 🆘 Troubleshooting

### Documentação Desatualizada

Se encontrar documentação desatualizada:

1. Abra issue com label `documentation` + `outdated`
2. Inclua link do documento e seção problemática
3. Sugira correção se possível

### Falta de Documentação

Se uma funcionalidade não tem guia:

1. Verifique se há SPEC correspondente em `specs/`
2. Se SPEC existe, crie guia usando template apropriado
3. Se SPEC não existe, crie issue solicitando SPEC primeiro

### Links Quebrados

Execute periodicamente:

```bash
# Verificar links quebrados (exemplo com markdownlint)
markdownlint-cli2 "docs/**/*.md"
```

---

## 📚 Recursos Adicionais

### Externos

- **ASP.NET Core Docs:** https://learn.microsoft.com/aspnet/core
- **EF Core Docs:** https://learn.microsoft.com/ef/core
- **Serilog Wiki:** https://github.com/serilog/serilog/wiki
- **RFC 7807 (Problem Details):** https://www.rfc-editor.org/rfc/rfc7807

### Internos

- **Repositório de SPECs:** `specs/`
- **Templates:** `templates/`
- **Requisitos:** `requisitos/`
- **API HTTP Examples:** `Vanq.API/Vanq.API.http`

---

## 📞 Contato

**Mantenedor da Documentação:** Tech Lead  
**Issues:** [GitHub Issues](https://github.com/nuno-simao/Vanq.Backend/issues)  
**Discussões:** [GitHub Discussions](https://github.com/nuno-simao/Vanq.Backend/discussions)

---

## 📄 Licença

Este projeto e sua documentação estão licenciados sob [especificar licença].

---

**Mantido por:** GitHub Copilot + Time de Desenvolvimento  
**Última Revisão:** 2025-10-01  
**Versão:** 1.0
