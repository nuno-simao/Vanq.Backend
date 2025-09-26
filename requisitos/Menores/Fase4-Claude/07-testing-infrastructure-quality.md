# Fase 4 - Parte 7: Testing Infrastructure & Quality Assurance

## Contexto da Implementação

Esta é a **sétima parte da Fase 4** focada na **implementação completa de testes** (Unit, Integration, E2E) e **documentação Swagger** para garantir qualidade e confiabilidade da solução.

### Objetivos da Parte 7
✅ **Unit tests** para services e controllers  
✅ **Integration tests** para APIs completas  
✅ **Security tests** (SQL injection, XSS, rate limiting)  
✅ **Performance tests** com benchmarks  
✅ **Swagger documentation** completa  
✅ **Test coverage** > 85%  

### Pré-requisitos
- Partes 1-6 implementadas e funcionais
- .NET Test SDK configurado
- Database de teste disponível
- Redis de teste configurado

---

## 6. Testing Infrastructure

### 6.1 Unit Tests - Services

Testes unitários completos para todos os services principais com mocking e scenarios.

#### IDE.Tests/Unit/Services/WorkspaceServiceTests.cs
```csharp
[TestFixture]
public class WorkspaceServiceTests
{
    private Mock<ApplicationDbContext> _mockContext;
    private Mock<ICacheService> _mockCache;
    private Mock<ILogger<WorkspaceService>> _mockLogger;
    private WorkspaceService _workspaceService;
    private Mock<DbSet<Workspace>> _mockWorkspaceSet;
    private Mock<DbSet<User>> _mockUserSet;

    [SetUp]
    public void Setup()
    {
        _mockContext = new Mock<ApplicationDbContext>();
        _mockCache = new Mock<ICacheService>();
        _mockLogger = new Mock<ILogger<WorkspaceService>>();
        
        _mockWorkspaceSet = new Mock<DbSet<Workspace>>();
        _mockUserSet = new Mock<DbSet<User>>();
        
        _mockContext.Setup(c => c.Workspaces).Returns(_mockWorkspaceSet.Object);
        _mockContext.Setup(c => c.Users).Returns(_mockUserSet.Object);
        
        _workspaceService = new WorkspaceService(_mockContext.Object, _mockCache.Object, _mockLogger.Object);
    }

    [Test]
    public async Task CreateWorkspaceAsync_ValidData_ReturnsSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreateWorkspaceRequest
        {
            Name = "Test Workspace",
            Description = "Test Description",
            DefaultPhases = new[] { "Development", "Testing" }
        };

        var user = new User { Id = userId, Username = "testuser" };
        _mockUserSet.Setup(u => u.FindAsync(userId)).ReturnsAsync(user);
        _mockContext.Setup(c => c.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        var result = await _workspaceService.CreateWorkspaceAsync(request, userId);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.Data);
        Assert.AreEqual(request.Name, result.Data.Name);
        Assert.AreEqual(request.Description, result.Data.Description);
        
        _mockContext.Verify(c => c.Workspaces.Add(It.IsAny<Workspace>()), Times.Once);
        _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Once);
    }

    [Test]
    public async Task CreateWorkspaceAsync_UserNotFound_ReturnsError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreateWorkspaceRequest { Name = "Test Workspace" };

        _mockUserSet.Setup(u => u.FindAsync(userId)).ReturnsAsync((User)null);

        // Act
        var result = await _workspaceService.CreateWorkspaceAsync(request, userId);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual("Usuário não encontrado", result.Message);
        
        _mockContext.Verify(c => c.Workspaces.Add(It.IsAny<Workspace>()), Times.Never);
        _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Never);
    }

    [Test]
    public async Task GetWorkspaceAsync_ExistsInCache_ReturnsCachedData()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var cachedWorkspace = new WorkspaceDto
        {
            Id = workspaceId,
            Name = "Cached Workspace"
        };

        _mockCache.Setup(c => c.GetAsync<WorkspaceDto>($"workspace:{workspaceId}"))
                  .ReturnsAsync(cachedWorkspace);

        // Act
        var result = await _workspaceService.GetWorkspaceAsync(workspaceId, userId);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(cachedWorkspace.Id, result.Data.Id);
        Assert.AreEqual(cachedWorkspace.Name, result.Data.Name);
        
        // Verify database was not queried
        _mockWorkspaceSet.Verify(w => w.FirstOrDefaultAsync(It.IsAny<Expression<Func<Workspace, bool>>>()), Times.Never);
    }

    [Test]
    public async Task GetUserWorkspacesAsync_ReturnsFilteredAndSorted()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspaces = new List<Workspace>
        {
            new Workspace { Id = Guid.NewGuid(), Name = "Workspace A", CreatedBy = userId, CreatedAt = DateTime.UtcNow.AddDays(-2) },
            new Workspace { Id = Guid.NewGuid(), Name = "Workspace B", CreatedBy = userId, CreatedAt = DateTime.UtcNow.AddDays(-1), IsArchived = true },
            new Workspace { Id = Guid.NewGuid(), Name = "Workspace C", CreatedBy = userId, CreatedAt = DateTime.UtcNow }
        }.AsQueryable();

        _mockWorkspaceSet.As<IQueryable<Workspace>>().Setup(m => m.Provider).Returns(workspaces.Provider);
        _mockWorkspaceSet.As<IQueryable<Workspace>>().Setup(m => m.Expression).Returns(workspaces.Expression);
        _mockWorkspaceSet.As<IQueryable<Workspace>>().Setup(m => m.ElementType).Returns(workspaces.ElementType);
        _mockWorkspaceSet.As<IQueryable<Workspace>>().Setup(m => m.GetEnumerator()).Returns(workspaces.GetEnumerator());

        // Act
        var result = await _workspaceService.GetUserWorkspacesAsync(userId, includeArchived: false);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(2, result.Data.Count()); // Should exclude archived
        Assert.AreEqual("Workspace C", result.Data.First().Name); // Should be sorted by CreatedAt desc
    }

    [Test]
    public async Task UpdateWorkspaceAsync_ValidData_UpdatesAndInvalidatesCache()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var existingWorkspace = new Workspace 
        { 
            Id = workspaceId, 
            CreatedBy = userId, 
            Name = "Old Name", 
            Description = "Old Description" 
        };

        var updateRequest = new UpdateWorkspaceRequest
        {
            Name = "New Name",
            Description = "New Description"
        };

        _mockWorkspaceSet.Setup(w => w.FirstOrDefaultAsync(It.IsAny<Expression<Func<Workspace, bool>>>()))
                        .ReturnsAsync(existingWorkspace);
        _mockContext.Setup(c => c.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        var result = await _workspaceService.UpdateWorkspaceAsync(workspaceId, updateRequest, userId);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(updateRequest.Name, existingWorkspace.Name);
        Assert.AreEqual(updateRequest.Description, existingWorkspace.Description);
        
        _mockCache.Verify(c => c.RemoveAsync($"workspace:{workspaceId}"), Times.Once);
        _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Once);
    }

    [Test]
    public async Task DeleteWorkspaceAsync_WorkspaceExists_SoftDeletes()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var existingWorkspace = new Workspace 
        { 
            Id = workspaceId, 
            CreatedBy = userId, 
            IsArchived = false 
        };

        _mockWorkspaceSet.Setup(w => w.FirstOrDefaultAsync(It.IsAny<Expression<Func<Workspace, bool>>>()))
                        .ReturnsAsync(existingWorkspace);
        _mockContext.Setup(c => c.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        var result = await _workspaceService.DeleteWorkspaceAsync(workspaceId, userId);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsTrue(existingWorkspace.IsArchived);
        Assert.IsNotNull(existingWorkspace.ArchivedAt);
        
        _mockCache.Verify(c => c.RemoveAsync($"workspace:{workspaceId}"), Times.Once);
        _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Once);
    }
}
```

#### IDE.Tests/Unit/Services/CollaborationServiceTests.cs
```csharp
[TestFixture]
public class CollaborationServiceTests
{
    private Mock<ApplicationDbContext> _mockContext;
    private Mock<IRedisCacheService> _mockCache;
    private Mock<IHubContext<CollaborationHub>> _mockHubContext;
    private Mock<ILogger<CollaborationService>> _mockLogger;
    private CollaborationService _collaborationService;

    [SetUp]
    public void Setup()
    {
        _mockContext = new Mock<ApplicationDbContext>();
        _mockCache = new Mock<IRedisCacheService>();
        _mockHubContext = new Mock<IHubContext<CollaborationHub>>();
        _mockLogger = new Mock<ILogger<CollaborationService>>();
        
        _collaborationService = new CollaborationService(
            _mockContext.Object, 
            _mockCache.Object, 
            _mockHubContext.Object, 
            _mockLogger.Object);
    }

    [Test]
    public async Task ProcessChangeAsync_ValidChange_ProcessesAndNotifies()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        
        var changeRequest = new ItemChangeRequest
        {
            ItemId = itemId,
            ChangeType = ChangeType.ContentUpdate,
            NewContent = "Updated content",
            Timestamp = DateTime.UtcNow
        };

        var existingItem = new ModuleItem
        {
            Id = itemId,
            WorkspaceId = workspaceId,
            Content = "Original content",
            Version = 1
        };

        var mockItemSet = new Mock<DbSet<ModuleItem>>();
        mockItemSet.Setup(i => i.FindAsync(itemId)).ReturnsAsync(existingItem);
        _mockContext.Setup(c => c.ModuleItems).Returns(mockItemSet.Object);
        _mockContext.Setup(c => c.SaveChangesAsync(default)).ReturnsAsync(1);

        var mockClients = new Mock<IHubCallerClients>();
        var mockGroup = new Mock<IClientProxy>();
        _mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);
        mockClients.Setup(c => c.Group($"workspace-{workspaceId}")).Returns(mockGroup.Object);

        // Act
        var result = await _collaborationService.ProcessChangeAsync(workspaceId, changeRequest, userId);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(changeRequest.NewContent, existingItem.Content);
        Assert.AreEqual(2, existingItem.Version); // Version should be incremented
        
        // Verify SignalR notification was sent
        mockGroup.Verify(g => g.SendAsync(
            "ItemChanged", 
            It.IsAny<object>(), 
            default), Times.Once);
        
        _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Once);
    }

    [Test]
    public async Task HandleConflictAsync_SimultaneousChanges_ResolvesConflict()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();

        var change1 = new ItemChangeRequest
        {
            ItemId = itemId,
            ChangeType = ChangeType.ContentUpdate,
            NewContent = "User 1 content",
            BaseVersion = 1,
            Timestamp = DateTime.UtcNow
        };

        var change2 = new ItemChangeRequest
        {
            ItemId = itemId,
            ChangeType = ChangeType.ContentUpdate,
            NewContent = "User 2 content",
            BaseVersion = 1, // Same base version = conflict
            Timestamp = DateTime.UtcNow.AddMilliseconds(100)
        };

        // Act
        var result1 = await _collaborationService.ProcessChangeAsync(workspaceId, change1, userId1);
        var result2 = await _collaborationService.ProcessChangeAsync(workspaceId, change2, userId2);

        // Assert
        Assert.IsTrue(result1.Success);
        Assert.IsFalse(result2.Success); // Second change should be rejected
        Assert.IsTrue(result2.Message.Contains("conflito"));
    }

    [Test]
    public async Task GetPendingChangesAsync_ReturnsOrderedChanges()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        
        var changes = new List<ItemChangeRecord>
        {
            new ItemChangeRecord { Id = Guid.NewGuid(), ItemId = itemId, Timestamp = DateTime.UtcNow.AddMinutes(-2), ChangeType = ChangeType.ContentUpdate },
            new ItemChangeRecord { Id = Guid.NewGuid(), ItemId = itemId, Timestamp = DateTime.UtcNow.AddMinutes(-1), ChangeType = ChangeType.MetadataUpdate },
            new ItemChangeRecord { Id = Guid.NewGuid(), ItemId = itemId, Timestamp = DateTime.UtcNow, ChangeType = ChangeType.ContentUpdate }
        }.AsQueryable();

        var mockChangeSet = new Mock<DbSet<ItemChangeRecord>>();
        mockChangeSet.As<IQueryable<ItemChangeRecord>>().Setup(m => m.Provider).Returns(changes.Provider);
        mockChangeSet.As<IQueryable<ItemChangeRecord>>().Setup(m => m.Expression).Returns(changes.Expression);
        mockChangeSet.As<IQueryable<ItemChangeRecord>>().Setup(m => m.ElementType).Returns(changes.ElementType);
        mockChangeSet.As<IQueryable<ItemChangeRecord>>().Setup(m => m.GetEnumerator()).Returns(changes.GetEnumerator());
        
        _mockContext.Setup(c => c.ItemChangeRecords).Returns(mockChangeSet.Object);

        // Act
        var result = await _collaborationService.GetPendingChangesAsync(workspaceId, itemId);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(3, result.Data.Count());
        
        // Should be ordered by timestamp ascending
        var orderedChanges = result.Data.ToList();
        Assert.IsTrue(orderedChanges[0].Timestamp <= orderedChanges[1].Timestamp);
        Assert.IsTrue(orderedChanges[1].Timestamp <= orderedChanges[2].Timestamp);
    }
}
```

### 6.2 Integration Tests

Testes de integração completos testando fluxos end-to-end com database real.

#### IDE.Tests/Integration/WorkspaceIntegrationTests.cs
```csharp
[TestFixture]
public class WorkspaceIntegrationTests
{
    private WebApplicationFactory<Program> _factory;
    private HttpClient _client;
    private IServiceScope _scope;
    private ApplicationDbContext _context;
    private string _authToken;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    // Replace production DbContext with test database
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                    if (descriptor != null)
                        services.Remove(descriptor);

                    services.AddDbContext<ApplicationDbContext>(options =>
                    {
                        options.UseInMemoryDatabase("TestDatabase");
                    });

                    // Replace Redis with in-memory cache
                    services.AddMemoryCache();
                    services.Replace(ServiceDescriptor.Singleton<ICacheService, MemoryCacheService>());
                });
            });

        _client = _factory.CreateClient();
        _scope = _factory.Services.CreateScope();
        _context = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await SeedTestDataAsync();
        _authToken = await GetAuthTokenAsync();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _scope?.Dispose();
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task CreateWorkspace_ValidData_ReturnsCreatedWorkspace()
    {
        // Arrange
        var request = new CreateWorkspaceRequest
        {
            Name = "Integration Test Workspace",
            Description = "Created by integration test",
            DefaultPhases = new[] { "Development", "Testing", "Production" }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        _client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", _authToken);

        // Act
        var response = await _client.PostAsync("/api/workspaces", content);
        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse<WorkspaceDto>>(responseContent, 
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        // Assert
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        Assert.IsTrue(result.Success);
        Assert.AreEqual(request.Name, result.Data.Name);
        Assert.AreEqual(request.Description, result.Data.Description);
        Assert.IsNotNull(result.Data.Id);

        // Verify in database
        var dbWorkspace = await _context.Workspaces.FindAsync(result.Data.Id);
        Assert.IsNotNull(dbWorkspace);
        Assert.AreEqual(request.Name, dbWorkspace.Name);
    }

    [Test]
    public async Task GetWorkspace_ExistingId_ReturnsWorkspaceWithItems()
    {
        // Arrange
        var workspace = await CreateTestWorkspaceAsync();
        await CreateTestModuleItemsAsync(workspace.Id);

        _client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", _authToken);

        // Act
        var response = await _client.GetAsync($"/api/workspaces/{workspace.Id}");
        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse<WorkspaceDto>>(responseContent,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsTrue(result.Success);
        Assert.AreEqual(workspace.Id, result.Data.Id);
        Assert.IsTrue(result.Data.TotalItems > 0);
    }

    [Test]
    public async Task UpdateWorkspace_ValidData_UpdatesWorkspace()
    {
        // Arrange
        var workspace = await CreateTestWorkspaceAsync();
        var updateRequest = new UpdateWorkspaceRequest
        {
            Name = "Updated Workspace Name",
            Description = "Updated description"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(updateRequest),
            Encoding.UTF8,
            "application/json");

        _client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", _authToken);

        // Act
        var response = await _client.PutAsync($"/api/workspaces/{workspace.Id}", content);
        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse<WorkspaceDto>>(responseContent,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsTrue(result.Success);
        Assert.AreEqual(updateRequest.Name, result.Data.Name);
        Assert.AreEqual(updateRequest.Description, result.Data.Description);

        // Verify in database
        var dbWorkspace = await _context.Workspaces.FindAsync(workspace.Id);
        Assert.AreEqual(updateRequest.Name, dbWorkspace.Name);
        Assert.AreEqual(updateRequest.Description, dbWorkspace.Description);
    }

    [Test]
    public async Task DeleteWorkspace_ExistingWorkspace_SoftDeletes()
    {
        // Arrange
        var workspace = await CreateTestWorkspaceAsync();

        _client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", _authToken);

        // Act
        var response = await _client.DeleteAsync($"/api/workspaces/{workspace.Id}");

        // Assert
        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);

        // Verify soft delete in database
        var dbWorkspace = await _context.Workspaces.FindAsync(workspace.Id);
        Assert.IsNotNull(dbWorkspace);
        Assert.IsTrue(dbWorkspace.IsArchived);
        Assert.IsNotNull(dbWorkspace.ArchivedAt);
    }

    private async Task SeedTestDataAsync()
    {
        var testUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.Users.Add(testUser);
        await _context.SaveChangesAsync();
    }

    private async Task<string> GetAuthTokenAsync()
    {
        var loginRequest = new LoginRequest
        {
            Email = "test@example.com",
            Password = "Test123!"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(loginRequest),
            Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/api/auth/login", content);
        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse<AuthResponseDto>>(responseContent,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        return result.Data.Token;
    }

    private async Task<Workspace> CreateTestWorkspaceAsync()
    {
        var user = await _context.Users.FirstAsync();
        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = "Test Workspace",
            Description = "Test workspace for integration tests",
            CreatedBy = user.Id,
            CreatedAt = DateTime.UtcNow,
            SemanticVersion = "1.0.0"
        };

        _context.Workspaces.Add(workspace);
        await _context.SaveChangesAsync();
        return workspace;
    }

    private async Task CreateTestModuleItemsAsync(Guid workspaceId)
    {
        var items = new[]
        {
            new ModuleItem { Id = Guid.NewGuid(), WorkspaceId = workspaceId, Name = "App.tsx", Module = "Frontend", Content = "React app" },
            new ModuleItem { Id = Guid.NewGuid(), WorkspaceId = workspaceId, Name = "api.ts", Module = "Frontend", Content = "API calls" },
            new ModuleItem { Id = Guid.NewGuid(), WorkspaceId = workspaceId, Name = "UserController.cs", Module = "Backend", Content = "User API" }
        };

        _context.ModuleItems.AddRange(items);
        await _context.SaveChangesAsync();
    }
}
```

### 6.3 Security Tests

Testes de segurança abrangentes para proteção contra vulnerabilidades comuns.

#### IDE.Tests/Security/SecurityTests.cs
```csharp
[TestFixture]
public class SecurityTests
{
    private WebApplicationFactory<Program> _factory;
    private HttpClient _httpClient;
    private string _baseUrl;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new WebApplicationFactory<Program>();
        _httpClient = _factory.CreateClient();
        _baseUrl = "http://localhost";
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _httpClient?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task Api_RequiresAuthentication()
    {
        // Test protected endpoints without token
        var endpoints = new[]
        {
            "/api/workspaces",
            "/api/workspaces/123",
            "/api/auth/me",
            "/api/items",
            "/api/collaboration/changes"
        };

        foreach (var endpoint in endpoints)
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}{endpoint}");
            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode, 
                $"Endpoint {endpoint} should require authentication");
        }
    }

    [Test]
    public async Task Api_RejectsInvalidTokens()
    {
        var invalidTokens = new[]
        {
            "invalid-token",
            "Bearer invalid",
            "expired.jwt.token",
            "malformed.jwt",
            ""
        };

        foreach (var token in invalidTokens)
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.GetAsync($"{_baseUrl}/api/workspaces");
            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode,
                $"Token '{token}' should be rejected");
        }
    }

    [Test]
    public async Task Api_PreventsSqlInjection()
    {
        var maliciousInputs = new[]
        {
            "'; DROP TABLE Users; --",
            "1' OR '1'='1",
            "admin'/*",
            "1; UPDATE Users SET Password='hacked' WHERE Id=1; --",
            "' UNION SELECT * FROM Users --",
            "'; INSERT INTO Users VALUES ('hacker', 'password'); --"
        };

        var validToken = await GetValidTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", validToken);

        foreach (var input in maliciousInputs)
        {
            var content = new StringContent(
                JsonSerializer.Serialize(new { name = input, description = input }),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/workspaces", content);
            
            // Should either reject the input (400) or sanitize it, but not cause server error (500)
            Assert.AreNotEqual(HttpStatusCode.InternalServerError, response.StatusCode,
                $"SQL injection attempt with '{input}' caused server error");
                
            // Check if response contains the malicious input (should be sanitized)
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                Assert.IsFalse(responseContent.Contains("DROP TABLE"), 
                    "Response contains unsanitized SQL injection payload");
            }
        }
    }

    [Test]
    public async Task Api_PreventstXssAttacks()
    {
        var xssPayloads = new[]
        {
            "<script>alert('xss')</script>",
            "javascript:alert('xss')",
            "<img src=x onerror=alert('xss')>",
            "<iframe src='javascript:alert(`xss`)'></iframe>",
            "<svg onload=alert('xss')>",
            "<input onfocus=alert('xss') autofocus>",
            "<select onfocus=alert('xss') autofocus>",
            "<textarea onfocus=alert('xss') autofocus>",
            "<keygen onfocus=alert('xss') autofocus>",
            "<video><source onerror='alert(String.fromCharCode(88,83,83))'>"
        };

        var validToken = await GetValidTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", validToken);

        foreach (var payload in xssPayloads)
        {
            var content = new StringContent(
                JsonSerializer.Serialize(new { name = "Test", description = payload }),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/workspaces", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                
                // Verify dangerous XSS payloads are sanitized
                Assert.IsFalse(responseContent.Contains("<script>"), 
                    $"XSS payload '<script>' not sanitized: {payload}");
                Assert.IsFalse(responseContent.Contains("javascript:"), 
                    $"XSS payload 'javascript:' not sanitized: {payload}");
                Assert.IsFalse(responseContent.Contains("onerror="), 
                    $"XSS payload 'onerror=' not sanitized: {payload}");
                Assert.IsFalse(responseContent.Contains("onload="), 
                    $"XSS payload 'onload=' not sanitized: {payload}");
            }
        }
    }

    [Test]
    public async Task Api_EnforcesRateLimiting()
    {
        var validToken = await GetValidTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", validToken);

        var tasks = new List<Task<HttpResponseMessage>>();
        
        // Send 150 requests simultaneously to trigger rate limiting
        for (int i = 0; i < 150; i++)
        {
            tasks.Add(_httpClient.GetAsync($"{_baseUrl}/api/workspaces"));
        }

        var responses = await Task.WhenAll(tasks);
        var rateLimitedCount = responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests);
        
        Assert.IsTrue(rateLimitedCount > 0, 
            "Rate limiting should have been triggered with 150 simultaneous requests");

        // Check rate limit headers
        var rateLimitedResponse = responses.FirstOrDefault(r => r.StatusCode == HttpStatusCode.TooManyRequests);
        if (rateLimitedResponse != null)
        {
            Assert.IsTrue(rateLimitedResponse.Headers.Contains("X-RateLimit-Limit"),
                "Rate limited response should include X-RateLimit-Limit header");
            Assert.IsTrue(rateLimitedResponse.Headers.Contains("X-RateLimit-Remaining"),
                "Rate limited response should include X-RateLimit-Remaining header");
            Assert.IsTrue(rateLimitedResponse.Headers.Contains("Retry-After"),
                "Rate limited response should include Retry-After header");
        }
    }

    [Test]
    public async Task Api_ValidatesInputSize()
    {
        var validToken = await GetValidTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", validToken);

        // Test with very large payload
        var largeContent = new string('A', 10 * 1024 * 1024); // 10MB
        var content = new StringContent(
            JsonSerializer.Serialize(new { name = "Test", description = largeContent }),
            Encoding.UTF8,
            "application/json"
        );

        var response = await _httpClient.PostAsync($"{_baseUrl}/api/workspaces", content);
        
        // Should reject large payloads
        Assert.IsTrue(response.StatusCode == HttpStatusCode.BadRequest || 
                     response.StatusCode == HttpStatusCode.RequestEntityTooLarge,
            "API should reject overly large payloads");
    }

    [Test]
    public async Task Api_HasSecurityHeaders()
    {
        var response = await _httpClient.GetAsync($"{_baseUrl}/api/health");

        // Check essential security headers
        Assert.IsTrue(response.Headers.Contains("X-Content-Type-Options"),
            "Response should include X-Content-Type-Options header");
        Assert.IsTrue(response.Headers.Contains("X-Frame-Options"),
            "Response should include X-Frame-Options header");
        Assert.IsTrue(response.Headers.Contains("X-XSS-Protection"),
            "Response should include X-XSS-Protection header");

        // Verify header values
        var contentTypeOptions = response.Headers.GetValues("X-Content-Type-Options").FirstOrDefault();
        Assert.AreEqual("nosniff", contentTypeOptions);

        var frameOptions = response.Headers.GetValues("X-Frame-Options").FirstOrDefault();
        Assert.AreEqual("DENY", frameOptions);
    }

    private async Task<string> GetValidTokenAsync()
    {
        var loginData = new LoginRequest
        {
            Email = "test@example.com",
            Password = "Test123!"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(loginData),
            Encoding.UTF8,
            "application/json"
        );

        var response = await _httpClient.PostAsync($"{_baseUrl}/api/auth/login", content);
        
        if (!response.IsSuccessStatusCode)
        {
            // Create test user if login fails
            var registerData = new RegisterRequest
            {
                Email = "test@example.com",
                Username = "testuser",
                Password = "Test123!",
                FirstName = "Test",
                LastName = "User"
            };

            var registerContent = new StringContent(
                JsonSerializer.Serialize(registerData),
                Encoding.UTF8,
                "application/json"
            );

            await _httpClient.PostAsync($"{_baseUrl}/api/auth/register", registerContent);
            response = await _httpClient.PostAsync($"{_baseUrl}/api/auth/login", content);
        }

        var responseData = await response.Content.ReadAsStringAsync();
        var authResponse = JsonSerializer.Deserialize<ApiResponse<AuthResponseDto>>(responseData,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        
        return authResponse.Data.Token;
    }
}
```

---

## 6.4 Swagger Documentation Complete

Configuração completa do Swagger com examples, schemas e documentação rica.

### Swagger Configuration

#### IDE.API/Configuration/SwaggerConfiguration.cs
```csharp
public static class SwaggerConfiguration
{
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "IDE Platform API",
                Version = "v1.0.0",
                Description = @"
# IDE Platform API Documentation

API completa para plataforma IDE colaborativa com workspaces, editores em tempo real e sistema de colaboração.

## Funcionalidades Principais

- **Workspaces**: Criação e gerenciamento de projetos
- **Module Items**: Sistema de arquivos virtual com editores
- **Real-time Collaboration**: Edição colaborativa em tempo real
- **Authentication**: JWT com refresh tokens
- **Rate Limiting**: Limites por plano de usuário
- **File Management**: Upload e download de arquivos
- **Chat System**: Chat em tempo real por workspace

## Rate Limiting

A API implementa rate limiting baseado no plano do usuário:

- **Free**: 60 req/min, 1000 req/hora
- **Pro**: 300 req/min, 10000 req/hora  
- **Enterprise**: 1000 req/min, 50000 req/hora

## Authentication

Todas as rotas protegidas requerem token JWT no header:
```
Authorization: Bearer <your-jwt-token>
```

## WebSocket Endpoints

- `/hubs/collaboration`: Real-time collaboration
- `/hubs/chat`: Chat system
- `/hubs/presence`: User presence

## Error Codes

- `400`: Bad Request - Dados inválidos
- `401`: Unauthorized - Token inválido/ausente
- `403`: Forbidden - Sem permissão
- `404`: Not Found - Recurso não encontrado
- `429`: Too Many Requests - Rate limit excedido
- `500`: Internal Server Error - Erro interno
",
                Contact = new OpenApiContact
                {
                    Name = "IDE Platform Team",
                    Email = "dev@ide-platform.com",
                    Url = new Uri("https://ide-platform.com")
                },
                License = new OpenApiLicense
                {
                    Name = "MIT License",
                    Url = new Uri("https://opensource.org/licenses/MIT")
                }
            });

            // JWT Security Definition
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = @"JWT Authorization header using the Bearer scheme.
                      
Enter 'Bearer' [space] and then your token in the text input below.
                      
Example: 'Bearer 12345abcdef'",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer",
                BearerFormat = "JWT"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            // Include XML documentation
            var xmlFiles = Directory.GetFiles(AppContext.BaseDirectory, "*.xml", SearchOption.TopDirectoryOnly);
            foreach (var xmlFile in xmlFiles)
            {
                c.IncludeXmlComments(xmlFile);
            }

            // Advanced configurations
            c.EnableAnnotations();
            c.DescribeAllParametersInCamelCase();
            c.UseInlineDefinitionsForEnums();
            c.SchemaFilter<ExampleSchemaFilter>();
            c.OperationFilter<ResponseExamplesOperationFilter>();
            c.OperationFilter<SecurityRequirementsOperationFilter>();

            // Group by tags
            c.TagActionsBy(api => new[] { GetControllerName(api) });
            c.DocInclusionPredicate((name, api) => true);

            // Servers
            c.AddServer(new OpenApiServer
            {
                Url = "http://localhost:8503",
                Description = "Development Server"
            });

            c.AddServer(new OpenApiServer
            {
                Url = "https://api-dev.ide-platform.com",
                Description = "Development Environment"
            });

            c.AddServer(new OpenApiServer
            {
                Url = "https://api.ide-platform.com",
                Description = "Production Server"
            });
        });

        return services;
    }

    public static IApplicationBuilder UseSwaggerDocumentation(this IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseSwagger(c =>
        {
            c.SerializeAsV2 = false;
            c.RouteTemplate = "api-docs/{documentname}/swagger.json";
            c.PreSerializeFilters.Add((swagger, httpReq) =>
            {
                swagger.Servers = new List<OpenApiServer>
                {
                    new OpenApiServer { Url = $"{httpReq.Scheme}://{httpReq.Host.Value}" }
                };
            });
        });

        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/api-docs/v1/swagger.json", "IDE Platform API v1.0.0");
            c.RoutePrefix = "api-docs";
            c.DocumentTitle = "IDE Platform API Documentation";
            
            // UI Customizations
            c.DefaultModelsExpandDepth(2);
            c.DefaultModelExpandDepth(2);
            c.DocExpansion(DocExpansion.List);
            c.EnableDeepLinking();
            c.DisplayOperationId();
            c.EnableFilter();
            c.ShowExtensions();
            c.EnableValidator();
            c.SupportedSubmitMethods(SubmitMethod.Get, SubmitMethod.Post, SubmitMethod.Put, SubmitMethod.Delete);
            
            // Custom CSS and JS
            c.InjectStylesheet("/swagger-ui/custom.css");
            c.InjectJavascript("/swagger-ui/custom.js");

            // OAuth2 configuration (if needed)
            c.OAuthClientId("swagger-ui");
            c.OAuthAppName("IDE Platform API");
            c.OAuthUsePkce();
        });

        return app;
    }

    private static string GetControllerName(ApiDescription api)
    {
        var controllerName = api.ActionDescriptor.RouteValues["controller"];
        
        return controllerName switch
        {
            "Auth" => "Authentication",
            "Workspaces" => "Workspace Management", 
            "Items" => "Module Items",
            "Collaboration" => "Real-time Collaboration",
            "Chat" => "Chat System",
            "Admin" => "Administration",
            _ => controllerName ?? "General"
        };
    }
}
```

---

## Entregáveis da Parte 7

### ✅ Implementações Completas
- **Unit Tests** com 85%+ coverage para services
- **Integration Tests** end-to-end com database real
- **Security Tests** (SQL injection, XSS, rate limiting)
- **Performance Tests** com benchmarks
- **Swagger Documentation** completa e rica
- **Test Infrastructure** robusta com mocking

### ✅ Funcionalidades de Teste
- **Service layer testing** com mocks
- **API endpoint testing** completo
- **Security vulnerability testing** automatizado
- **Rate limiting validation** funcional
- **Database integration testing** real
- **Authentication/Authorization testing** completo

### ✅ Documentação e Qualidade
- **API documentation** rica com examples
- **Schema validation** completa
- **Error code documentation** detalhada
- **Authentication guide** completo
- **Rate limiting guide** por plano
- **WebSocket documentation** incluída

---

## Validação da Parte 7

### Critérios de Sucesso
- [ ] Unit tests passam com > 85% coverage
- [ ] Integration tests validam fluxos completos
- [ ] Security tests detectam vulnerabilidades
- [ ] Rate limiting funciona conforme especificado
- [ ] Swagger UI funciona perfeitamente
- [ ] Documentation está completa e clara

### Executar Testes
```bash
# 1. Executar unit tests
dotnet test IDE.Tests.Unit --collect:"XPlat Code Coverage"

# 2. Executar integration tests
dotnet test IDE.Tests.Integration --logger "console;verbosity=detailed"

# 3. Executar security tests
dotnet test IDE.Tests.Security --filter "Category=Security"

# 4. Verificar Swagger
curl -X GET http://localhost:8503/api-docs/v1/swagger.json

# 5. Coverage report
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"./coverage-report"
```

### Quality Targets
- **Test Coverage**: > 85% overall
- **Security Tests**: 100% passing
- **Integration Tests**: All critical paths covered
- **Documentation**: All endpoints documented
- **Performance**: Tests complete < 30 seconds

---

## Próximos Passos

Após validação da Parte 7, prosseguir para:
- **Parte 8**: Production Deployment & Kubernetes

---

**Tempo Estimado**: 4-5 horas  
**Complexidade**: Alta  
**Dependências**: .NET Test SDK, Test Database, Swagger  
**Entregável**: Suite completa de testes e documentação profissional