# Especificação [Nome da especificação]

[Descrição breve da especificação]


---

## 1. Objetivos

[Descrever os objetivos da especificação]

---

## 2. Distribuição dos Componentes por Camada

---

## 3. Estrutura de Pastas (Proposta Inicial)

[Descrever a estrutura de pastas da especificação]

```
Vanq.Domain/
  Entities/
  Constants/
  Exceptions/
Vanq.Application/
  Abstractions/
  Contracts/
  Services/
Vanq.Infrastructure/
  Persistence/
    Configurations/
  Auth/
    Jwt/
    Password/
    Tokens/
  DependencyInjection/
  Migrations/
Vanq.Shared/
  Results/
Vanq.API/
  Program.cs
  Endpoints/
  docs/
```

---

## 4. Entidades (Vanq.Domain)

---

## 5. Contratos (Vanq.Application/Contracts/Auth)

---

## 6. Abstrações (Vanq.Application/Abstractions)

---

## 7. Infra - JwtOptions e Token Service

---

## 8. Infra - Password Hasher

---

## 9. Infra - RefreshTokenFactory + Service

---

## 10. Infra - DbContext e Configurações

---

## 11. Extensions de DI (Vanq.Infrastructure/DependencyInjection)

---

## 12. API - Program.cs (Vanq.API)

---

## 13. API - Endpoints (Vanq.API/Endpoints/AuthEndpoints.cs)

---

## 14. Configuração appsettings.json (Vanq.API)

---

## 15. Migrações

---

## 16. Fluxos (Resumo Alinhado às Camadas)

---

## 17. Segurança

---

## 18. Checklist Atualizado

---

## 19. Próximos Incrementos Sugeridos

---

## 20. Diferenças vs Especificação Original

---

## 21. Ajuste no Refresh para Retornar Novo Refresh Token

[Descrever]

---

## 22. Testes (Escopo Inicial)

| Teste | Objetivo |
|-------|----------|

---

## 23. Pacotes (Resumo por Projeto)

| Projeto | Pacotes |
|---------|---------|
| Vanq.Domain | [Descrever] |
| Vanq.Application | [Descrever] |
| Vanq.Infrastructure | [Exemplo: Microsoft.EntityFrameworkCore, Npgsql.EntityFrameworkCore.PostgreSQL, Microsoft.EntityFrameworkCore.Design, BCrypt.Net-Next] |
| Vanq.API | [Exemplo: Microsoft.AspNetCore.Authentication.JwtBearer, Scalar.AspNetCore, Swashbuckle/OpenAPI built-in minimal (AddOpenApi), (referências aos outros projetos)] |
| Vanq.Shared | [Descrever] |

---

## 24. Observações de Deploy

---

Fim.