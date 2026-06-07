using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// YARP reverse proxy configuration
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// JWT validation (symmetric key — same as microservices)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// ─── Aggregated Swagger UI ──────────────────────────────────────────
// Serves a single Swagger UI at /swagger that aggregates all microservices.
// Each service's swagger.json is proxied through YARP routes defined in appsettings.json.
app.UseSwaggerUI(options =>
{
    options.DocumentTitle = "MangaERP - API Gateway";
    options.RoutePrefix = "swagger";

    // Each endpoint points to the YARP-proxied swagger.json of the downstream service
    options.SwaggerEndpoint("/swagger/services/identity/v1/swagger.json", "🔐 Identity Service");
    options.SwaggerEndpoint("/swagger/services/submission/v1/swagger.json", "📝 Submission Service");
    options.SwaggerEndpoint("/swagger/services/series/v1/swagger.json", "📚 Series Service");
    options.SwaggerEndpoint("/swagger/services/chapter/v1/swagger.json", "📖 Chapter Service");
    options.SwaggerEndpoint("/swagger/services/task/v1/swagger.json", "🎨 Task Service");
    options.SwaggerEndpoint("/swagger/services/qa/v1/swagger.json", "🔍 QA Service");
    options.SwaggerEndpoint("/swagger/services/publishing/v1/swagger.json", "🚀 Publishing Service");
    options.SwaggerEndpoint("/swagger/services/ranking/v1/swagger.json", "🏆 Ranking Service");

    options.DisplayRequestDuration();
    options.DefaultModelsExpandDepth(-1); // Collapse models by default for cleaner UI
});

app.UseAuthentication();
app.UseAuthorization();

// Map all reverse-proxied routes (API + Swagger proxies)
app.MapReverseProxy();

app.Run();
