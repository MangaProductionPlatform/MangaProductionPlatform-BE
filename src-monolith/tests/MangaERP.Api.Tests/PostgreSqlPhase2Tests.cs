using Npgsql;
using STTask = System.Threading.Tasks.Task;
using MangaERP.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using MangaERP.Shared.Domain.Exceptions;

namespace MangaERP.Api.Tests;

[Collection("Phase2 PostgreSQL")]
public sealed class PostgreSqlPhase2Tests
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
    public async STTask Phase2TablesExistInPostgreSql()
    {
        await using var connection = await OpenAsync();

        await using var seriesGrantTable = new NpgsqlCommand("SELECT to_regclass('public.\"SeriesAccessGrants\"')::text", connection);
        Assert.Equal("\"SeriesAccessGrants\"", (string?)await seriesGrantTable.ExecuteScalarAsync());

        await using var taskAttemptTable = new NpgsqlCommand("SELECT to_regclass('public.\"TaskAssignmentAttempts\"')::text", connection);
        Assert.Equal("\"TaskAssignmentAttempts\"", (string?)await taskAttemptTable.ExecuteScalarAsync());
    }

    [Fact]
    public async STTask PartialUniqueIndex_SeriesAccessGrants_EnforcesOneActiveGrant()
    {
        await using var connection = await OpenAsync();
        var mangakaId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var seriesId = Guid.NewGuid();
        var collabId = Guid.NewGuid();
        var invitationId = Guid.NewGuid();

        await SeedUserAndCollabAsync(connection, mangakaId, assistantId, collabId, invitationId, seriesId);

        var grant1Id = Guid.NewGuid();
        await ExecuteAsync(connection, $"""
            INSERT INTO "SeriesAccessGrants"
              ("Id","CollaborationId","SeriesId","GrantedByUserId","GrantedAt","CreatedAt","UpdatedAt","ConcurrencyToken")
            VALUES ('{grant1Id}','{collabId}','{seriesId}','{mangakaId}',now(),now(),now(),'{Guid.NewGuid()}')
            """);

        // Inserting a second active grant for same collaboration & series should fail with unique violation
        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, $"""
            INSERT INTO "SeriesAccessGrants"
              ("Id","CollaborationId","SeriesId","GrantedByUserId","GrantedAt","CreatedAt","UpdatedAt","ConcurrencyToken")
            VALUES ('{Guid.NewGuid()}','{collabId}','{seriesId}','{mangakaId}',now(),now(),now(),'{Guid.NewGuid()}')
            """));

        // Revoke first grant
        await ExecuteAsync(connection, $"""
            UPDATE "SeriesAccessGrants"
            SET "RevokedAt" = now(), "RevokedByUserId" = '{mangakaId}', "RevokeReason" = 'Revoked'
            WHERE "Id" = '{grant1Id}'
            """);

        // Now creating a new grant for same collaboration & series succeeds
        var grant2Id = Guid.NewGuid();
        await ExecuteAsync(connection, $"""
            INSERT INTO "SeriesAccessGrants"
              ("Id","CollaborationId","SeriesId","GrantedByUserId","GrantedAt","CreatedAt","UpdatedAt","ConcurrencyToken")
            VALUES ('{grant2Id}','{collabId}','{seriesId}','{mangakaId}',now(),now(),now(),'{Guid.NewGuid()}')
            """);
    }

    [Fact]
    public async STTask PartialUniqueIndex_TaskAssignmentAttempts_EnforcesOnePendingAttempt()
    {
        await using var connection = await OpenAsync();
        var mangakaId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var seriesId = Guid.NewGuid();
        var collabId = Guid.NewGuid();
        var invitationId = Guid.NewGuid();
        var chapterId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        await SeedUserAndCollabAsync(connection, mangakaId, assistantId, collabId, invitationId, seriesId);
        await SeedTaskAsync(connection, seriesId, chapterId, taskId);

        await ExecuteAsync(connection, $"""
            INSERT INTO "TaskAssignmentAttempts"
              ("Id","TaskId","AssistantId","CollaborationId","AttemptNumber","Status","AssignedAt","AssignedByUserId","CreatedAt","UpdatedAt","ConcurrencyToken")
            VALUES ('{Guid.NewGuid()}','{taskId}','{assistantId}','{collabId}',1,'PendingAcceptance',now(),'{mangakaId}',now(),now(),'{Guid.NewGuid()}')
            """);

        // Inserting a second PendingAcceptance attempt for same task should fail with unique violation
        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, $"""
            INSERT INTO "TaskAssignmentAttempts"
              ("Id","TaskId","AssistantId","CollaborationId","AttemptNumber","Status","AssignedAt","AssignedByUserId","CreatedAt","UpdatedAt","ConcurrencyToken")
            VALUES ('{Guid.NewGuid()}','{taskId}','{assistantId}','{collabId}',2,'PendingAcceptance',now(),'{mangakaId}',now(),now(),'{Guid.NewGuid()}')
            """));
    }

    [Fact]
    public async STTask TransactionRollback_LeavesNoOrphanAttemptOrGrant()
    {
        await using var connection = await OpenAsync();
        var mangakaId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var seriesId = Guid.NewGuid();
        var collabId = Guid.NewGuid();
        var invitationId = Guid.NewGuid();
        var chapterId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var grantId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();

        await SeedUserAndCollabAsync(connection, mangakaId, assistantId, collabId, invitationId, seriesId);
        await SeedTaskAsync(connection, seriesId, chapterId, taskId);

        await using var tx = await connection.BeginTransactionAsync();
        await ExecuteAsync(connection, $"""
            INSERT INTO "SeriesAccessGrants"
              ("Id","CollaborationId","SeriesId","GrantedByUserId","GrantedAt","CreatedAt","UpdatedAt","ConcurrencyToken")
            VALUES ('{grantId}','{collabId}','{seriesId}','{mangakaId}',now(),now(),now(),'{Guid.NewGuid()}')
            """, tx);

        await ExecuteAsync(connection, $"""
            INSERT INTO "TaskAssignmentAttempts"
              ("Id","TaskId","AssistantId","CollaborationId","AttemptNumber","Status","AssignedAt","AssignedByUserId","CreatedAt","UpdatedAt","ConcurrencyToken")
            VALUES ('{attemptId}','{taskId}','{assistantId}','{collabId}',1,'PendingAcceptance',now(),'{mangakaId}',now(),now(),'{Guid.NewGuid()}')
            """, tx);

        await tx.RollbackAsync();

        await using var grantCheck = new NpgsqlCommand($"SELECT count(*) FROM \"SeriesAccessGrants\" WHERE \"Id\"='{grantId}'", connection);
        Assert.Equal(0L, (long)await grantCheck.ExecuteScalarAsync()!);

        await using var attemptCheck = new NpgsqlCommand($"SELECT count(*) FROM \"TaskAssignmentAttempts\" WHERE \"Id\"='{attemptId}'", connection);
        Assert.Equal(0L, (long)await attemptCheck.ExecuteScalarAsync()!);
    }

    private static async STTask ExecuteAsync(NpgsqlConnection connection, string sql, NpgsqlTransaction? transaction = null)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync();
    }

    private static async STTask SeedUserAndCollabAsync(NpgsqlConnection connection, Guid mangaka, Guid assistant, Guid collab, Guid invitation, Guid series)
    {
        await ExecuteAsync(connection, $"""
            INSERT INTO "Users" ("Id","Username","Email","PasswordHash","Role","AccountStatus","IsDeleted","CreatedAt") VALUES
              ('{mangaka}','mgk_{mangaka}@test.local','mgk_{mangaka}@test.local','x','Mangaka','Active',false,now()),
              ('{assistant}','ast_{assistant}@test.local','ast_{assistant}@test.local','x','Assistant','Active',false,now())
            """);

        await ExecuteAsync(connection, $"""
            INSERT INTO "StudioInvitations"
              ("Id","SeriesId","InviterMangakaId","AssistantUserId","AssistantEmail","NormalizedAssistantEmail","Status","IsNewAccountFlow","RegistrationDeliveryStatus","CreatedAt","ExpiresAt")
            VALUES ('{invitation}','{series}','{mangaka}','{assistant}','ast_{assistant}@test.local','ast_{assistant}@test.local','Accepted',false,'NotRequired',now(),now()+interval '1 day')
            """);

        await ExecuteAsync(connection, $"""
            INSERT INTO "MangakaAssistantCollaborations"
              ("Id","MangakaId","AssistantId","InvitationId","Status","StartedAt","CreatedAt","UpdatedAt","ConcurrencyToken")
            VALUES ('{collab}','{mangaka}','{assistant}','{invitation}','Active',now(),now(),now(),'{Guid.NewGuid()}')
            """);

        await ExecuteAsync(connection, $"""
            INSERT INTO "MangaSeries" ("Id","Title","AuthorId","Status","IsDeleted","CreatedAt")
            VALUES ('{series}','Series {series}','{mangaka}','Ongoing',false,now())
            """);
    }

    private static async STTask SeedTaskAsync(NpgsqlConnection connection, Guid series, Guid chapter, Guid task)
    {
        await ExecuteAsync(connection, $"""
            INSERT INTO "Chapters" ("Id","SeriesId","Title","ChapterNumber","TotalPages","Status","IsDeleted","CreatedAt","EditorialRound")
            VALUES ('{chapter}','{series}','Chapter 1',1.00,10,'Draft',false,now(),1)
            """);

        await ExecuteAsync(connection, $"""
            INSERT INTO "PageTasks" ("Id","ChapterId","PageNumber","BaseImageUrl","TaskStatus","TaskType","IsDeleted","CreatedAt","UpdatedAt")
            VALUES ('{task}','{chapter}',1,'http://base.img/1.png','Pending','General',false,now(),now())
            """);
    }
}

[CollectionDefinition("Phase2 PostgreSQL", DisableParallelization = true)]
public sealed class Phase2PostgreSqlCollection { }
