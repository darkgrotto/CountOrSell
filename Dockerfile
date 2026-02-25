# ─────────────────────────────────────────────────────────────────────────────
# CountOrSell — Multi-stage Docker build
#
# Works on Docker Desktop for Windows (Linux container mode), Docker on Linux,
# and Azure Container Registry / Azure Container Apps.
#
# Persistent data is stored at /data — mount a volume or Azure Files share
# at that path to survive container restarts.
#
# Environment variables:
#   Jwt__Key              JWT signing secret (32+ chars)  [REQUIRED]
#   Jwt__Issuer           Token issuer         (default: CountOrSell)
#   Jwt__Audience         Token audience       (default: CountOrSellUsers)
#   COS_DATABASE_PATH     SQLite file path     (default: /data/database/CountOrSell.db)
#   COS_IMAGES_PATH       Card images root     (default: /data/images)
#   ASPNETCORE_URLS       Listen address       (default: http://+:8080)
# ─────────────────────────────────────────────────────────────────────────────

# ── Stage 1: Build the React frontend ────────────────────────────────────────
FROM node:20-alpine AS frontend
WORKDIR /web
COPY src/countorsell-web/package*.json ./
RUN npm ci --silent
COPY src/countorsell-web/ ./
RUN npm run build

# ── Stage 2: Build the ASP.NET Core API ──────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS api
WORKDIR /src
COPY src/CountOrSell.sln .
COPY src/CountOrSell.Core/  CountOrSell.Core/
COPY src/CountOrSell.Api/   CountOrSell.Api/
COPY src/CountOrSell.Cli/   CountOrSell.Cli/
RUN dotnet publish CountOrSell.Api/CountOrSell.Api.csproj \
      -c Release -o /publish --nologo

# ── Stage 3: Runtime image ────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Copy the published API
COPY --from=api     /publish ./

# Copy the built frontend into wwwroot — the API serves it as static files
COPY --from=frontend /web/dist ./wwwroot

# Persistent data directory (mount a volume here)
RUN mkdir -p /data/database /data/images

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
ENV Jwt__Issuer=CountOrSell
ENV Jwt__Audience=CountOrSellUsers
ENV COS_DATABASE_PATH=/data/database/CountOrSell.db
ENV COS_IMAGES_PATH=/data/images

VOLUME ["/data"]
EXPOSE 8080

ENTRYPOINT ["dotnet", "CountOrSell.Api.dll"]
