using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vanq.Domain.Entities;
using Vanq.Infrastructure.Persistence;
using Vanq.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Vanq.Infrastructure.Tests.Persistence;

public class UserRepositoryTests
{
    [Fact]
    public async Task AddAsync_ShouldPersistUserAndAllowQueries()
    {
        await using var context = CreateContext();
        var repository = new UserRepository(context);
        var now = DateTime.UtcNow;
        var user = User.Create("user@example.com", "hashed-password", now);

        await repository.AddAsync(user, CancellationToken.None);
        await context.SaveChangesAsync();

        var exists = await repository.ExistsByEmailAsync(user.Email, CancellationToken.None);
        exists.Should().BeTrue();

        var fetched = await repository.GetByEmailAsync(user.Email, CancellationToken.None);
        fetched.Should().NotBeNull();
        fetched!.Email.Should().Be(user.Email);
        fetched.PasswordHash.Should().Be("hashed-password");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNullForEmptyGuid()
    {
        await using var context = CreateContext();
        var repository = new UserRepository(context);

        var result = await repository.GetByIdAsync(Guid.Empty, CancellationToken.None);

        result.Should().BeNull();
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
