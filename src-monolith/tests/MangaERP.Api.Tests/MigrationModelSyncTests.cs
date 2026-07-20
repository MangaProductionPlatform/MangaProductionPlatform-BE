using MangaERP.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace MangaERP.Api.Tests;

public class MigrationModelSyncTests
{
    [Fact]
    public void FinalizeEditorialWorkflowFailsClearlyOnDuplicatePendingInvitations()
    {
        var root = FindRepositoryRoot();
        var migrationPath = Path.Combine(root, "src", "Shared", "MangaERP.Shared.Infrastructure", "Persistence", "Migrations", "20260719174115_FinalizeEditorialWorkflow.cs");
        var migration = File.ReadAllText(migrationPath);

        Assert.Contains("Duplicate pending StudioInvitations require explicit administrator cleanup", migration);
        Assert.DoesNotContain("SET \"Status\" = 'Cancelled'", migration);
    }

    [Fact]
    public void RuntimeModelMatchesLatestMigrationSnapshot()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=model_sync;Username=test;Password=test")
            .Options);

        var migrations = db.GetService<IMigrationsAssembly>();
        var snapshot = Assert.IsAssignableFrom<ModelSnapshot>(migrations.ModelSnapshot);
        var initializer = db.GetService<IModelRuntimeInitializer>();
        var snapshotModel = initializer.Initialize(snapshot.Model, designTime: true);
        var currentModel = db.GetService<IDesignTimeModel>().Model;
        var differ = db.GetService<IMigrationsModelDiffer>();

        var differences = differ.GetDifferences(
            snapshotModel.GetRelationalModel(),
            currentModel.GetRelationalModel());

        Assert.True(differences.Count == 0,
            string.Join(Environment.NewLine, differences.Select(x => x switch
            {
                AddColumnOperation add => $"AddColumn {add.Table}.{add.Name}",
                DropColumnOperation drop => $"DropColumn {drop.Table}.{drop.Name}",
                _ => x.GetType().Name
            })));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MangaERP.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the Backend repository root.");
    }
}
