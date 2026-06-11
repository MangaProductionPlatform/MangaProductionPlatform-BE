# ── Stage 1: Build ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Override any Windows host NuGet fallback paths baked into project.assets.json
ENV NUGET_FALLBACK_PACKAGES=""

# Copy solution + all .csproj files first for layer-cache optimisation
COPY MangaERP.sln ./
COPY src/MangaERP.Api/MangaERP.Api.csproj                                             src/MangaERP.Api/
COPY src/Shared/MangaERP.Shared.Domain/MangaERP.Shared.Domain.csproj                 src/Shared/MangaERP.Shared.Domain/
COPY src/Shared/MangaERP.Shared.Application/MangaERP.Shared.Application.csproj       src/Shared/MangaERP.Shared.Application/
COPY src/Shared/MangaERP.Shared.Infrastructure/MangaERP.Shared.Infrastructure.csproj src/Shared/MangaERP.Shared.Infrastructure/
COPY src/Modules/MangaERP.Identity/MangaERP.Identity.csproj                           src/Modules/MangaERP.Identity/
COPY src/Modules/MangaERP.Submission/MangaERP.Submission.csproj                       src/Modules/MangaERP.Submission/
COPY src/Modules/MangaERP.Series/MangaERP.Series.csproj                               src/Modules/MangaERP.Series/
COPY src/Modules/MangaERP.Chapter/MangaERP.Chapter.csproj                             src/Modules/MangaERP.Chapter/
COPY src/Modules/MangaERP.Task/MangaERP.Task.csproj                                   src/Modules/MangaERP.Task/
COPY src/Modules/MangaERP.QA/MangaERP.QA.csproj                                       src/Modules/MangaERP.QA/
COPY src/Modules/MangaERP.Publishing/MangaERP.Publishing.csproj                       src/Modules/MangaERP.Publishing/
COPY src/Modules/MangaERP.Ranking/MangaERP.Ranking.csproj                             src/Modules/MangaERP.Ranking/

RUN dotnet restore src/MangaERP.Api/MangaERP.Api.csproj

# Copy all remaining source files
COPY . .

RUN dotnet publish src/MangaERP.Api/MangaERP.Api.csproj \
    -c Release -o /publish

# ── Stage 2: Runtime ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Non-root user for security
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
COPY --from=build /publish .
RUN chown -R appuser:appgroup /app
USER appuser

# Railway will override PORT at runtime — we default to 8080 locally
ENV ASPNETCORE_ENVIRONMENT=Production
ENV PORT=8080

EXPOSE ${PORT}

ENTRYPOINT ["dotnet", "MangaERP.Api.dll"]
