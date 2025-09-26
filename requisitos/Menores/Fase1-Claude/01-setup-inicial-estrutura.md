# Parte 1: Setup Inicial e Estrutura

## 📋 Visão Geral
**Duração**: 15-20 minutos  
**Complexidade**: Baixa  
**Dependências**: Nenhuma (primeira parte)

Esta parte estabelece a fundação do projeto com Clean Architecture, criando toda a estrutura de pastas e arquivos de projeto necessários.

## 🎯 Objetivos
- ✅ Criar estrutura completa de pastas seguindo Clean Architecture
- ✅ Configurar todos os arquivos .csproj com dependências corretas
- ✅ Criar solution file
- ✅ Estabelecer namespaces consistentes

## 📁 Estrutura a ser Criada

```
IDE.Backend/
├── src/
│   ├── IDE.API/                    # Minimal API, endpoints e startup
│   ├── IDE.Application/            # Casos de uso, DTOs e Interfaces
│   ├── IDE.Domain/                 # Entidades, Value Objects e regras
│   ├── IDE.Infrastructure/         # Persistência e serviços externos
│   └── IDE.Shared/                 # Utilitários, extensões e constantes
├── tests/
│   ├── IDE.UnitTests/              # Testes unitários rápidos
│   ├── IDE.IntegrationTests/       # Testes de integração API
│   └── IDE.ArchitectureTests/      # Validação de arquitetura
├── docs/                           # Documentação completa
├── scripts/                        # Scripts de database e deploy
└── IDE.Backend.sln                 # Solution file
```

## 🚀 Execução Passo a Passo

### 1. Criar Estrutura de Diretórios

Execute no terminal PowerShell na raiz do projeto:

```powershell
# Criar estrutura src/
mkdir src\IDE.API, src\IDE.Application, src\IDE.Domain, src\IDE.Infrastructure, src\IDE.Shared

# Criar estrutura tests/
mkdir tests\IDE.UnitTests, tests\IDE.IntegrationTests, tests\IDE.ArchitectureTests

# Criar diretórios auxiliares
mkdir docs, scripts

# Criar subdiretórios dentro de cada projeto
mkdir src\IDE.API\Endpoints, src\IDE.API\Middleware, src\IDE.API\Configuration
mkdir src\IDE.Application\Auth, src\IDE.Application\Email, src\IDE.Application\Security, src\IDE.Application\Common, src\IDE.Application\Interfaces
mkdir src\IDE.Domain\Entities, src\IDE.Domain\Enums, src\IDE.Domain\Events, src\IDE.Domain\ValueObjects
mkdir src\IDE.Infrastructure\Data, src\IDE.Infrastructure\Auth, src\IDE.Infrastructure\Email, src\IDE.Infrastructure\Caching, src\IDE.Infrastructure\Monitoring, src\IDE.Infrastructure\Extensions
mkdir src\IDE.Shared\Common, src\IDE.Shared\Extensions, src\IDE.Shared\Constants, src\IDE.Shared\Configuration
```

### 2. Criar Solution File

```powershell
# Na raiz do projeto
dotnet new sln -n IDE.Backend
```

### 3. Criar Projetos

#### 3.1 IDE.Domain (Class Library)
```powershell
cd src\IDE.Domain
dotnet new classlib -n IDE.Domain --framework net8.0
cd ..\..
```

**IDE.Domain.csproj**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="System.ComponentModel.Annotations" Version="5.0.0" />
  </ItemGroup>
</Project>
```

#### 3.2 IDE.Shared (Class Library)
```powershell
cd src\IDE.Shared
dotnet new classlib -n IDE.Shared --framework net8.0
cd ..\..
```

**IDE.Shared.csproj**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="System.Text.Json" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="8.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\IDE.Domain\IDE.Domain.csproj" />
  </ItemGroup>
</Project>
```

#### 3.3 IDE.Application (Class Library)
```powershell
cd src\IDE.Application
dotnet new classlib -n IDE.Application --framework net8.0
cd ..\..
```

**IDE.Application.csproj**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="AutoMapper" Version="12.0.1" />
    <PackageReference Include="FluentValidation" Version="11.8.0" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\IDE.Domain\IDE.Domain.csproj" />
    <ProjectReference Include="..\IDE.Shared\IDE.Shared.csproj" />
  </ItemGroup>
</Project>
```

#### 3.4 IDE.Infrastructure (Class Library)
```powershell
cd src\IDE.Infrastructure
dotnet new classlib -n IDE.Infrastructure --framework net8.0
cd ..\..
```

**IDE.Infrastructure.csproj**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.0" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="8.0.0" />
    <PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />
    <PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="7.0.3" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.0" />
    <PackageReference Include="StackExchange.Redis" Version="2.7.4" />
    <PackageReference Include="SendGrid" Version="9.29.1" />
    <PackageReference Include="MailKit" Version="4.3.0" />
    <PackageReference Include="MimeKit" Version="4.3.0" />
    <PackageReference Include="OtpNet" Version="1.9.0" />
    <PackageReference Include="Serilog.Extensions.Hosting" Version="8.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\IDE.Application\IDE.Application.csproj" />
    <ProjectReference Include="..\IDE.Domain\IDE.Domain.csproj" />
    <ProjectReference Include="..\IDE.Shared\IDE.Shared.csproj" />
  </ItemGroup>
</Project>
```

#### 3.5 IDE.API (Web API)
```powershell
cd src\IDE.API
dotnet new web -n IDE.API --framework net8.0
cd ..\..
```

**IDE.API.csproj**:
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="8.0.0" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.0" />
    <PackageReference Include="Swashbuckle.AspNetCore.Annotations" Version="6.5.0" />
    <PackageReference Include="FluentValidation.AspNetCore" Version="11.3.0" />
    <PackageReference Include="AutoMapper.Extensions.Microsoft.DependencyInjection" Version="12.0.1" />
    <PackageReference Include="Serilog.AspNetCore" Version="8.0.0" />
    <PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
    <PackageReference Include="Serilog.Sinks.Console" Version="5.0.1" />
    <PackageReference Include="Microsoft.Extensions.Caching.StackExchangeRedis" Version="8.0.0" />
    <PackageReference Include="Microsoft.AspNetCore.RateLimiting" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore" Version="8.0.0" />
    <PackageReference Include="Microsoft.ApplicationInsights.AspNetCore" Version="2.21.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\IDE.Application\IDE.Application.csproj" />
    <ProjectReference Include="..\IDE.Infrastructure\IDE.Infrastructure.csproj" />
    <ProjectReference Include="..\IDE.Shared\IDE.Shared.csproj" />
  </ItemGroup>
</Project>
```

### 4. Criar Projetos de Teste

#### 4.1 IDE.UnitTests
```powershell
cd tests\IDE.UnitTests
dotnet new xunit -n IDE.UnitTests --framework net8.0
cd ..\..
```

**IDE.UnitTests.csproj**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.1" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
    <PackageReference Include="Moq" Version="4.20.69" />
    <PackageReference Include="FluentAssertions" Version="6.12.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="8.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\IDE.Application\IDE.Application.csproj" />
    <ProjectReference Include="..\..\src\IDE.Infrastructure\IDE.Infrastructure.csproj" />
  </ItemGroup>
</Project>
```

#### 4.2 IDE.IntegrationTests
```powershell
cd tests\IDE.IntegrationTests
dotnet new xunit -n IDE.IntegrationTests --framework net8.0
cd ..\..
```

**IDE.IntegrationTests.csproj**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.1" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.0" />
    <PackageReference Include="Testcontainers.PostgreSql" Version="3.6.0" />
    <PackageReference Include="Testcontainers.Redis" Version="3.6.0" />
    <PackageReference Include="FluentAssertions" Version="6.12.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\IDE.API\IDE.API.csproj" />
  </ItemGroup>
</Project>
```

#### 4.3 IDE.ArchitectureTests
```powershell
cd tests\IDE.ArchitectureTests
dotnet new xunit -n IDE.ArchitectureTests --framework net8.0
cd ..\..
```

**IDE.ArchitectureTests.csproj**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.1" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
    <PackageReference Include="NetArchTest.Rules" Version="1.3.2" />
    <PackageReference Include="FluentAssertions" Version="6.12.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\IDE.API\IDE.API.csproj" />
    <ProjectReference Include="..\..\src\IDE.Application\IDE.Application.csproj" />
    <ProjectReference Include="..\..\src\IDE.Domain\IDE.Domain.csproj" />
    <ProjectReference Include="..\..\src\IDE.Infrastructure\IDE.Infrastructure.csproj" />
    <ProjectReference Include="..\..\src\IDE.Shared\IDE.Shared.csproj" />
  </ItemGroup>
</Project>
```

### 5. Adicionar Projetos à Solution

```powershell
# Adicionar projetos src/
dotnet sln add src\IDE.Domain\IDE.Domain.csproj
dotnet sln add src\IDE.Shared\IDE.Shared.csproj
dotnet sln add src\IDE.Application\IDE.Application.csproj
dotnet sln add src\IDE.Infrastructure\IDE.Infrastructure.csproj
dotnet sln add src\IDE.API\IDE.API.csproj

# Adicionar projetos tests/
dotnet sln add tests\IDE.UnitTests\IDE.UnitTests.csproj
dotnet sln add tests\IDE.IntegrationTests\IDE.IntegrationTests.csproj
dotnet sln add tests\IDE.ArchitectureTests\IDE.ArchitectureTests.csproj
```

### 6. Restaurar Dependências e Validar

```powershell
# Restaurar todos os pacotes
dotnet restore

# Verificar se compila
dotnet build
```

## ✅ Critérios de Validação

Ao final desta parte, você deve ter:

- [ ] **Solution file** criado e funcionando
- [ ] **8 projetos** adicionados à solution
- [ ] **Estrutura de pastas** completa seguindo Clean Architecture
- [ ] **Compilação bem-sucedida** (`dotnet build` sem erros)
- [ ] **Dependências restauradas** (`dotnet restore` sem erros)
- [ ] **Namespaces** consistentes (IDE.Domain, IDE.Application, etc.)

## 📝 Arquivos Criados

Esta parte criará aproximadamente **15-20 arquivos**:
- 1 Solution file (.sln)
- 8 Project files (.csproj)
- Estrutura de diretórios (50+ pastas)

## 🔄 Próximos Passos

Após concluir esta parte, você estará pronto para:
- **Parte 2**: Entidades de Domínio Core
- Implementar as entidades principais (User, RefreshToken, etc.)
- Definir enums e value objects essenciais

## 🚨 Troubleshooting Comum

**Erro de compilação**: Verifique se todos os PackageReference estão corretos  
**Dependências não restauram**: Execute `dotnet restore` na raiz  
**Projetos não aparecem na solution**: Use `dotnet sln list` para verificar  

---
**⏱️ Tempo estimado**: 15-20 minutos  
**🎯 Próxima parte**: 02-entidades-dominio-core.md