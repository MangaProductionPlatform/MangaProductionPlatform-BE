using Npgsql;
using STTask = System.Threading.Tasks.Task;
using MangaERP.Shared.Infrastructure.Persistence;
using MangaERP.Shared.Infrastructure.Repositories;
using MangaERP.Shared.Application.Ports;
using Microsoft.EntityFrameworkCore;
using MangaERP.Shared.Domain.Exceptions;
using MangaERP.Studio.Domain.Entities;

namespace MangaERP.Api.Tests;

[Collection("Phase1 PostgreSQL")]
public sealed class PostgreSqlPhase1Tests
{
    private readonly string _connectionString = Environment.GetEnvironmentVariable("PHASE1_POSTGRES_CONNECTION")
        ?? throw new InvalidOperationException("PHASE1_POSTGRES_CONNECTION is required for PostgreSQL integration tests.");

    private async System.Threading.Tasks.Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    [Fact]
    public async STTask ProviderAndPhase1SchemaArePostgreSql()
    {
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand("SELECT version()", connection);
        Assert.Contains("PostgreSQL", (string?)await command.ExecuteScalarAsync(), StringComparison.OrdinalIgnoreCase);

        await using var schema = new NpgsqlCommand("SELECT to_regclass('public.\"MangakaAssistantCollaborations\"')::text", connection);
        Assert.Equal("\"MangakaAssistantCollaborations\"", (string?)await schema.ExecuteScalarAsync());
    }

    [Fact]
    public async STTask PartialIndexesAndCheckConstraintsAreEnforced()
    {
        await using var connection = await OpenAsync();
        var ids = await SeedUsersAndInvitationAsync(connection);
        var collaborationId = Guid.NewGuid();

        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, $"""
            INSERT INTO "MangakaAssistantCollaborations"
              ("Id","MangakaId","AssistantId","InvitationId","Status","SuspensionMode","StartedAt","CreatedAt","UpdatedAt","ConcurrencyToken")
            VALUES ('{collaborationId}','{ids.mangaka}','{ids.assistant}','{ids.invitation}','Suspended',NULL,now(),now(),now(),'{Guid.NewGuid()}')
            """));

        await ExecuteAsync(connection, $"""
            INSERT INTO "MangakaAssistantCollaborations"
              ("Id","MangakaId","AssistantId","InvitationId","Status","StartedAt","CreatedAt","UpdatedAt","ConcurrencyToken")
            VALUES ('{collaborationId}','{ids.mangaka}','{ids.assistant}','{ids.invitation}','Active',now(),now(),now(),'{Guid.NewGuid()}')
            """);

        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, $"""
            INSERT INTO "MangakaAssistantCollaborations"
              ("Id","MangakaId","AssistantId","InvitationId","Status","StartedAt","CreatedAt","UpdatedAt","ConcurrencyToken")
            VALUES ('{Guid.NewGuid()}','{Guid.NewGuid()}','{ids.assistant}','{Guid.NewGuid()}','EndingRequested',now(),now(),now(),'{Guid.NewGuid()}')
            """));
    }

    [Fact]
    public async STTask ConcurrentNonEndedCollaborationRaceAllowsExactlyOne()
    {
        await using var setup = await OpenAsync();
        var ids = await SeedUsersAndInvitationAsync(setup);
        var barrier = new Barrier(2);

        async System.Threading.Tasks.Task<bool> TryInsert(Guid invitationId)
        {
            await using var connection = await OpenAsync();
            await using var tx = await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            await System.Threading.Tasks.Task.Run(() => barrier.SignalAndWait());
            try
            {
                await using var command = new NpgsqlCommand($"""
                    INSERT INTO "MangakaAssistantCollaborations"
                      ("Id","MangakaId","AssistantId","InvitationId","Status","StartedAt","CreatedAt","UpdatedAt","ConcurrencyToken")
                    VALUES ('{Guid.NewGuid()}','{ids.mangaka}','{ids.assistant}','{invitationId}','Active',now(),now(),now(),'{Guid.NewGuid()}')
                    """, connection, tx);
                await command.ExecuteNonQueryAsync();
                await tx.CommitAsync();
                return true;
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                await tx.RollbackAsync();
                return false;
            }
        }

        var results = await System.Threading.Tasks.Task.WhenAll(TryInsert(ids.invitation), TryInsert(ids.invitation2));
        Assert.Equal(1, results.Count(x => x));
        Assert.Equal(1, results.Count(x => !x));
    }

    [Fact]
    public async STTask RollbackLeavesNoCollaborationOrEvent()
    {
        await using var connection = await OpenAsync();
        var ids = await SeedUsersAndInvitationAsync(connection);
        var collaborationId = Guid.NewGuid();
        await using var tx = await connection.BeginTransactionAsync();
        await ExecuteAsync(connection, $"""
            INSERT INTO "MangakaAssistantCollaborations"
              ("Id","MangakaId","AssistantId","InvitationId","Status","StartedAt","CreatedAt","UpdatedAt","ConcurrencyToken")
            VALUES ('{collaborationId}','{ids.mangaka}','{ids.assistant}','{ids.invitation}','Active',now(),now(),now(),'{Guid.NewGuid()}')
            """, tx);
        await ExecuteAsync(connection, $"INSERT INTO \"CollaborationEvents\" (\"Id\",\"CollaborationId\",\"EventType\",\"ActorUserId\",\"OccurredAt\") VALUES ('{Guid.NewGuid()}','{collaborationId}','CollaborationActivated','{ids.assistant}',now())", tx);
        await tx.RollbackAsync();

        await using var check = new NpgsqlCommand($"SELECT count(*) FROM \"MangakaAssistantCollaborations\" WHERE \"Id\"='{collaborationId}'", connection);
        Assert.Equal(0L, (long)await check.ExecuteScalarAsync()!);
    }

    [Fact]
    public async STTask ConcurrentInvitationAcceptanceAllowsOneWinner()
    {
        await using var connection = await OpenAsync();
        var ids = await SeedPendingAcceptanceScenarioAsync(connection);
        var barrier = new Barrier(2);

        async System.Threading.Tasks.Task<Exception?> Accept(Guid invitationId)
        {
            await System.Threading.Tasks.Task.Run(() => barrier.SignalAndWait());
            await using var db = CreateDbContext();
            var repo = new StudioInvitationRepository(new TestDbContextProvider(db));
            try
            {
                await repo.AcceptInvitationAsync(invitationId, ids.assistant, ids.assistant, DateTime.UtcNow, "pg-test");
                return null;
            }
            catch (Exception ex) { return ex; }
        }

        var results = await System.Threading.Tasks.Task.WhenAll(Accept(ids.invitation1), Accept(ids.invitation2));
        Assert.Equal(1, results.Count(x => x is null));
        Assert.Equal(1, results.Count(x => x is ConflictException));

        await using var verify = await OpenAsync();
        await using var count = new NpgsqlCommand("SELECT count(*) FROM \"MangakaAssistantCollaborations\" WHERE \"AssistantId\" = $1 AND \"Status\" <> 'Ended'", verify);
        count.Parameters.AddWithValue(ids.assistant);
        Assert.Equal(1L, (long)await count.ExecuteScalarAsync()!);
    }

    [Fact]
    public async STTask StaleCollaborationUpdateRaisesEfConcurrencyException()
    {
        await using var connection = await OpenAsync();
        var ids = await SeedUsersAndInvitationAsync(connection);
        var collaborationId = Guid.NewGuid();
        await ExecuteAsync(connection, $"""
            INSERT INTO "MangakaAssistantCollaborations"
              ("Id","MangakaId","AssistantId","InvitationId","Status","StartedAt","CreatedAt","UpdatedAt","ConcurrencyToken")
            VALUES ('{collaborationId}','{ids.mangaka}','{ids.assistant}','{ids.invitation}','Active',now(),now(),now(),'{Guid.NewGuid()}')
            """);

        await using var first = CreateDbContext();
        await using var second = CreateDbContext();
        var left = await first.MangakaAssistantCollaborations.SingleAsync(x => x.Id == collaborationId);
        var right = await second.MangakaAssistantCollaborations.SingleAsync(x => x.Id == collaborationId);
        left.Suspend(CollaborationSuspensionMode.SuspendNewAssignments, "first", DateTime.UtcNow);
        right.Suspend(CollaborationSuspensionMode.SuspendAllAccess, "stale", DateTime.UtcNow);
        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    private static async STTask ExecuteAsync(NpgsqlConnection connection, string sql, NpgsqlTransaction? transaction = null)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync();
    }

    private static async System.Threading.Tasks.Task<(Guid mangaka, Guid assistant, Guid invitation, Guid invitation2)> SeedUsersAndInvitationAsync(NpgsqlConnection connection)
    {
        var mangaka = Guid.NewGuid();
        var assistant = Guid.NewGuid();
        var invitation = Guid.NewGuid();
        var invitation2 = Guid.NewGuid();
        await ExecuteAsync(connection, $"""
            INSERT INTO "Users" ("Id","Username","Email","PasswordHash","Role","AccountStatus","IsDeleted","CreatedAt") VALUES
              ('{mangaka}','mgk_{mangaka}@test.local','mgk_{mangaka}@test.local','x','Mangaka','Active',false,now()),
              ('{assistant}','ast_{assistant}@test.local','ast_{assistant}@test.local','x','Assistant','Active',false,now())
            """);
        await ExecuteAsync(connection, $"""
            INSERT INTO "StudioInvitations"
              ("Id","SeriesId","InviterMangakaId","AssistantUserId","AssistantEmail","NormalizedAssistantEmail","Status","IsNewAccountFlow","RegistrationDeliveryStatus","CreatedAt","ExpiresAt")
            VALUES ('{invitation}','{Guid.NewGuid()}','{mangaka}','{assistant}','ast_{assistant}@test.local','ast_{assistant}@test.local','Accepted',false,'NotRequired',now(),now()+interval '1 day'),
                   ('{invitation2}','{Guid.NewGuid()}','{mangaka}','{assistant}','ast_{assistant}@test.local','ast_{assistant}@test.local','Accepted',false,'NotRequired',now(),now()+interval '1 day')
            """);
        return (mangaka, assistant, invitation, invitation2);
    }

    private static async System.Threading.Tasks.Task<(Guid assistant, Guid invitation1, Guid invitation2)> SeedPendingAcceptanceScenarioAsync(NpgsqlConnection connection)
    {
        var mangaka1 = Guid.NewGuid();
        var mangaka2 = Guid.NewGuid();
        var assistant = Guid.NewGuid();
        var invitation1 = Guid.NewGuid();
        var invitation2 = Guid.NewGuid();
        await ExecuteAsync(connection, $"""
            INSERT INTO "Users" ("Id","Username","Email","PasswordHash","Role","AccountStatus","IsDeleted","CreatedAt") VALUES
              ('{mangaka1}','mgk_{mangaka1}@test.local','mgk_{mangaka1}@test.local','x','Mangaka','Active',false,now()),
              ('{mangaka2}','mgk_{mangaka2}@test.local','mgk_{mangaka2}@test.local','x','Mangaka','Active',false,now()),
              ('{assistant}','ast_{assistant}@test.local','ast_{assistant}@test.local','x','Assistant','Active',false,now())
            """);
        await ExecuteAsync(connection, $"""
            INSERT INTO "StudioInvitations"
              ("Id","SeriesId","InviterMangakaId","AssistantUserId","AssistantEmail","NormalizedAssistantEmail","Status","IsNewAccountFlow","RegistrationDeliveryStatus","CreatedAt","ExpiresAt") VALUES
              ('{invitation1}','{Guid.NewGuid()}','{mangaka1}','{assistant}','ast_{assistant}@test.local','ast_{assistant}@test.local','Pending',false,'NotRequired',now(),now()+interval '1 day'),
              ('{invitation2}','{Guid.NewGuid()}','{mangaka2}','{assistant}','ast_{assistant}@test.local','ast_{assistant}@test.local','Pending',false,'NotRequired',now(),now()+interval '1 day')
            """);
        return (assistant, invitation1, invitation2);
    }

    private AppDbContext CreateDbContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_connectionString).ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)).Options);

    private sealed class TestDbContextProvider(AppDbContext context) : IDbContextProvider
    {
        public object GetDbContext() => context;
    }
}

[CollectionDefinition("Phase1 PostgreSQL", DisableParallelization = true)]
public sealed class Phase1PostgreSqlCollection { }
