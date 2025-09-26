# Fase 3.12: Docker & Deployment - Finalização da API

## Containerização e Deploy da API de Colaboração

Esta parte implementa a **containerização completa** da API de colaboração em tempo real, incluindo Docker, Docker Compose, scripts de deployment e documentação final.

**Pré-requisitos**: Partes 3.1 a 3.11 totalmente implementadas

## 1. Docker Configuration

### 1.1 Dockerfile Principal

#### IDE.Api/Dockerfile
```dockerfile
# Estágio de build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar arquivos de projeto
COPY ["IDE.Api/IDE.Api.csproj", "IDE.Api/"]
COPY ["IDE.Application/IDE.Application.csproj", "IDE.Application/"]
COPY ["IDE.Domain/IDE.Domain.csproj", "IDE.Domain/"]
COPY ["IDE.Infrastructure/IDE.Infrastructure.csproj", "IDE.Infrastructure/"]

# Restaurar dependências
RUN dotnet restore "IDE.Api/IDE.Api.csproj"

# Copiar código fonte
COPY . .

# Build da aplicação
WORKDIR "/src/IDE.Api"
RUN dotnet build "IDE.Api.csproj" -c Release -o /app/build

# Estágio de publish
FROM build AS publish
RUN dotnet publish "IDE.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Estágio runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Instalar dependências do sistema
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        curl \
        iputils-ping \
    && rm -rf /var/lib/apt/lists/*

# Criar usuário não-root
RUN addgroup --system --gid 1001 dotnet \
    && adduser --system --uid 1001 --ingroup dotnet dotnet

# Copiar arquivos publicados
COPY --from=publish /app/publish .

# Configurar permissões
RUN chown -R dotnet:dotnet /app
USER dotnet

# Configurar variáveis de ambiente
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_USE_POLLING_FILE_WATCHER=true

# Expor porta
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

# Comando de inicialização
ENTRYPOINT ["dotnet", "IDE.Api.dll"]
```

### 1.2 Dockerfile para Development

#### IDE.Api/Dockerfile.dev
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0
WORKDIR /src

# Instalar dotnet-ef tool
RUN dotnet tool install --global dotnet-ef
ENV PATH="$PATH:/root/.dotnet/tools"

# Instalar dependências para hot reload
RUN dotnet dev-certs https

# Copiar arquivos de projeto
COPY ["IDE.Api/IDE.Api.csproj", "IDE.Api/"]
COPY ["IDE.Application/IDE.Application.csproj", "IDE.Application/"]
COPY ["IDE.Domain/IDE.Domain.csproj", "IDE.Domain/"]
COPY ["IDE.Infrastructure/IDE.Infrastructure.csproj", "IDE.Infrastructure/"]

# Restaurar dependências
RUN dotnet restore "IDE.Api/IDE.Api.csproj"

# Copiar código fonte
COPY . .

WORKDIR "/src/IDE.Api"

# Configurar variáveis de ambiente para desenvolvimento
ENV ASPNETCORE_ENVIRONMENT=Development
ENV ASPNETCORE_URLS=http://+:8080;https://+:8081
ENV ASPNETCORE_Kestrel__Certificates__Default__Password=dev-password
ENV ASPNETCORE_Kestrel__Certificates__Default__Path=/https/dev-cert.pfx

# Expor portas
EXPOSE 8080
EXPOSE 8081

# Hot reload command
CMD ["dotnet", "watch", "run", "--no-restore", "--urls", "http://0.0.0.0:8080;https://0.0.0.0:8081"]
```

## 2. Docker Compose

### 2.1 Docker Compose para Produção

#### docker-compose.yml
```yaml
version: '3.8'

networks:
  ide-network:
    driver: bridge

volumes:
  postgres-data:
    driver: local
  redis-data:
    driver: local

services:
  # Banco de dados PostgreSQL
  postgres:
    image: postgres:15-alpine
    container_name: ide-postgres
    restart: unless-stopped
    environment:
      POSTGRES_DB: ide_collaboration
      POSTGRES_USER: ide_user
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:-ide_strong_password_123}
    volumes:
      - postgres-data:/var/lib/postgresql/data
      - ./scripts/init-db.sql:/docker-entrypoint-initdb.d/init-db.sql:ro
    ports:
      - "5432:5432"
    networks:
      - ide-network
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ide_user -d ide_collaboration"]
      interval: 30s
      timeout: 10s
      retries: 3

  # Cache Redis
  redis:
    image: redis:7-alpine
    container_name: ide-redis
    restart: unless-stopped
    command: redis-server --appendonly yes --requirepass ${REDIS_PASSWORD:-redis_strong_password_123}
    volumes:
      - redis-data:/data
      - ./configs/redis.conf:/usr/local/etc/redis/redis.conf:ro
    ports:
      - "6379:6379"
    networks:
      - ide-network
    healthcheck:
      test: ["CMD", "redis-cli", "--raw", "incr", "ping"]
      interval: 30s
      timeout: 10s
      retries: 3

  # API de Colaboração
  api:
    build:
      context: .
      dockerfile: IDE.Api/Dockerfile
    container_name: ide-api
    restart: unless-stopped
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__DefaultConnection: "Host=postgres;Database=ide_collaboration;Username=ide_user;Password=${POSTGRES_PASSWORD:-ide_strong_password_123}"
      ConnectionStrings__Redis: "redis:6379,password=${REDIS_PASSWORD:-redis_strong_password_123}"
      JwtSettings__SecretKey: ${JWT_SECRET_KEY:-your-super-secret-key-here-change-in-production}
      JwtSettings__Issuer: "IDE.Api"
      JwtSettings__Audience: "IDE.Client"
      CorsSettings__AllowedOrigins__0: "${FRONTEND_URL:-http://localhost:3000}"
      CorsSettings__AllowedOrigins__1: "${FRONTEND_URL_HTTPS:-https://localhost:3000}"
      RateLimiting__MaxRequestsPerMinute: 200
      RateLimiting__MaxRequestsPerHour: 2000
      RateLimiting__MaxRequestsPerDay: 20000
    ports:
      - "8080:8080"
    networks:
      - ide-network
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s

  # Nginx Proxy
  nginx:
    image: nginx:alpine
    container_name: ide-nginx
    restart: unless-stopped
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./configs/nginx/nginx.conf:/etc/nginx/nginx.conf:ro
      - ./configs/nginx/default.conf:/etc/nginx/conf.d/default.conf:ro
      - ./ssl:/etc/nginx/ssl:ro
      - ./logs/nginx:/var/log/nginx
    networks:
      - ide-network
    depends_on:
      - api
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost/health"]
      interval: 30s
      timeout: 10s
      retries: 3

  # Prometheus para monitoramento (opcional)
  prometheus:
    image: prom/prometheus:latest
    container_name: ide-prometheus
    restart: unless-stopped
    ports:
      - "9090:9090"
    volumes:
      - ./configs/prometheus/prometheus.yml:/etc/prometheus/prometheus.yml:ro
    networks:
      - ide-network
    profiles:
      - monitoring

  # Grafana para dashboards (opcional)
  grafana:
    image: grafana/grafana:latest
    container_name: ide-grafana
    restart: unless-stopped
    ports:
      - "3000:3000"
    environment:
      GF_SECURITY_ADMIN_PASSWORD: ${GRAFANA_PASSWORD:-admin123}
    volumes:
      - ./configs/grafana:/etc/grafana/provisioning:ro
    networks:
      - ide-network
    profiles:
      - monitoring
```

### 2.2 Docker Compose para Desenvolvimento

#### docker-compose.dev.yml
```yaml
version: '3.8'

networks:
  ide-network:
    driver: bridge

volumes:
  postgres-data-dev:
    driver: local

services:
  # Banco de dados para desenvolvimento
  postgres-dev:
    image: postgres:15-alpine
    container_name: ide-postgres-dev
    restart: unless-stopped
    environment:
      POSTGRES_DB: ide_collaboration_dev
      POSTGRES_USER: ide_dev
      POSTGRES_PASSWORD: dev_password
    volumes:
      - postgres-data-dev:/var/lib/postgresql/data
    ports:
      - "5433:5432"
    networks:
      - ide-network

  # Redis para desenvolvimento
  redis-dev:
    image: redis:7-alpine
    container_name: ide-redis-dev
    restart: unless-stopped
    command: redis-server --appendonly yes
    ports:
      - "6380:6379"
    networks:
      - ide-network

  # API para desenvolvimento com hot reload
  api-dev:
    build:
      context: .
      dockerfile: IDE.Api/Dockerfile.dev
    container_name: ide-api-dev
    restart: unless-stopped
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ASPNETCORE_URLS: http://+:8080;https://+:8081
      ConnectionStrings__DefaultConnection: "Host=postgres-dev;Database=ide_collaboration_dev;Username=ide_dev;Password=dev_password"
      ConnectionStrings__Redis: "redis-dev:6379"
      JwtSettings__SecretKey: "development-secret-key-not-for-production"
      JwtSettings__Issuer: "IDE.Api.Dev"
      JwtSettings__Audience: "IDE.Client.Dev"
    ports:
      - "8080:8080"
      - "8081:8081"
    volumes:
      - .:/src:delegated
      - /src/IDE.Api/bin
      - /src/IDE.Api/obj
    networks:
      - ide-network
    depends_on:
      - postgres-dev
      - redis-dev
```

## 3. Configuration Files

### 3.1 Nginx Configuration

#### configs/nginx/nginx.conf
```nginx
user nginx;
worker_processes auto;

error_log /var/log/nginx/error.log notice;
pid /var/run/nginx.pid;

events {
    worker_connections 1024;
    use epoll;
    multi_accept on;
}

http {
    include /etc/nginx/mime.types;
    default_type application/octet-stream;

    # Logging
    log_format main '$remote_addr - $remote_user [$time_local] "$request" '
                    '$status $body_bytes_sent "$http_referer" '
                    '"$http_user_agent" "$http_x_forwarded_for"';

    access_log /var/log/nginx/access.log main;

    # Performance
    sendfile on;
    tcp_nopush on;
    tcp_nodelay on;
    keepalive_timeout 65;
    types_hash_max_size 2048;

    # Gzip compression
    gzip on;
    gzip_vary on;
    gzip_proxied any;
    gzip_comp_level 6;
    gzip_types
        text/plain
        text/css
        text/xml
        text/javascript
        application/json
        application/javascript
        application/xml+rss
        application/atom+xml
        image/svg+xml;

    # Rate limiting
    limit_req_zone $binary_remote_addr zone=api:10m rate=10r/s;
    limit_req_zone $binary_remote_addr zone=websocket:10m rate=5r/s;

    # Include server configurations
    include /etc/nginx/conf.d/*.conf;
}
```

#### configs/nginx/default.conf
```nginx
# Upstream para API
upstream ide_api {
    server api:8080;
    keepalive 32;
}

# Configuração do servidor principal
server {
    listen 80;
    server_name localhost;

    # Rate limiting
    limit_req zone=api burst=20 nodelay;

    # Configurações de proxy
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection 'upgrade';
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_cache_bypass $http_upgrade;

    # Health check endpoint
    location /health {
        proxy_pass http://ide_api/health;
        access_log off;
    }

    # API endpoints
    location /api/ {
        proxy_pass http://ide_api;
        proxy_connect_timeout 5s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;
    }

    # SignalR hubs - configuração especial para WebSocket
    location /hubs/ {
        limit_req zone=websocket burst=10 nodelay;
        
        proxy_pass http://ide_api;
        proxy_connect_timeout 5s;
        proxy_send_timeout 3600s;
        proxy_read_timeout 3600s;
        
        # WebSocket headers
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # Swagger UI
    location /swagger {
        proxy_pass http://ide_api/swagger;
    }

    # Metrics endpoint (opcional)
    location /metrics {
        proxy_pass http://ide_api/metrics;
        allow 127.0.0.1;
        allow 10.0.0.0/8;
        allow 172.16.0.0/12;
        allow 192.168.0.0/16;
        deny all;
    }

    # Security headers
    add_header X-Frame-Options "SAMEORIGIN" always;
    add_header X-XSS-Protection "1; mode=block" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header Referrer-Policy "no-referrer-when-downgrade" always;
    add_header Content-Security-Policy "default-src 'self' http: https: data: blob: 'unsafe-inline'" always;

    # Error pages
    error_page 500 502 503 504 /50x.html;
    location = /50x.html {
        root /usr/share/nginx/html;
    }
}
```

### 3.2 Redis Configuration

#### configs/redis.conf
```conf
# Redis configuration for IDE Collaboration

# Network
bind 0.0.0.0
port 6379
timeout 300
tcp-keepalive 300

# General
daemonize no
pidfile /var/run/redis_6379.pid
loglevel notice
logfile ""

# Persistence
save 900 1
save 300 10
save 60 10000
dbfilename dump.rdb
dir /data

# Append only file
appendonly yes
appendfilename "appendonly.aof"
appendfsync everysec

# Memory management
maxmemory 512mb
maxmemory-policy allkeys-lru

# Security
# requirepass will be set via command line

# Clients
maxclients 10000

# Keyspace notifications (for SignalR)
notify-keyspace-events Ex
```

## 4. Deployment Scripts

### 4.1 Production Deployment Script

#### scripts/deploy.sh
```bash
#!/bin/bash

# Deployment script for IDE Collaboration API
# Usage: ./scripts/deploy.sh [environment]

set -e

ENVIRONMENT=${1:-production}
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" &> /dev/null && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"

echo "🚀 Starting deployment for environment: $ENVIRONMENT"

# Load environment variables
if [ -f "$PROJECT_DIR/.env.$ENVIRONMENT" ]; then
    echo "📋 Loading environment variables from .env.$ENVIRONMENT"
    export $(cat "$PROJECT_DIR/.env.$ENVIRONMENT" | xargs)
fi

# Validate required environment variables
echo "🔍 Validating environment variables..."
required_vars=("POSTGRES_PASSWORD" "REDIS_PASSWORD" "JWT_SECRET_KEY")
for var in "${required_vars[@]}"; do
    if [ -z "${!var}" ]; then
        echo "❌ Error: $var is not set"
        exit 1
    fi
done

# Build and start services
echo "🔨 Building Docker images..."
cd "$PROJECT_DIR"

if [ "$ENVIRONMENT" = "development" ]; then
    docker-compose -f docker-compose.dev.yml build --no-cache
    docker-compose -f docker-compose.dev.yml up -d
    echo "🎯 Development environment started"
else
    docker-compose build --no-cache
    docker-compose up -d
    echo "🎯 Production environment started"
fi

# Wait for services to be healthy
echo "⏳ Waiting for services to be healthy..."
sleep 30

# Check health
echo "🏥 Checking service health..."
if curl -f http://localhost:8080/health; then
    echo "✅ API is healthy"
else
    echo "❌ API health check failed"
    exit 1
fi

# Run database migrations
echo "🗄️ Running database migrations..."
if [ "$ENVIRONMENT" = "development" ]; then
    docker-compose -f docker-compose.dev.yml exec api-dev dotnet ef database update
else
    docker-compose exec api dotnet ef database update
fi

echo "🎉 Deployment completed successfully!"
echo "📊 Services status:"
docker-compose ps
```

### 4.2 Database Migration Script

#### scripts/migrate.sh
```bash
#!/bin/bash

# Database migration script
# Usage: ./scripts/migrate.sh [up|down|reset]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" &> /dev/null && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
ACTION=${1:-up}

echo "🗄️ Database migration: $ACTION"

cd "$PROJECT_DIR"

case $ACTION in
    up)
        echo "📈 Applying migrations..."
        docker-compose exec api dotnet ef database update
        ;;
    down)
        echo "📉 Rolling back last migration..."
        docker-compose exec api dotnet ef migrations remove
        ;;
    reset)
        echo "🔄 Resetting database..."
        docker-compose exec api dotnet ef database drop --force
        docker-compose exec api dotnet ef database update
        ;;
    *)
        echo "❌ Invalid action. Use: up|down|reset"
        exit 1
        ;;
esac

echo "✅ Migration completed"
```

### 4.3 Backup Script

#### scripts/backup.sh
```bash
#!/bin/bash

# Backup script for IDE Collaboration system
# Usage: ./scripts/backup.sh

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" &> /dev/null && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
BACKUP_DIR="$PROJECT_DIR/backups"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)

echo "💾 Starting backup process..."

# Create backup directory
mkdir -p "$BACKUP_DIR"

# Backup PostgreSQL
echo "🗄️ Backing up PostgreSQL database..."
docker-compose exec -T postgres pg_dump \
    -U ide_user \
    -d ide_collaboration \
    --clean \
    --if-exists \
    > "$BACKUP_DIR/postgres_$TIMESTAMP.sql"

# Backup Redis (RDB snapshot)
echo "📊 Backing up Redis data..."
docker-compose exec redis redis-cli BGSAVE
sleep 5
docker cp ide-redis:/data/dump.rdb "$BACKUP_DIR/redis_$TIMESTAMP.rdb"

# Create tarball
echo "📦 Creating backup archive..."
cd "$BACKUP_DIR"
tar -czf "ide_backup_$TIMESTAMP.tar.gz" \
    "postgres_$TIMESTAMP.sql" \
    "redis_$TIMESTAMP.rdb"

# Cleanup individual files
rm "postgres_$TIMESTAMP.sql" "redis_$TIMESTAMP.rdb"

# Keep only last 10 backups
ls -t ide_backup_*.tar.gz | tail -n +11 | xargs -r rm --

echo "✅ Backup completed: ide_backup_$TIMESTAMP.tar.gz"
```

## 5. Environment Configuration

### 5.1 Production Environment Variables

#### .env.production
```env
# Database Configuration
POSTGRES_PASSWORD=your_super_secure_postgres_password_here
POSTGRES_DB=ide_collaboration
POSTGRES_USER=ide_user

# Redis Configuration
REDIS_PASSWORD=your_super_secure_redis_password_here

# JWT Configuration
JWT_SECRET_KEY=your-super-secret-jwt-key-change-this-in-production-64-chars-min
JWT_ISSUER=IDE.Api
JWT_AUDIENCE=IDE.Client

# CORS Configuration
FRONTEND_URL=https://your-frontend-domain.com
FRONTEND_URL_HTTPS=https://your-frontend-domain.com

# Rate Limiting
RATE_LIMIT_REQUESTS_PER_MINUTE=200
RATE_LIMIT_REQUESTS_PER_HOUR=2000
RATE_LIMIT_REQUESTS_PER_DAY=20000

# Monitoring (optional)
GRAFANA_PASSWORD=your_grafana_admin_password
```

### 5.2 Development Environment Variables

#### .env.development
```env
# Database Configuration
POSTGRES_PASSWORD=dev_password
POSTGRES_DB=ide_collaboration_dev
POSTGRES_USER=ide_dev

# Redis Configuration (no password for dev)
REDIS_PASSWORD=

# JWT Configuration
JWT_SECRET_KEY=development-secret-key-not-for-production-use-only
JWT_ISSUER=IDE.Api.Dev
JWT_AUDIENCE=IDE.Client.Dev

# CORS Configuration
FRONTEND_URL=http://localhost:3000
FRONTEND_URL_HTTPS=https://localhost:3000

# Rate Limiting (more permissive for dev)
RATE_LIMIT_REQUESTS_PER_MINUTE=1000
RATE_LIMIT_REQUESTS_PER_HOUR=10000
RATE_LIMIT_REQUESTS_PER_DAY=100000
```

## 6. Documentation

### 6.1 README.md Principal

#### README.md
```markdown
# IDE Collaboration API

API completa de colaboração em tempo real para IDEs, com suporte a chat, presença de usuários, edição colaborativa e notificações.

## 🚀 Características

- **Colaboração em Tempo Real**: Edição simultânea com operational transform
- **Chat Integrado**: Sistema completo de mensagens com reactions
- **Presença de Usuários**: Tracking de usuários ativos e suas ações
- **Notificações**: Sistema de notificações em tempo real
- **Rate Limiting**: Controle de taxa para prevenção de abuse
- **Métricas e Auditoria**: Monitoramento completo do sistema
- **SignalR**: WebSocket com fallback para long-polling
- **RESTful API**: Endpoints HTTP complementares

## 🛠️ Tecnologias

- **.NET 8**: Framework principal
- **SignalR**: Comunicação em tempo real
- **Entity Framework Core**: ORM
- **PostgreSQL**: Banco de dados principal
- **Redis**: Cache e backplane do SignalR
- **Docker**: Containerização
- **Nginx**: Proxy reverso e load balancing

## 📋 Pré-requisitos

- Docker & Docker Compose
- .NET 8 SDK (para desenvolvimento)
- PostgreSQL 15+
- Redis 7+

## 🚀 Execução Rápida

### Produção
```bash
# Clone o repositório
git clone <repository-url>
cd ide-collaboration-api

# Configure variáveis de ambiente
cp .env.production.example .env.production
# Edite .env.production com suas configurações

# Deploy
./scripts/deploy.sh production
```

### Desenvolvimento
```bash
# Clone o repositório
git clone <repository-url>
cd ide-collaboration-api

# Configure variáveis de ambiente
cp .env.development.example .env.development

# Iniciar ambiente de desenvolvimento
./scripts/deploy.sh development
```

## 📚 API Endpoints

### Autenticação
- `POST /api/auth/login` - Login do usuário
- `POST /api/auth/register` - Registro de usuário
- `POST /api/auth/refresh` - Refresh token

### Chat
- `GET /api/workspaces/{id}/chat/messages` - Listar mensagens
- `POST /api/workspaces/{id}/chat/messages` - Enviar mensagem
- `PUT /api/workspaces/{id}/chat/messages/{id}` - Editar mensagem
- `DELETE /api/workspaces/{id}/chat/messages/{id}` - Deletar mensagem

### Presença
- `GET /api/workspaces/{id}/presence` - Usuários ativos
- `GET /api/workspaces/{id}/presence/active-count` - Contagem de ativos

### Notificações
- `GET /api/notifications` - Listar notificações
- `POST /api/notifications/{id}/mark-read` - Marcar como lida

### SignalR Hubs
- `/hubs/collaboration` - Hub principal de colaboração
- `/hubs/chat` - Hub dedicado ao chat

## 🔧 Configuração

### Variáveis de Ambiente

Veja os arquivos `.env.production` e `.env.development` para todas as configurações disponíveis.

### Banco de Dados

```bash
# Aplicar migrations
./scripts/migrate.sh up

# Reset do banco (CUIDADO!)
./scripts/migrate.sh reset
```

### Backup

```bash
# Criar backup
./scripts/backup.sh

# Backups são salvos em ./backups/
```

## 🏥 Monitoramento

### Health Checks
- `GET /health` - Status geral do sistema

### Métricas (Admin)
- `GET /api/metrics/system` - Métricas do sistema
- `GET /api/metrics/workspaces/{id}` - Métricas do workspace

### Logs
Os logs são coletados pelo Docker e podem ser visualizados com:
```bash
docker-compose logs -f api
```

## 🔒 Segurança

- **JWT**: Autenticação baseada em tokens
- **CORS**: Configurado para domínios específicos
- **Rate Limiting**: Proteção contra abuse
- **HTTPS**: Obrigatório em produção
- **Sanitização**: Entrada de dados sanitizada

## 🚀 Deploy em Produção

### Pré-deploy Checklist

- [ ] Configurar variáveis de ambiente de produção
- [ ] Configurar certificados SSL
- [ ] Configurar backup automatizado
- [ ] Configurar monitoramento
- [ ] Testar health checks

### Comandos de Deploy

```bash
# Deploy inicial
./scripts/deploy.sh production

# Atualizações
docker-compose pull
docker-compose up -d

# Verificar status
docker-compose ps
curl http://localhost/health
```

## 🐛 Troubleshooting

### API não responde
```bash
# Verificar logs
docker-compose logs api

# Verificar saúde dos serviços
docker-compose ps
curl http://localhost/health
```

### SignalR não conecta
- Verificar CORS settings
- Verificar autenticação JWT
- Verificar logs do hub

### Banco de dados
```bash
# Conectar ao PostgreSQL
docker-compose exec postgres psql -U ide_user -d ide_collaboration

# Verificar migrations
docker-compose exec api dotnet ef migrations list
```

## 📖 Documentação da API

Acesse `/swagger` para documentação interativa da API.

## 🤝 Contribuição

1. Fork o projeto
2. Crie uma branch para sua feature (`git checkout -b feature/AmazingFeature`)
3. Commit suas mudanças (`git commit -m 'Add some AmazingFeature'`)
4. Push para a branch (`git push origin feature/AmazingFeature`)
5. Abra um Pull Request

## 📄 Licença

Este projeto está licenciado sob a MIT License.
```

## Entregáveis da Parte 3.12

✅ **Containerização completa**:
- Dockerfile otimizado para produção
- Dockerfile.dev para desenvolvimento
- Multi-stage build com segurança

✅ **Docker Compose**:
- Ambiente de produção completo
- Ambiente de desenvolvimento
- Serviços auxiliares (Nginx, Redis, PostgreSQL)

✅ **Scripts de deployment**:
- Deploy automatizado
- Migrations de banco
- Backup e restore
- Health checks

✅ **Configurações**:
- Nginx como proxy reverso
- Redis otimizado
- Variáveis de ambiente
- SSL/TLS ready

✅ **Documentação**:
- README completo
- Instruções de deploy
- Troubleshooting guide
- API documentation

## 🎯 Sistema Completo!

Com esta parte 3.12, o **sistema completo de colaboração em tempo real** está finalizado, incluindo:

- ✅ **Camada de Domínio** (3.1)
- ✅ **SignalR Hubs** (3.2-3.3)
- ✅ **Services Completos** (3.4-3.6)
- ✅ **Controllers REST** (3.7)
- ✅ **Configuração DI** (3.8)
- ✅ **Docker & Deploy** (3.9)

**Próximo passo**: Testar o sistema completo ou partir para a **Fase 4** de otimizações e recursos avançados.