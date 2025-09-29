# Templates de Especificação

Este diretório contém templates padronizados para criação de especificações e decisões arquiteturais do projeto Vanq.  
Objetivo: fornecer insumos claros, consistentes e “Copilot-friendly”.

## 1. Arquivos Disponíveis

| Arquivo | Tipo | Finalidade |
|---------|------|-----------|
| spec-feature.md | Template | Especificação de nova funcionalidade de negócio |
| spec-api.md | Template | Definição ou evolução de endpoints/API |
| spec-infra.md | Template | Alterações de infraestrutura / componentes técnicos |
| spec-refactor.md | Template | Refatorações (sem mudança de comportamento externo) |
| spec-small.md | Template | Mudanças pequenas / rápidas |
| adr-template.md | Template | Arquivo de registro de decisão arquitetural (ADR) |
| README.md | Guia | Este guia |

## 2. Numeração GLOBAL

Você escolheu numeração global única para todas as specs.  
Formato de ID global: `SPEC-0001`, `SPEC-0002`, … (incremental, zero padding de 4 dígitos sugerido).

Nome de arquivo recomendado:
`SPEC-0001-FEAT-user-registration.md`  
`SPEC-0002-API-auth-session-endpoint.md`  
`SPEC-0003-INFRA-caching-layer.md`  
`SPEC-0004-REFACT-auth-service-split.md`

Regras:
- O prefixo do arquivo começa SEMPRE com o ID global.
- O campo `spec.id` no front matter deve ser idêntico ao ID (ex.: `SPEC-0003`).
- O sufixo slug (kebab-case) ajuda a leitura, mas não faz parte do ID.
- O tipo (FEAT, API, INFRA, REFACT) diferencia a natureza, mas não reseta a sequência.

## 3. Estrutura de Diretórios Recomendada

```
templates/
  (apenas os modelos .md)
specs/
  SPEC-0001-FEAT-...
  SPEC-0002-API-...
  SPEC-0003-INFRA-...
  ...
decisions/
  ADR-0001-initial-architecture.md
  ADR-0002-database-choice.md
```

(Se preferir, pode colocar ADRs dentro de `decisions/` ou `adr/`.)

## 4. Convenções de Identificadores

| Prefixo | Tipo | Exemplo |
|---------|------|---------|
| SPEC- | Especificação | SPEC-0007 |
| REQ- | Requisito funcional | REQ-03 |
| NFR- | Requisito não funcional | NFR-01 |
| BR- | Regra de negócio | BR-02 |
| ENT- | Entidade nova | ENT-01 |
| API- | Endpoint | API-01 |
| ERR- | Código de erro | ERR-04 |
| TASK- | Tarefa de implementação | TASK-07 |
| TEST- | Caso de teste | TEST-05 |
| DEC- | Decisão (inline na spec) | DEC-02 |
| QST- | Questão em aberto | QST-01 |
| FLAG- | Feature flag | FLAG-01 |
| RISK- | Risco | RISK-01 |
| ADR- | Arquivo de decisão arquitetural formal | ADR-0003 |

IDs internos (REQ-01, etc.) podem reiniciar por spec. Se precisar referenciar globalmente entre specs, cite junto do spec (ex.: SPEC-0004 / REQ-02).

## 5. Quando Criar uma ADR

Use `adr-template.md` quando:
- A decisão envolve tecnologia, contrato, padrão ou mudança irreversível relevante.
- Há várias opções com trade-offs significativos.
- Impacta múltiplas specs ou o futuro da arquitetura.

Não use ADR para:
- Pequenos detalhes de implementação local.
- Decisões triviais e facilmente revertidas.

Fluxo sugerido:
1. Criar arquivo: `decisions/ADR-0001-storage-choice.md`
2. Preencher status como `proposed`.
3. Revisar PR (ou revisão informal).
4. Ao aceitar, mudar para `accepted`.
5. Se substituída, atualizar `status: superseded` e referenciar nova em `superseded_by`.

## 6. Ciclo de Vida de uma Spec

Status: `draft` → `reviewing` → `approved` → (eventual) `deprecated`.  
Atualizações relevantes incrementam versão semântica (0.1.0 → 0.2.0 para adições compatíveis, 1.0.0 quando consolidada).

## 7. Conteúdo Mínimo (spec-small)

Obrigatório:
- Objetivo
- Escopo (In / Out / Não Fazer)
- Requisitos (REQ-*)
- Tarefas
- Critérios de Aceite
- Prompt Copilot

Opcional:
- NFR
- Entidades novas
- API endpoints
- i18n / Flags
- Decisões / Pendências

## 8. Diretrizes para Escrever

Faça:
- Requisitos objetivos e testáveis.
- Evite dependências implícitas: declare ordem em tarefas.
- Use frases claras e curtas.

Evite:
- Termos vagos: “rápido”, “seguro” sem métrica.
- Reutilizar IDs (IDs não devem ser reciclados).
- Seções vazias sem justificativa (melhor remover do que deixar “???”, exceto placeholders deliberados).

## 9. i18n e Feature Flags

Se i18n = Sim:
- Listar chaves e contexto (ex.: mensagens de erro).
- Definir fallback (pt-BR → en-US).

Feature Flags:
| Campo | Descrição |
|-------|-----------|
| Nome | kebab-case |
| Escopo | API / Infra / Feature |
| Estratégia | Gradual / Big-bang / Kill-switch |
| Fallback | Comportamento ao desligar |

## 10. Integração com Copilot

Bloco “Prompt Copilot” deve:
- Referenciar SPEC-ID e REQs explícitos.
- Conter somente o necessário (evitar transbordar todo documento).
- Indicar limites (não criar entidades além das listadas, etc.).

Exemplo Mini Prompt:
```
Copilot: Implementar SPEC-0012 (REQ-01..REQ-03).
Criar ENT-01 Customer.
Endpoints: API-01 GET /v1/customers/{id}.
NFR: p95 < 120ms.
Não adicionar libs externas.
```

## 11. ADR vs DEC

| Aspecto | DEC (inline na spec) | ADR (arquivo) |
|---------|----------------------|---------------|
| Escopo | Local àquela spec | Transversal ou estratégico |
| Formalidade | Leve | Completa (contexto, opções, trade-offs) |
| Evolução | Pode ser removida se irrelevante | Mantida historicamente (mesmo supersedida) |
| Referências | Citada apenas na spec | Pode referenciar várias specs |

Se uma DEC local crescer em impacto, promova a uma ADR e referencie.

## 12. Exemplo de Ligação Spec ↔ ADR

Em `spec-feature.md`:
```
Decisões:
| ID | Contexto | Decisão | Alternativas | Consequência |
| DEC-02 | Armazenamento tokens | Ver ADR-0003 | X,Y | Maior robustez |
```

Em `ADR-0003`:
```
spec_refs: [SPEC-0005, SPEC-0007]
```

## 13. Checklist Geral Antes de Aprovar Spec

- [ ] ID global correto (formato SPEC-XXXX)
- [ ] Objetivo + Escopo claro
- [ ] REQs numerados e claros
- [ ] NFRs só se relevantes e mensuráveis
- [ ] Entidades novas (se houver) descritas
- [ ] Endpoints (se API)
- [ ] Flags / i18n avaliados
- [ ] Tarefas executáveis
- [ ] Decisões registradas (ou ADR criada)
- [ ] Prompt Copilot consistente
- [ ] Sem ambiguidade óbvia / termos vagos

## 14. Evoluções Futuras (Sugestões)

- Script para gerar próximo ID (ex.: `spec next`).
- GitHub Action validando front matter (YAML).
- Dashboard agregando REQ ↔ TEST (coverage lógica).
- Repositório de métricas para comparar NFRs declarados vs reais.

## 15. FAQ

1. Posso ter múltiplas specs ativas para o mesmo domínio?  
   Sim, desde que independentes e sem conflito de escopo.

2. Quando arquivar uma spec?  
   Quando entregue e estabilizada — mova para `specs/archived/` ou marque `status: deprecated`.

3. Reutilizo REQ-IDs entre specs?  
   Sim, REQ-IDs são locais à spec (use referência cruzada se necessário).

4. ADR precisa de versão?  
   Normalmente não. Use status + histórico; se muito alterada, substitua via nova ADR e marque superseded.

---

Em caso de dúvida: comece simples com spec-small e evolua.

Fim.