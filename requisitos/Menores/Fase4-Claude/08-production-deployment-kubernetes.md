# Fase 4 - Parte 8: Production Deployment & Kubernetes

## Contexto da Implementação

Esta é a **oitava parte da Fase 4** focada no **deployment completo em produção** usando **Kubernetes** com configurações de alta disponibilidade, monitoramento e estratégias de rollback.

### Objetivos da Parte 8
✅ **Kubernetes manifests** production-ready  
✅ **Multi-environment** deployment (dev/prod)  
✅ **Rolling updates** com zero downtime  
✅ **Auto-scaling** baseado em métricas  
✅ **Health checks** completos  
✅ **Secrets management** seguro  

### Pré-requisitos
- Partes 1-7 implementadas e testadas
- Cluster Kubernetes configurado
- Docker Registry (Azure ACR) disponível
- NGINX Ingress Controller instalado
- cert-manager para SSL (opcional)

---

## 7. Production Deployment

### 7.1 Kubernetes Manifests

Configurações completas para deployment em produção com alta disponibilidade.

#### k8s/namespace.yaml
```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: ide-production
  labels:
    name: ide-production
    environment: production
---
apiVersion: v1
kind: Namespace
metadata:
  name: ide-development
  labels:
    name: ide-development
    environment: development
---
apiVersion: v1
kind: Namespace
metadata:
  name: ide-staging
  labels:
    name: ide-staging
    environment: staging
```

#### k8s/configmap.yaml
```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: ide-backend-config
  namespace: ide-production
data:
  appsettings.json: |
    {
      "ConnectionStrings": {
        "DefaultConnection": "",
        "Redis": "redis-service:6379"
      },
      "JWT": {
        "Issuer": "IDE.API",
        "Audience": "IDE.Frontend",
        "ExpiryInMinutes": 60
      },
      "RateLimiting": {
        "EnableRateLimiting": true,
        "GlobalLimit": 1000,
        "WindowMinutes": 60
      },
      "Logging": {
        "LogLevel": {
          "Default": "Information",
          "Microsoft.AspNetCore": "Warning",
          "System.Net.Http.HttpClient": "Warning",
          "IDE": "Information"
        }
      },
      "AllowedHosts": "*",
      "ASPNETCORE_ENVIRONMENT": "Production"
    }
  redis.conf: |
    # Redis configuration for production
    maxmemory 1gb
    maxmemory-policy allkeys-lru
    save 900 1
    save 300 10
    save 60 10000
    appendonly yes
    appendfsync everysec
---
apiVersion: v1
kind: ConfigMap
metadata:
  name: ide-backend-config-dev
  namespace: ide-development
data:
  appsettings.json: |
    {
      "ConnectionStrings": {
        "DefaultConnection": "",
        "Redis": "redis-service-dev:6379"
      },
      "JWT": {
        "Issuer": "IDE.API",
        "Audience": "IDE.Frontend",
        "ExpiryInMinutes": 60
      },
      "RateLimiting": {
        "EnableRateLimiting": false
      },
      "Logging": {
        "LogLevel": {
          "Default": "Debug",
          "Microsoft.AspNetCore": "Information",
          "IDE": "Debug"
        }
      },
      "AllowedHosts": "*",
      "ASPNETCORE_ENVIRONMENT": "Development"
    }
```

#### k8s/secrets.yaml
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: ide-backend-secrets
  namespace: ide-production
type: Opaque
data:
  # Base64 encoded values - NEVER commit real secrets to Git
  # Use kubectl create secret or external secret management
  ConnectionStrings__DefaultConnection: <base64-encoded-connection-string>
  ConnectionStrings__Redis: <base64-encoded-redis-connection>
  JWT__Secret: <base64-encoded-jwt-secret>
  OAuth__GitHub__ClientId: <base64-encoded-github-client-id>
  OAuth__GitHub__ClientSecret: <base64-encoded-github-secret>
  OAuth__Google__ClientId: <base64-encoded-google-client-id>
  OAuth__Google__ClientSecret: <base64-encoded-google-secret>
  OAuth__Microsoft__ClientId: <base64-encoded-microsoft-client-id>
  OAuth__Microsoft__ClientSecret: <base64-encoded-microsoft-secret>
  Azure__Storage__ConnectionString: <base64-encoded-storage-connection>
  SendGrid__ApiKey: <base64-encoded-sendgrid-key>
---
apiVersion: v1
kind: Secret
metadata:
  name: ide-backend-secrets-dev
  namespace: ide-development
type: Opaque
data:
  # Development secrets
  ConnectionStrings__DefaultConnection: <base64-encoded-dev-connection-string>
  ConnectionStrings__Redis: <base64-encoded-dev-redis-connection>
  JWT__Secret: <base64-encoded-dev-jwt-secret>
---
apiVersion: v1
kind: Secret
metadata:
  name: acr-secret
  namespace: ide-production
type: kubernetes.io/dockerconfigjson
data:
  .dockerconfigjson: <base64-encoded-docker-config>
---
apiVersion: v1
kind: Secret
metadata:
  name: acr-secret
  namespace: ide-development
type: kubernetes.io/dockerconfigjson
data:
  .dockerconfigjson: <base64-encoded-docker-config>
```

#### k8s/deployment.yaml
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: ide-backend
  namespace: ide-production
  labels:
    app: ide-backend
    version: v1
    environment: production
spec:
  replicas: 3
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxUnavailable: 1
      maxSurge: 1
  selector:
    matchLabels:
      app: ide-backend
  template:
    metadata:
      labels:
        app: ide-backend
        version: v1
        environment: production
      annotations:
        prometheus.io/scrape: "true"
        prometheus.io/port: "8080"
        prometheus.io/path: "/metrics"
    spec:
      containers:
      - name: ide-backend
        image: ideregistry.azurecr.io/ide-backend:latest
        ports:
        - containerPort: 8080
          name: http
          protocol: TCP
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ASPNETCORE_URLS
          value: "http://+:8080"
        - name: POD_NAME
          valueFrom:
            fieldRef:
              fieldPath: metadata.name
        - name: POD_NAMESPACE
          valueFrom:
            fieldRef:
              fieldPath: metadata.namespace
        - name: POD_IP
          valueFrom:
            fieldRef:
              fieldPath: status.podIP
        envFrom:
        - configMapRef:
            name: ide-backend-config
        - secretRef:
            name: ide-backend-secrets
        resources:
          requests:
            cpu: 200m
            memory: 512Mi
          limits:
            cpu: 1000m
            memory: 2Gi
        livenessProbe:
          httpGet:
            path: /health/live
            port: 8080
            httpHeaders:
            - name: Accept
              value: application/json
          initialDelaySeconds: 45
          periodSeconds: 30
          timeoutSeconds: 10
          failureThreshold: 3
          successThreshold: 1
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 8080
            httpHeaders:
            - name: Accept
              value: application/json
          initialDelaySeconds: 20
          periodSeconds: 10
          timeoutSeconds: 5
          failureThreshold: 3
          successThreshold: 1
        startupProbe:
          httpGet:
            path: /health
            port: 8080
            httpHeaders:
            - name: Accept
              value: application/json
          initialDelaySeconds: 10
          periodSeconds: 5
          timeoutSeconds: 5
          failureThreshold: 20
          successThreshold: 1
        securityContext:
          allowPrivilegeEscalation: false
          runAsNonRoot: true
          runAsUser: 65534
          readOnlyRootFilesystem: true
          capabilities:
            drop:
            - ALL
        volumeMounts:
        - name: tmp
          mountPath: /tmp
        - name: var-log
          mountPath: /var/log
      volumes:
      - name: tmp
        emptyDir: {}
      - name: var-log
        emptyDir: {}
      imagePullSecrets:
      - name: acr-secret
      restartPolicy: Always
      terminationGracePeriodSeconds: 60
      affinity:
        podAntiAffinity:
          preferredDuringSchedulingIgnoredDuringExecution:
          - weight: 100
            podAffinityTerm:
              labelSelector:
                matchExpressions:
                - key: app
                  operator: In
                  values:
                  - ide-backend
              topologyKey: kubernetes.io/hostname
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: ide-backend-dev
  namespace: ide-development
  labels:
    app: ide-backend-dev
    environment: development
spec:
  replicas: 1
  selector:
    matchLabels:
      app: ide-backend-dev
  template:
    metadata:
      labels:
        app: ide-backend-dev
        environment: development
    spec:
      containers:
      - name: ide-backend
        image: ideregistry.azurecr.io/ide-backend:dev
        ports:
        - containerPort: 8080
          name: http
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Development"
        - name: ASPNETCORE_URLS
          value: "http://+:8080"
        envFrom:
        - configMapRef:
            name: ide-backend-config-dev
        - secretRef:
            name: ide-backend-secrets-dev
        resources:
          requests:
            cpu: 100m
            memory: 256Mi
          limits:
            cpu: 500m
            memory: 1Gi
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 30
        readinessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 15
          periodSeconds: 10
      imagePullSecrets:
      - name: acr-secret
```

#### k8s/service.yaml
```yaml
apiVersion: v1
kind: Service
metadata:
  name: ide-backend-service
  namespace: ide-production
  labels:
    app: ide-backend
    environment: production
  annotations:
    service.beta.kubernetes.io/azure-load-balancer-internal: "false"
spec:
  type: ClusterIP
  ports:
  - port: 80
    targetPort: 8080
    protocol: TCP
    name: http
  selector:
    app: ide-backend
  sessionAffinity: None
---
apiVersion: v1
kind: Service
metadata:
  name: ide-backend-service-dev
  namespace: ide-development
  labels:
    app: ide-backend-dev
    environment: development
spec:
  type: ClusterIP
  ports:
  - port: 80
    targetPort: 8080
    protocol: TCP
    name: http
  selector:
    app: ide-backend-dev
```

### 7.2 Auto-scaling Configuration

#### k8s/hpa.yaml
```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: ide-backend-hpa
  namespace: ide-production
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: ide-backend
  minReplicas: 3
  maxReplicas: 15
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80
  - type: Pods
    pods:
      metric:
        name: http_requests_per_second
      target:
        type: AverageValue
        averageValue: "100"
  behavior:
    scaleDown:
      stabilizationWindowSeconds: 300
      policies:
      - type: Percent
        value: 10
        periodSeconds: 60
      - type: Pods
        value: 1
        periodSeconds: 60
      selectPolicy: Min
    scaleUp:
      stabilizationWindowSeconds: 60
      policies:
      - type: Percent
        value: 50
        periodSeconds: 60
      - type: Pods
        value: 3
        periodSeconds: 60
      selectPolicy: Max
```

#### k8s/vpa.yaml
```yaml
apiVersion: autoscaling.k8s.io/v1
kind: VerticalPodAutoscaler
metadata:
  name: ide-backend-vpa
  namespace: ide-production
spec:
  targetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: ide-backend
  updatePolicy:
    updateMode: "Auto"
  resourcePolicy:
    containerPolicies:
    - containerName: ide-backend
      maxAllowed:
        cpu: 2000m
        memory: 4Gi
      minAllowed:
        cpu: 100m
        memory: 256Mi
      controlledResources: ["cpu", "memory"]
```

### 7.3 Ingress and Load Balancing

#### k8s/ingress.yaml
```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: ide-backend-ingress
  namespace: ide-production
  annotations:
    kubernetes.io/ingress.class: nginx
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
    nginx.ingress.kubernetes.io/force-ssl-redirect: "true"
    nginx.ingress.kubernetes.io/proxy-body-size: "100m"
    nginx.ingress.kubernetes.io/proxy-read-timeout: "300"
    nginx.ingress.kubernetes.io/proxy-send-timeout: "300"
    nginx.ingress.kubernetes.io/rate-limit: "100"
    nginx.ingress.kubernetes.io/rate-limit-window: "1m"
    nginx.ingress.kubernetes.io/rate-limit-connections: "10"
    nginx.ingress.kubernetes.io/enable-cors: "true"
    nginx.ingress.kubernetes.io/cors-allow-origin: "https://ide-platform.com,https://app.ide-platform.com"
    nginx.ingress.kubernetes.io/cors-allow-methods: "GET,POST,PUT,DELETE,OPTIONS"
    nginx.ingress.kubernetes.io/cors-allow-headers: "Authorization,Content-Type,Accept"
    cert-manager.io/cluster-issuer: "letsencrypt-prod"
    nginx.ingress.kubernetes.io/configuration-snippet: |
      add_header X-Frame-Options "DENY" always;
      add_header X-Content-Type-Options "nosniff" always;
      add_header X-XSS-Protection "1; mode=block" always;
      add_header Referrer-Policy "strict-origin-when-cross-origin" always;
spec:
  tls:
  - hosts:
    - api.ide-platform.com
    secretName: ide-api-tls
  rules:
  - host: api.ide-platform.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: ide-backend-service
            port:
              number: 80
      - path: /hubs
        pathType: Prefix
        backend:
          service:
            name: ide-backend-service
            port:
              number: 80
---
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: ide-backend-ingress-dev
  namespace: ide-development
  annotations:
    kubernetes.io/ingress.class: nginx
    nginx.ingress.kubernetes.io/ssl-redirect: "false"
    nginx.ingress.kubernetes.io/proxy-body-size: "100m"
    nginx.ingress.kubernetes.io/enable-cors: "true"
    nginx.ingress.kubernetes.io/cors-allow-origin: "*"
spec:
  rules:
  - host: api-dev.ide-platform.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: ide-backend-service-dev
            port:
              number: 80
```

### 7.4 Pod Disruption Budget

#### k8s/pdb.yaml
```yaml
apiVersion: policy/v1
kind: PodDisruptionBudget
metadata:
  name: ide-backend-pdb
  namespace: ide-production
spec:
  minAvailable: 2
  selector:
    matchLabels:
      app: ide-backend
---
apiVersion: policy/v1
kind: PodDisruptionBudget
metadata:
  name: redis-pdb
  namespace: ide-production
spec:
  minAvailable: 1
  selector:
    matchLabels:
      app: redis
```

### 7.5 Network Policies

#### k8s/network-policy.yaml
```yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: ide-backend-netpol
  namespace: ide-production
spec:
  podSelector:
    matchLabels:
      app: ide-backend
  policyTypes:
  - Ingress
  - Egress
  ingress:
  - from:
    - namespaceSelector:
        matchLabels:
          name: ingress-nginx
    ports:
    - protocol: TCP
      port: 8080
  - from:
    - namespaceSelector:
        matchLabels:
          name: monitoring
    ports:
    - protocol: TCP
      port: 8080
  egress:
  - to:
    - namespaceSelector:
        matchLabels:
          name: ide-production
    - podSelector:
        matchLabels:
          app: redis
    ports:
    - protocol: TCP
      port: 6379
  - to: []
    ports:
    - protocol: TCP
      port: 443  # HTTPS outbound
    - protocol: TCP
      port: 5432 # PostgreSQL
  - to: []
    ports:
    - protocol: UDP
      port: 53   # DNS
```

---

## 7.6 Deployment Scripts

### Production Deployment Script

#### scripts/deploy.sh
```bash
#!/bin/bash

set -e

# Configuration
ENVIRONMENT=${1:-production}
IMAGE_TAG=${2:-latest}
NAMESPACE="ide-${ENVIRONMENT}"
REGISTRY="ideregistry.azurecr.io"
APP_NAME="ide-backend"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}=== IDE Backend Deployment ===${NC}"
echo -e "${BLUE}Environment: ${ENVIRONMENT}${NC}"
echo -e "${BLUE}Image tag: ${IMAGE_TAG}${NC}"
echo -e "${BLUE}Namespace: ${NAMESPACE}${NC}"
echo -e "${BLUE}Registry: ${REGISTRY}${NC}"
echo ""

# Check prerequisites
check_prerequisites() {
    echo -e "${YELLOW}Checking prerequisites...${NC}"
    
    if ! command -v kubectl &> /dev/null; then
        echo -e "${RED}kubectl is required but not installed. Aborting.${NC}"
        exit 1
    fi
    
    if ! command -v helm &> /dev/null; then
        echo -e "${YELLOW}helm not found, some optional features may not work.${NC}"
    fi
    
    # Test cluster connectivity
    if ! kubectl cluster-info &> /dev/null; then
        echo -e "${RED}Cannot connect to Kubernetes cluster. Check your kubeconfig.${NC}"
        exit 1
    fi
    
    echo -e "${GREEN}Prerequisites check passed.${NC}"
}

# Create or verify namespace
setup_namespace() {
    echo -e "${YELLOW}Setting up namespace ${NAMESPACE}...${NC}"
    
    if ! kubectl get namespace "${NAMESPACE}" &> /dev/null; then
        echo -e "${YELLOW}Creating namespace ${NAMESPACE}...${NC}"
        kubectl apply -f k8s/namespace.yaml
    else
        echo -e "${GREEN}Namespace ${NAMESPACE} already exists.${NC}"
    fi
}

# Deploy based on environment
deploy() {
    case $ENVIRONMENT in
        "production")
            deploy_production
            ;;
        "development")
            deploy_development
            ;;
        "staging")
            deploy_staging
            ;;
        *)
            echo -e "${RED}Unknown environment: ${ENVIRONMENT}${NC}"
            echo "Supported environments: production, development, staging"
            exit 1
            ;;
    esac
}

deploy_production() {
    echo -e "${YELLOW}Deploying to production environment...${NC}"
    
    # Apply secrets (should be managed externally in real production)
    echo -e "${YELLOW}Applying secrets...${NC}"
    kubectl apply -f k8s/secrets.yaml -n "${NAMESPACE}"
    
    # Apply configmaps
    echo -e "${YELLOW}Applying configuration...${NC}"
    kubectl apply -f k8s/configmap.yaml -n "${NAMESPACE}"
    
    # Apply Pod Disruption Budget first
    echo -e "${YELLOW}Applying Pod Disruption Budget...${NC}"
    kubectl apply -f k8s/pdb.yaml -n "${NAMESPACE}"
    
    # Apply Network Policies
    echo -e "${YELLOW}Applying Network Policies...${NC}"
    kubectl apply -f k8s/network-policy.yaml -n "${NAMESPACE}"
    
    # Update deployment with new image
    if kubectl get deployment "${APP_NAME}" -n "${NAMESPACE}" &> /dev/null; then
        echo -e "${YELLOW}Updating existing deployment with new image...${NC}"
        kubectl set image deployment/"${APP_NAME}" "${APP_NAME}"="${REGISTRY}/${APP_NAME}:${IMAGE_TAG}" -n "${NAMESPACE}"
    fi
    
    # Apply all resources
    echo -e "${YELLOW}Applying deployment manifests...${NC}"
    kubectl apply -f k8s/deployment.yaml -n "${NAMESPACE}"
    kubectl apply -f k8s/service.yaml -n "${NAMESPACE}"
    kubectl apply -f k8s/hpa.yaml -n "${NAMESPACE}"
    kubectl apply -f k8s/ingress.yaml -n "${NAMESPACE}"
    
    # Optional: Apply VPA if supported
    if kubectl api-resources | grep -q verticalpodautoscalers; then
        echo -e "${YELLOW}Applying Vertical Pod Autoscaler...${NC}"
        kubectl apply -f k8s/vpa.yaml -n "${NAMESPACE}"
    fi
}

deploy_development() {
    echo -e "${YELLOW}Deploying to development environment...${NC}"
    
    # Apply dev-specific resources
    kubectl apply -f k8s/secrets.yaml -n "${NAMESPACE}"
    kubectl apply -f k8s/configmap.yaml -n "${NAMESPACE}"
    
    # Update image
    if kubectl get deployment "${APP_NAME}-dev" -n "${NAMESPACE}" &> /dev/null; then
        kubectl set image deployment/"${APP_NAME}-dev" "${APP_NAME}"="${REGISTRY}/${APP_NAME}:${IMAGE_TAG}" -n "${NAMESPACE}"
    fi
    
    kubectl apply -f k8s/deployment.yaml -n "${NAMESPACE}"
    kubectl apply -f k8s/service.yaml -n "${NAMESPACE}"
    kubectl apply -f k8s/ingress.yaml -n "${NAMESPACE}"
}

deploy_staging() {
    echo -e "${YELLOW}Deploying to staging environment...${NC}"
    # Similar to production but with staging-specific configurations
    deploy_production
}

# Wait for deployment to complete
wait_for_deployment() {
    echo -e "${YELLOW}Waiting for deployment to complete...${NC}"
    
    local deployment_name="${APP_NAME}"
    if [ "$ENVIRONMENT" = "development" ]; then
        deployment_name="${APP_NAME}-dev"
    fi
    
    if ! kubectl rollout status deployment/"${deployment_name}" -n "${NAMESPACE}" --timeout=600s; then
        echo -e "${RED}Deployment failed or timed out!${NC}"
        echo -e "${YELLOW}Recent events:${NC}"
        kubectl get events -n "${NAMESPACE}" --sort-by='.lastTimestamp' | tail -10
        exit 1
    fi
    
    echo -e "${GREEN}Deployment completed successfully!${NC}"
}

# Verify deployment
verify_deployment() {
    echo -e "${YELLOW}Verifying deployment...${NC}"
    
    # Check pod status
    echo -e "${YELLOW}Pod status:${NC}"
    kubectl get pods -n "${NAMESPACE}" -l app="${APP_NAME}"
    
    # Check service
    echo -e "${YELLOW}Service status:${NC}"
    kubectl get service -n "${NAMESPACE}"
    
    # Check ingress
    echo -e "${YELLOW}Ingress status:${NC}"
    kubectl get ingress -n "${NAMESPACE}"
    
    # Health check
    echo -e "${YELLOW}Performing health check...${NC}"
    sleep 30
    
    local deployment_name="${APP_NAME}"
    if [ "$ENVIRONMENT" = "development" ]; then
        deployment_name="${APP_NAME}-dev"
    fi
    
    if kubectl exec -n "${NAMESPACE}" deployment/"${deployment_name}" -- curl -f http://localhost:8080/health > /dev/null 2>&1; then
        echo -e "${GREEN}Health check passed!${NC}"
    else
        echo -e "${YELLOW}Health check failed, but deployment may still be starting...${NC}"
    fi
}

# Cleanup function
cleanup() {
    if [ $? -ne 0 ]; then
        echo -e "${RED}Deployment failed!${NC}"
        echo -e "${YELLOW}Showing recent logs:${NC}"
        kubectl logs -n "${NAMESPACE}" -l app="${APP_NAME}" --tail=50
    fi
}

# Main execution
main() {
    trap cleanup EXIT
    
    check_prerequisites
    setup_namespace
    deploy
    wait_for_deployment
    verify_deployment
    
    echo -e "${GREEN}=== Deployment completed successfully! ===${NC}"
    echo -e "${GREEN}Application is available at:${NC}"
    
    if [ "$ENVIRONMENT" = "production" ]; then
        echo -e "${GREEN}  - https://api.ide-platform.com${NC}"
    else
        echo -e "${GREEN}  - https://api-${ENVIRONMENT}.ide-platform.com${NC}"
    fi
}

# Execute main function
main "$@"
```

### Rollback Script

#### scripts/rollback.sh
```bash
#!/bin/bash

set -e

ENVIRONMENT=${1:-production}
NAMESPACE="ide-${ENVIRONMENT}"
APP_NAME="ide-backend"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

echo -e "${BLUE}=== IDE Backend Rollback ===${NC}"
echo -e "${BLUE}Environment: ${ENVIRONMENT}${NC}"
echo -e "${BLUE}Namespace: ${NAMESPACE}${NC}"
echo ""

# Determine deployment name
DEPLOYMENT_NAME="${APP_NAME}"
if [ "$ENVIRONMENT" = "development" ]; then
    DEPLOYMENT_NAME="${APP_NAME}-dev"
fi

# Check rollout history
echo -e "${YELLOW}Current rollout history:${NC}"
kubectl rollout history deployment/"${DEPLOYMENT_NAME}" -n "${NAMESPACE}"

# Prompt for confirmation
echo ""
echo -e "${YELLOW}Are you sure you want to rollback to the previous version? (y/N)${NC}"
read -r response
if [[ ! "$response" =~ ^[Yy]$ ]]; then
    echo -e "${YELLOW}Rollback cancelled.${NC}"
    exit 0
fi

# Perform rollback
echo -e "${YELLOW}Rolling back to previous version...${NC}"
kubectl rollout undo deployment/"${DEPLOYMENT_NAME}" -n "${NAMESPACE}"

# Wait for rollback to complete
echo -e "${YELLOW}Waiting for rollback to complete...${NC}"
kubectl rollout status deployment/"${DEPLOYMENT_NAME}" -n "${NAMESPACE}" --timeout=300s

# Verify rollback
echo -e "${YELLOW}Verifying rollback...${NC}"
kubectl get pods -n "${NAMESPACE}" -l app="${APP_NAME}"

# Health check
sleep 30
if kubectl exec -n "${NAMESPACE}" deployment/"${DEPLOYMENT_NAME}" -- curl -f http://localhost:8080/health > /dev/null 2>&1; then
    echo -e "${GREEN}Rollback completed successfully!${NC}"
else
    echo -e "${RED}Rollback completed but health check failed. Please investigate.${NC}"
    exit 1
fi

echo -e "${GREEN}=== Rollback completed successfully! ===${NC}"
```

---

## Entregáveis da Parte 8

### ✅ Implementações Completas
- **Kubernetes manifests** para prod/dev/staging
- **Auto-scaling** (HPA e VPA) configurado
- **Rolling updates** com zero downtime
- **Health checks** robustos (startup/liveness/readiness)
- **Ingress** com SSL e rate limiting
- **Secrets management** seguro

### ✅ Funcionalidades de Deployment
- **Multi-environment** support (prod/dev/staging)
- **Pod Disruption Budget** para alta disponibilidade
- **Network Policies** para segurança
- **Resource limits** e requests otimizados
- **Affinity rules** para distribuição de pods
- **Security contexts** hardened

### ✅ Scripts de Automação
- **Deploy script** completo com validações
- **Rollback script** para reversão rápida
- **Health checks** automatizados
- **Logging** e troubleshooting integrados
- **Environment-specific** configurations
- **Error handling** robusto

---

## Validação da Parte 8

### Critérios de Sucesso
- [ ] Deploy em produção funciona sem erros
- [ ] Auto-scaling responde à carga
- [ ] Health checks passam em todos os pods
- [ ] Rolling update funciona com zero downtime
- [ ] SSL e ingress funcionam corretamente
- [ ] Rollback funciona em caso de problemas

### Testes de Deployment
```bash
# 1. Deploy inicial
./scripts/deploy.sh production v1.0.0

# 2. Verificar pods
kubectl get pods -n ide-production -l app=ide-backend

# 3. Testar health check
kubectl exec -n ide-production deployment/ide-backend -- curl -f http://localhost:8080/health

# 4. Teste de load (trigger auto-scaling)
kubectl run load-test --image=busybox --rm -it --restart=Never -- /bin/sh

# 5. Rolling update
./scripts/deploy.sh production v1.0.1

# 6. Rollback se necessário
./scripts/rollback.sh production
```

### Production Targets
- **Pod startup time**: < 30 seconds
- **Rolling update time**: < 2 minutes
- **Zero downtime**: Durante updates
- **Auto-scaling**: Resposta < 60 seconds
- **SSL grade**: A+ rating
- **Resource efficiency**: 70% CPU/memory utilization

---

## Próximos Passos

Após validação da Parte 8, prosseguir para:
- **Parte 9**: Monitoring & Observability

---

**Tempo Estimado**: 4-5 horas  
**Complexidade**: Alta  
**Dependências**: Kubernetes Cluster, Docker Registry, NGINX Ingress  
**Entregável**: Sistema completo de deployment em produção com alta disponibilidade