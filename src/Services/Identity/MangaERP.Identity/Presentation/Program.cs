using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Infrastructure.Persistence;
using MangaERP.Identity.Infrastructure.Persistence.Repositories;
using MangaERP.Identity.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("IdentityDb")));

// MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// Repositories & Services
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddSingleton<MangaERP.BuildingBlocks.Infrastructure.Messaging.IEventBus, MangaERP.BuildingBlocks.Infrastructure.Messaging.InMemoryEventBus>();

// Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Auto-create database with retry policy for SQL Server bootup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    int retries = 10;
    while (retries > 0)
    {
        try
        {
            await context.Database.EnsureCreatedAsync();
            break;
        }
        catch (Exception ex)
        {
            retries--;
            Console.WriteLine($"Database initialization for IdentityDb failed. Retrying... ({retries} retries left). Error: {ex.Message}");
            if (retries == 0) throw;
            await Task.Delay(5000);
        }
    }
}

app.Run();
