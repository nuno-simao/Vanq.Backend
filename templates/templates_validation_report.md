# SPEC-{XXXX} - Relatório de Validação de Conformidade

**Data:** {YYYY-MM-DD}  
**Revisor:** {Nome do Revisor / GitHub Copilot}  
**Spec:** SPEC-{XXXX}-{CATEGORIA}-{nome-da-feature} (approved)  
**Status Geral:** {✅ CONFORME / ⚠️ CONFORME COM RESSALVAS / ❌ NÃO CONFORME}  
**Versão:** v{X.Y}

---

## 📊 Resumo Executivo

{Breve descrição do resultado geral da validação - 2-3 parágrafos}

A implementação do {NOME_DA_FEATURE} está **[STATUS]** ao SPEC-{XXXX}, com {XX}% de aderência. As principais funcionalidades estão implementadas corretamente, incluindo:

- ✅ {Lista dos principais componentes implementados}
- ✅ {Outra funcionalidade chave}
- ✅ {Mais uma funcionalidade chave}
- {Opcional} ⚠️ {Ressalvas identificadas}

**Divergências críticas identificadas:** {Listar divergências ou "Nenhuma"}

### {Opcional} X.1 Principais Entregas

- ✅ **{Categoria 1}:** {Descrição breve}
- ✅ **{Categoria 2}:** {Descrição breve}
- ✅ **{Categoria 3}:** {Descrição breve}
- ✅ **Testes:** {X testes / Y% cobertura}
- ✅ **Documentação:** {Guias criados}

---

## ✅ Validações Positivas

### 1. **{Categoria Principal} ({ID-XX} a {ID-YY})** ✅ CONFORME

{Se aplicável, incluir tabela de endpoints/entidades/features}

| ID | Nome/Endpoint | Implementado | Detalhes | Status |
|----|---------------|--------------|----------|--------|
| {ID-01} | {Nome} | ✅ | {Detalhes} | ✅ Conforme |
| {ID-02} | {Nome} | ✅ | {Detalhes} | ✅ Conforme |
| {ID-03} | {Nome} | ✅ | {Detalhes} | ✅ Conforme |

**Nota:** {Observações adicionais sobre organização ou decisões técnicas}

---

### 2. **{Categoria Secundária} ({ID-XX} a {ID-YY})** ✅ CONFORME

#### **{ID-01}: {Nome do Item}** ✅
```{linguagem}
// Código exemplo demonstrando implementação
public class Example
{
    public Guid Id { get; private set; }              // ✅ SPEC: Comentário de validação
    public string Name { get; private set; }          // ✅ SPEC: Outro comentário
    public DateTimeOffset CreatedAt { get; private set; } // ✅ SPEC: Timestamp
}
```
**Validações:** {Regex, regras de negócio, etc.}  
✅ Conforme {BR-XX}

---

#### **{ID-02}: {Nome do Item}** ✅
```{linguagem}
// Outro exemplo de implementação
```
**Validações:** {Descrição das validações aplicadas}

---

### 3. **Requisitos Funcionais** ✅ CONFORME

#### **REQ-01: {Título do Requisito}**
**Criticidade:** {MUST / SHOULD / COULD}  
**Status:** ✅ **CONFORME**

**Evidências:**
- **Arquivo:** `{Caminho/do/arquivo.cs}`
- **Implementação:** {Descrição breve}
- **Detalhes Técnicos:** {Informações relevantes}

**Validação Técnica:**
```{linguagem}
// Código chave demonstrando conformidade
public async Task<Result> Method()
{
    // Implementação conforme spec
}
```

**Testes Relacionados:**
- `{Nome_do_Teste_1}`
- `{Nome_do_Teste_2}`
- `{Nome_do_Teste_3}`

---

#### **REQ-02: {Título do Requisito}**
**Criticidade:** {MUST / SHOULD / COULD}  
**Status:** ✅ **CONFORME**

**Evidências:**
- **Interface:** `{Caminho/Interface.cs}`
- **Implementação:** `{Caminho/Implementacao.cs}`
- **Padrão Utilizado:** {Descrição do padrão/abordagem}

**Código Chave:**
```{linguagem}
// Trecho relevante mostrando implementação
```

**Testes Relacionados:**
- `{Nome_do_Teste}`

---

### 4. **Requisitos Não-Funcionais** ✅ CONFORME

#### **NFR-01: {Título} - {Descrição breve}**
**Categoria:** {Performance / Confiabilidade / Segurança / Observabilidade}  
**Status:** ✅ **CONFORME**

**Evidências:**
- **Métrica:** {Descrição da métrica atingida}
- **Implementação:** {Como foi atendido}
- **Validação:** {Como foi testado}

**Validação Técnica:**
```{linguagem}
// Código demonstrando atendimento ao NFR
```

**Nota:** {Observações adicionais ou benchmarks}

---

### 5. **Regras de Negócio** ✅ CONFORME

| ID | Regra | Implementação | Status |
|----|-------|---------------|--------|
| BR-01 | {Descrição da regra} | ✅ Validação em `{Arquivo.Method()}` | ✅ Conforme |
| BR-02 | {Descrição da regra} | ✅ {Onde está implementado} | ✅ Conforme |
| BR-03 | {Descrição da regra} | ✅ {Onde está implementado} | ✅ Conforme |

---

### 6. **Decisões Técnicas ({DEC-XX} a {DEC-YY})** ✅ CONFORME

| ID | Decisão | Implementação | Evidência |
|----|---------|---------------|-----------|
| DEC-01 | {Descrição da decisão técnica} | ✅ | `{Arquivo.cs}` + `{ConfigFile.cs}` |
| DEC-02 | {Descrição da decisão técnica} | ✅ | {Evidência ou método específico} |
| DEC-03 | {Descrição da decisão técnica} | ✅ | {Evidência ou abordagem usada} |

---

## {Opcional} ✅ Migrações/Integrações Concluídas

### 1. **{Título da Migração/Integração}** ✅ {STATUS}

**Status:** ✅ **{DESCRIÇÃO DO STATUS}**  
{Descrição do que foi migrado/integrado e contexto}

**Data de Conclusão:** {YYYY-MM-DD}  
**Versão:** v{X.Y}

**Evidência da Migração:**
```{linguagem}
// Código mostrando estado após migração
```

**Arquitetura Atual:**
```
[Componente A] → [Componente B] ✅
                       ↓
                  [Componente C] → [Persistência]
```

**Fases Concluídas:**
- ✅ **Fase 1 (v{X.Y}):** {Descrição}
- ✅ **Fase 2 (v{X.Y}):** {Descrição}
- {Opcional} ✅ **Fase 3 (v{X.Y}):** {Descrição}

**Validações:**
- ✅ {Validação 1}
- ✅ {Validação 2}
- ✅ {Validação 3}

---

## ⚠️ Divergências Identificadas

### 1. **{Título da Divergência}** {🔴 CRÍTICO / 🟡 MODERADO / 🟢 MENOR}

**Problema:**  
{Descrição clara do problema identificado}

**Localização:**
```markdown
Linha {XX} de `{arquivo}`:
{Trecho mostrando o problema}
```

**Deveria ser:**
```markdown
{Trecho correto conforme spec}
```

**Impacto:** {Descrição do impacto - ex: "Confusão para desenvolvedores", "Breaking change potencial"}

**Recomendação:** {Ação sugerida para correção}

---

### 2. **{Outra Divergência}** {🔴 CRÍTICO / 🟡 MODERADO / 🟢 MENOR}

{Mesmo formato da divergência anterior}

---

## 📋 Checklist de Conformidade

### Requisitos Funcionais
- [ ] REQ-01: {Descrição} ✅
- [ ] REQ-02: {Descrição} ✅
- [ ] REQ-03: {Descrição} ⚠️
- [ ] REQ-04: {Descrição} ❌

### Requisitos Não Funcionais
- [ ] NFR-01: {Descrição} ✅
- [ ] NFR-02: {Descrição} ✅
- [ ] NFR-03: {Descrição} ✅

### Entidades
- [ ] ENT-01: {Nome} ✅
- [ ] ENT-02: {Nome} ✅

### API Endpoints
- [ ] API-01 a API-10 ({X}/{Y}) ✅

### Regras de Negócio
- [ ] BR-01 a BR-04 ({X}/{Y}) ✅

### Decisões
- [ ] DEC-01: {Descrição} ✅
- [ ] DEC-02: {Descrição} ✅

### Testes
- [ ] Cobertura de Testes: {XX}% ✅
- [ ] Testes Unitários: {X}/{Y} passing ✅
- [ ] Testes de Integração: {X}/{Y} passing ✅

---

## 🔧 Recomendações de Ação

### **Prioridade ALTA** 🔴
1. **{Título da Ação Crítica}**
   - {Descrição do que precisa ser feito}
   - {Justificativa da urgência}
   - {Etapas sugeridas}

### **Prioridade MÉDIA** 🟡
2. **{Título da Ação Moderada}**
   - {Descrição do que precisa ser feito}
   - {Justificativa}
   - {Etapas sugeridas}

### **Prioridade BAIXA** 🟢
3. **{Título da Ação Opcional}**
   - {Descrição do que pode ser melhorado}
   - {Benefícios esperados}

### **{Opcional} CONCLUÍDO** ✅
~~4. **{Título da Ação Já Realizada}**~~
   - ✅ {O que foi feito}
   - ✅ {Resultado alcançado}
   - ✅ {Validação}

---

## 📊 {Opcional} Métricas de Qualidade

| Métrica | Valor | Target | Status |
|---------|-------|--------|--------|
| Cobertura de Testes | {XX}% | ≥80% | ✅ |
| Complexidade Ciclomática (Média) | {X.Y} | <10 | ✅ |
| Dívida Técnica (Estimada) | {X}h | <8h | ⚠️ |
| Conformidade com SPEC | {XX}% | 100% | ✅ |
| Warnings de Compilação | {X} | 0 | ✅ |

---

## ✅ Conclusão

**A implementação do {NOME_DA_FEATURE} está {STATUS}:**

1. ✅ **Funcionalidade:** {XX}% conforme
2. ✅ **Arquitetura:** {XX}% conforme
3. {⚠️ / ✅} **Documentação:** {Status e observações}
4. {⚠️ / ✅} **Testes:** {Status e observações}

**{Há / Não há} blockers para uso em produção.** {Contexto adicional sobre prontidão}

{Opcional: Listar próximos passos ou melhorias futuras}

---

## 📝 Histórico de Revisões

| Versão | Data | Autor | Mudanças |
|--------|------|-------|----------|
| v1.0 | {YYYY-MM-DD} | {Autor} | Relatório inicial |
| v1.1 | {YYYY-MM-DD} | {Autor} | {Descrição da atualização} |

---

**Assinado por:** {Nome do Revisor}  
**Data:** {YYYY-MM-DD}  
{Opcional: **Referência SPEC:** SPEC-{XXXX} v{X.Y}}  
**Versão do Relatório:** v{X.Y}  
**Status:** {Produção-Ready / Em Desenvolvimento / Bloqueado}

---

## 📚 Referências

- **SPEC Principal:** [`specs/SPEC-{XXXX}-{nome}.md`](../specs/SPEC-{XXXX}-{nome}.md)
- {Opcional} **SPECs Relacionadas:** SPEC-{YYYY}, SPEC-{ZZZZ}
- {Opcional} **Documentação Técnica:** [`docs/{documento}.md`](../{documento}.md)
- {Opcional} **ADRs Relacionadas:** ADR-{XXX}
- {Opcional} **Issues/PRs:** #{número}

---

## 💡 Notas de Uso deste Template

### Instruções para Preenchimento:

1. **Substituir placeholders:** Todos os itens entre `{chaves}` devem ser substituídos
2. **Remover seções opcionais:** Marque seções com `{Opcional}` - remova se não aplicável
3. **Adaptar checkboxes:** Use `[x]` para marcados, `[ ]` para não marcados
4. **Emojis de status:**
   - ✅ Conforme / Completo
   - ⚠️ Conforme com ressalvas / Atenção
   - ❌ Não conforme / Blocker
   - 🔴 Prioridade Alta / Crítico
   - 🟡 Prioridade Média / Moderado
   - 🟢 Prioridade Baixa / Menor

5. **Seções por tipo de SPEC:**
   - **Feature:** Enfatizar REQ-XX, BR-XX, API-XX
   - **Infra:** Enfatizar NFR-XX, DEC-XX, métricas
   - **Refactor:** Enfatizar migrações, before/after, validações

6. **Código de exemplo:** Use blocos de código com linguagem apropriada (`csharp`, `sql`, `json`, etc.)

7. **Tabelas:** Manter formatação Markdown consistente, alinhar colunas visualmente

8. **Links:** Usar caminhos relativos para referenciar documentos do repositório

### Seções Obrigatórias:
- Resumo Executivo
- Validações Positivas (pelo menos 1 categoria)
- Checklist de Conformidade
- Conclusão

### Seções Opcionais (remover se não aplicável):
- Migrações/Integrações Concluídas
- Divergências Identificadas (apenas se houver)
- Métricas de Qualidade
- Histórico de Revisões (se for primeira versão)

### Critérios de Status Geral:
- **✅ CONFORME:** 100% de aderência, zero blockers
- **⚠️ CONFORME COM RESSALVAS:** ≥90% de aderência, divergências documentadas mas não bloqueantes
- **❌ NÃO CONFORME:** <90% de aderência, ou presença de blockers críticos

---

**Template Version:** 1.0  
**Última Atualização:** 2025-10-01  
**Mantido por:** GitHub Copilot  
**Baseado em:** SPEC-0006 e SPEC-0011 validation reports
