using MangaERP.Series.Application.Ports;
using MangaERP.Series.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<SeriesDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SeriesDb")));

builder.Services.AddScoped<ISeriesRepository, SeriesRepository>();
builder.Services.AddSingleton<MangaERP.BuildingBlocks.Infrastructure.Messaging.IEventBus, MangaERP.BuildingBlocks.Infrastructure.Messaging.InMemoryEventBus>();

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidateAudience = true,
            ValidateLifetime = true, ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "MangaERP - Series Service", Version = "v1" }));

var app = builder.Build();
app.UseSwagger(); app.UseSwaggerUI();
app.UseAuthentication(); app.UseAuthorization();
app.MapControllers();

// Auto-create database with retry policy for SQL Server bootup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<SeriesDbContext>();
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
            Console.WriteLine($"Database initialization for SeriesDb failed. Retrying... ({retries} retries left). Error: {ex.Message}");
            if (retries == 0) throw;
            await Task.Delay(5000);
        }
    }
}

app.Run();
