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

    [Fact]
    public async STTask PartialUniqueIndex_TaskAssignmentAttempts_Accepted_EnforcesSingleAcceptedAttempt()
    {
        await using var connection = await OpenAsync();
        var mangakaId = Guid.NewGuid();
        var assistant1Id = Guid.NewGuid();
        var assistant2Id = Guid.NewGuid();
        var collab1Id = Guid.NewGuid();
        var collab2Id = Guid.NewGuid();
        var inv1Id = Guid.NewGuid();
        var inv2Id = Guid.NewGuid();
        var seriesId = Guid.NewGuid();
        var chapterId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        await SeedUserAndCollabAsync(connection, mangakaId, assistant1Id, collab1Id, inv1Id, seriesId);

        // Insert assistant 2 and collab 2 separately
        await ExecuteAsync(connection, $"INSERT INTO \"Users\" (\"Id\",\"Username\",\"Email\",\"PasswordHash\",\"Role\",\"AccountStatus\",\"IsDeleted\",\"CreatedAt\") VALUES ('{assistant2Id}','ast_{assistant2Id}@test.local','ast_{assistant2Id}@test.local','x','Assistant','Active',false,now())");
        await ExecuteAsync(connection, $"INSERT INTO \"StudioInvitations\" (\"Id\",\"SeriesId\",\"InviterMangakaId\",\"AssistantUserId\",\"AssistantEmail\",\"NormalizedAssistantEmail\",\"Status\",\"IsNewAccountFlow\",\"RegistrationDeliveryStatus\",\"CreatedAt\",\"ExpiresAt\") VALUES ('{inv2Id}','{seriesId}','{mangakaId}','{assistant2Id}','ast_{assistant2Id}@test.local','ast_{assistant2Id}@test.local','Accepted',false,'NotRequired',now(),now()+interval '1 day')");
        await ExecuteAsync(connection, $"INSERT INTO \"MangakaAssistantCollaborations\" (\"Id\",\"MangakaId\",\"AssistantId\",\"InvitationId\",\"Status\",\"StartedAt\",\"CreatedAt\",\"UpdatedAt\",\"ConcurrencyToken\") VALUES ('{collab2Id}','{mangakaId}','{assistant2Id}','{inv2Id}','Active',now(),now(),now(),'{Guid.NewGuid()}')");

        await SeedTaskAsync(connection, seriesId, chapterId, taskId);

        var attempt1Id = Guid.NewGuid();
        var attempt2Id = Guid.NewGuid();

        await ExecuteAsync(connection, $"""
            INSERT INTO "TaskAssignmentAttempts"
              ("Id","TaskId","AssistantId","CollaborationId","AttemptNumber","Status","AssignedByUserId","AssignedAt","ResponseDeadline","WorkDeadline","CreatedAt","UpdatedAt","ConcurrencyToken","AssignmentRole")
            VALUES
              ('{attempt1Id}','{taskId}','{assistant1Id}','{collab1Id}',1,'PendingAcceptance','{mangakaId}',now(),now()+interval '1 day',now()+interval '2 days',now(),now(),'{Guid.NewGuid()}','Primary');
            """);

        // Attempt 1 accepts successfully (releasing PendingAcceptance index for task)
        await ExecuteAsync(connection, $"UPDATE \"TaskAssignmentAttempts\" SET \"Status\" = 'Accepted' WHERE \"Id\" = '{attempt1Id}'");

        // Insert attempt 2 (now PendingAcceptance is free)
        await ExecuteAsync(connection, $"""
            INSERT INTO "TaskAssignmentAttempts"
              ("Id","TaskId","AssistantId","CollaborationId","AttemptNumber","Status","AssignedByUserId","AssignedAt","ResponseDeadline","WorkDeadline","CreatedAt","UpdatedAt","ConcurrencyToken","AssignmentRole")
            VALUES
              ('{attempt2Id}','{taskId}','{assistant2Id}','{collab2Id}',2,'PendingAcceptance','{mangakaId}',now(),now()+interval '1 day',now()+interval '2 days',now(),now(),'{Guid.NewGuid()}','Primary');
            """);

        // Attempt 2 try to accept -> Must throw PostgresException (23505 Unique Violation on IX_TaskAssignmentAttempts_TaskId_Accepted)
        var ex = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await ExecuteAsync(connection, $"UPDATE \"TaskAssignmentAttempts\" SET \"Status\" = 'Accepted' WHERE \"Id\" = '{attempt2Id}'");
        });

        Assert.Equal("23505", ex.SqlState); // 23505 = unique_violation

        // Verify DB state: Exactly 1 Accepted attempt exists
        await using var cmd = new NpgsqlCommand($"SELECT COUNT(*) FROM \"TaskAssignmentAttempts\" WHERE \"TaskId\" = '{taskId}' AND \"Status\" = 'Accepted'", connection);
        var acceptedCount = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
        Assert.Equal(1L, acceptedCount);
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
            INSERT INTO "MangaSeries" ("Id","Title","AuthorId","Status","CancellationStatus","IsDeleted","CreatedAt")
            VALUES ('{series}','Series {series}','{mangaka}','Ongoing',0,false,now())
            """);
    }

    private static async STTask SeedTaskAsync(NpgsqlConnection connection, Guid series, Guid chapter, Guid task)
    {
        await ExecuteAsync(connection, $"""
            INSERT INTO "Chapters" ("Id","SeriesId","Title","ChapterNumber","TotalPages","Status","IsDeleted","CreatedAt","EditorialRound")
            VALUES ('{chapter}','{series}','Chapter 1',1.00,10,'Draft',false,now(),1)
            """);

        await ExecuteAsync(connection, $"""
            INSERT INTO "PageTasks" ("Id","ChapterId","PageNumber","BaseImageUrl","TaskStatus","TaskType","ProgressPercent","IsDeleted","CreatedAt","UpdatedAt")
            VALUES ('{task}','{chapter}',1,'http://base.img/1.png','Pending','General',0,false,now(),now())
            """);
    }
    private AppDbContext CreateDbContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_connectionString).ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)).Options);
}

[CollectionDefinition("Phase2 PostgreSQL", DisableParallelization = true)]
public sealed class Phase2PostgreSqlCollection { }
