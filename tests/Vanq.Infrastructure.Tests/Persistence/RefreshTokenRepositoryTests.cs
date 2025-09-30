using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vanq.Domain.Entities;
using Vanq.Infrastructure.Persistence;
using Vanq.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Vanq.Infrastructure.Tests.Persistence;

public class RefreshTokenRepositoryTests
{
    [Fact]
    public async Task GetByHashAsync_ShouldReturnToken_WhenStored()
    {
        await using var context = CreateContext();
        var repository = new RefreshTokenRepository(context);
        var userId = Guid.NewGuid();
        var plain = "plain-token";
        var hash = ComputeHash(plain);
        var now = DateTime.UtcNow;
        var token = RefreshToken.Issue(userId, hash, now, now.AddDays(1), "stamp");

        await repository.AddAsync(token, CancellationToken.None);
        await context.SaveChangesAsync();

        var fetched = await repository.GetByHashAsync(hash, CancellationToken.None);

        fetched.Should().NotBeNull();
        fetched!.TokenHash.Should().Be(hash);
    }

    [Fact]
    public async Task Update_ShouldPersistRevocation()
    {
        await using var context = CreateContext();
        var repository = new RefreshTokenRepository(context);
        var userId = Guid.NewGuid();
        var plain = "another-token";
        var hash = ComputeHash(plain);
        var now = DateTime.UtcNow;
        var token = RefreshToken.Issue(userId, hash, now, now.AddDays(1), "stamp");

        await repository.AddAsync(token, CancellationToken.None);
        await context.SaveChangesAsync();

        var tracked = await repository.GetByUserAndHashAsync(userId, hash, CancellationToken.None, track: true);
        tracked.Should().NotBeNull();

        tracked!.Revoke(nowUtc: now);
        repository.Update(tracked);
        await context.SaveChangesAsync();

        var refreshed = await repository.GetByUserAndHashAsync(userId, hash, CancellationToken.None);
        refreshed!.RevokedAt.Should().NotBeNull();
    }

    private static string ComputeHash(string token)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(token);
        var hashBytes = sha.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
