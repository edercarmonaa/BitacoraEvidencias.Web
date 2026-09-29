using System.Net;
using System.Security.Claims;
using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BitacoraEvidencias.Web.Tests.Services;

public class AuditLogServiceTests
{
    [Fact]
    public async Task WriteAsync_PersistsAuditLog_UsingHttpContextFallbackValues()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var httpContext = new DefaultHttpContext();
        httpContext.User = BuildPrincipal(userId: 11, username: "auditor", role: "Admin");
        httpContext.Request.Headers.UserAgent = "test-agent";
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var logger = new TestLogger<AuditLogService>();
        var service = new AuditLogService(dbContext, accessor, logger);
        var occurredAtLocal = new DateTime(2026, 3, 23, 10, 15, 0, DateTimeKind.Local);

        await service.WriteAsync(new AuditLogEntry
        {
            EventType = " Login ",
            EntityType = " Auth ",
            EntityId = 9,
            NumeroOficio = " OF-001 ",
            Details = " detalle ",
            Success = true,
            OccurredAtUtc = occurredAtLocal
        });

        var log = await dbContext.AuditLogs.SingleAsync();

        Assert.Equal("Login", log.EventType);
        Assert.Equal("Auth", log.EntityType);
        Assert.Equal(9, log.EntityId);
        Assert.Equal(11, log.UserId);
        Assert.Equal("auditor", log.Username);
        Assert.Equal("Admin", log.Role);
        Assert.Equal("127.0.0.1", log.IpAddress);
        Assert.Equal("test-agent", log.UserAgent);
        Assert.Equal("OF-001", log.NumeroOficio);
        Assert.Equal("detalle", log.Details);
        Assert.Equal(occurredAtLocal.ToUniversalTime(), log.OccurredAtUtc);
        Assert.Empty(logger.Errors);
    }

    [Fact]
    public async Task WriteAsync_DoesNothing_WhenEventTypeIsEmpty()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var service = new AuditLogService(
            dbContext,
            new HttpContextAccessor(),
            new TestLogger<AuditLogService>());

        await service.WriteAsync(new AuditLogEntry
        {
            EventType = "   "
        });

        Assert.Empty(await dbContext.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task WriteAsync_DoesNotThrow_WhenPersistenceFails_AndLogsError()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        await using var dbContext = new ThrowingAppDbContext(options);
        var logger = new TestLogger<AuditLogService>();
        var service = new AuditLogService(
            dbContext,
            new HttpContextAccessor(),
            logger);

        var exception = await Record.ExceptionAsync(() => service.WriteAsync(new AuditLogEntry
        {
            EventType = "FailureTest",
            EntityType = "Audit"
        }));

        Assert.Null(exception);
        Assert.Single(logger.Errors);
        Assert.Contains("No fue posible persistir la auditoria", logger.Errors[0], StringComparison.Ordinal);
    }

    private static ClaimsPrincipal BuildPrincipal(int userId, string username, string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, username),
            new(ClaimTypes.Role, role)
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private sealed class ThrowingAppDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated failure");
        }
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<string> Errors { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error)
            {
                Errors.Add(formatter(state, exception));
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose()
            {
            }
        }
    }
}
