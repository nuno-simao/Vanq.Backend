# Instruções de Uso do GitHub Copilot – Vanq.Backend

> Documento de orientação para maximizar a utilidade do GitHub Copilot neste repositório backend (.NET / C#), refletindo o estado atual do projeto e os próximos passos planejados.
> Atualize sempre que arquitetura, padrões, dependências ou fluxos mudarem.  
> Objetivo: aumentar produtividade sem comprometer consistência, performance e manutenibilidade.

---
## 1. Visão Geral do Projeto
- Nome: Vanq Backend
- Estado atual: solução em estágio inicial baseada no template Minimal API do .NET, com exemplo `WeatherForecast` e documentação interativa via Scalar.
- Objetivo 2025: evoluir para backend modular que suporte produtos Vanq, mantendo simplicidade enquanto as camadas de domínio e infraestrutura ainda não existem.
- Stakeholders principais: Equipe de engenharia, produto e QA (definir pessoas conforme onboarding).
- Critérios de sucesso (curto prazo):
  - Build rodando e deployável.
  - Documentação viva (OpenAPI + instruções internas).
  - Facilidade para adicionar novos módulos sem retrabalho grande.

## 2. Stack Tecnológica Atual
| Camada | Tecnologia / Framework | Versão | Observações |
|--------|------------------------|--------|-------------|
| Backend (API) | ASP.NET Core Minimal API | net10.0 | Projeto `Vanq.API`; expõe `GET /weatherforecast` e redirect para documentações. |
| Documentação | Scalar.AspNetCore | 2.8.7 | UI em `/scalar`; `app.MapOpenApi()` habilitado. |
| Configuração | `appsettings*.json` | - | Connection string padrão para PostgreSQL local (`postgres/postgres`), ainda não utilizada pelo código. |

### Dependências em uso
- `Microsoft.AspNetCore.OpenApi` `10.0.0-rc.1.25451.107`
- `Scalar.AspNetCore` `2.8.7`

### Stack planejada (validar antes de implementar)
- EF Core + PostgreSQL para persistência.
- Mensageria com RabbitMQ (provável) usando padrão Outbox.
- Redis para cache/tokens.
- Observabilidade completa (Serilog, OpenTelemetry, Prometheus/Grafana).
- Autenticação via OAuth2/JWT.

## 3. Estrutura do Repositório
```
Vanq.Backend.slnx
├─ Vanq.API/             # Host Minimal API + configuração OpenAPI/Scalar
├─ Vanq.Application/     # Placeholder para casos de uso (sem código ainda)
├─ Vanq.Domain/          # Placeholder para entidades/Value Objects (vazio)
├─ Vanq.Infrastructure/  # Placeholder para adapters/persistência (vazio)
└─ Vanq.Shared/          # Placeholder para utilidades compartilhadas (vazio)
```
Notas:
- Ainda não há pasta `tests/` ou projetos de teste criados.
- `Program.cs` concentra pipeline, serviços e endpoints.

## 4. Direção Arquitetural
- Objetivo: evoluir para Clean Architecture + DDD modular conforme requisitos surgirem.
- Enquanto as camadas não existem, mantenha implementações simples e isoladas no host `Vanq.API`.
- Ao introduzir funcionalidade real:
  - Coloque invariantes e regras no projeto `Vanq.Domain`.
  - Use `Vanq.Application` para orquestrar casos de uso, comandos e queries.
  - Implemente acesso a dados/integrations em `Vanq.Infrastructure`.
  - Centralize tipos compartilhados em `Vanq.Shared` (evitar dependências cruzadas desnecessárias).
- Evite dependências inversas (ex: domínio referenciando infraestrutura).

## 5. Convenções de Código
- Código e nomes em inglês; comentários de negócio críticos podem estar em pt-BR.
- Arquivos/classes em PascalCase, métodos e variáveis em camelCase.
- Nullable referência ligado (`<Nullable>enable</Nullable>`); trate avisos como obrigatórios.
- Prefira records para DTOs imutáveis e métodos fábrica estáticos para entidades.
- Limite métodos a ~25 linhas onde possível; extraia helpers para manter legibilidade.
- Configure `.editorconfig` e `dotnet format` quando o código crescer.

## 6. Configuração & Segredos
- `appsettings.json` e `appsettings.Development.json` possuem `ConnectionStrings:DefaultConnection`. Atualize conforme o que estiver realmente em uso.
- Nunca commitar segredos reais; utilize variáveis de ambiente ou arquivos ignorados.
- Documente novas configurações relevantes nesta seção ou em `docs/`.

## 7. Documentação, Observabilidade e Saúde
- OpenAPI exposta via `app.MapOpenApi()`; utilize `Vanq.API.http` para chamadas rápidas.
- Scalar disponível em `/scalar`; a rota raiz (`/`) redireciona para essa UI.
- Ainda não há logging estruturado, tracing ou métricas. Registre decisões ao implementar Serilog/OpenTelemetry.

## 8. Estratégia de Testes
- Não há testes implementados. Ao iniciar:
  - Use xUnit + FluentAssertions como padrão.
  - Estruture `tests/Unit`, `tests/Integration`, etc., conforme a complexidade crescer.
  - Configure pipeline (GitHub Actions) para executar `dotnet test`.
- Testes devem ser determinísticos e independentes de serviços externos.

## 9. Fluxo de Desenvolvimento
1. Branches: `feat/<contexto>`, `fix/<contexto>`, `chore/<contexto>`, `refactor/<contexto>`.
2. Commits semânticos (): `feat: ...`, `fix: ...`, `docs: ...`, `test: ...`.
3. Pull Requests: descreva objetivo, mudanças, como validar localmente e se há configurações extras.

## 10. Uso do GitHub Copilot
- Use para: esqueleto de endpoints, handlers, testes, scripts básicos, documentação inicial.
- Revise manualmente qualquer regra de negócio sugerida.
- Não introduza novas dependências ou padrões sem alinhamento com a equipe.
- Prefira prompts com contexto claro (módulo, objetivo, restrições).

## 11. Próximos Passos Recomendados
- Criar projetos de teste e configurar a execução automática (`dotnet test`).
- Remover o exemplo `WeatherForecast` quando existir caso de uso real.
- Definir módulos/domínios reais e mover lógica para `Vanq.Domain`/`Vanq.Application`.
- Configurar logging e observabilidade básica antes de expor endpoints públicos.
- Escrever documentação inicial em `docs/` (ex: visão arquitetural, decisões).

## 12. Referências Internas
- Ainda não há documentação auxiliar. Adicione arquivos em `docs/` e referencie-os aqui à medida que forem criados.

---

Mantenha este arquivo conciso e objetivo. Ajustes incrementais são preferíveis a reescritas totais.

Última revisão: 26/09/2025
Responsável: Nuno Francisco Simão