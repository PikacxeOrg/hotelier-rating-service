# --------------------------------------------------------
# 1) Build stage
# --------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG VERSION=1.0.0
WORKDIR /src

# Copy csproj files to maximize layer cache hits
COPY src/RatingService.Domain/RatingService.Domain.csproj ./src/RatingService.Domain/
COPY src/RatingService.Infrastructure/RatingService.Infrastructure.csproj ./src/RatingService.Infrastructure/
COPY src/RatingService.Api/RatingService.Api.csproj ./src/RatingService.Api/
RUN dotnet restore ./src/RatingService.Api/RatingService.Api.csproj

# Copy rest of sources
COPY src/ ./src/

# Publish with trimming for smaller output
RUN dotnet publish ./src/RatingService.Api/RatingService.Api.csproj \
    -c Release -o /app/publish /p:UseAppHost=false /p:SelfContained=false /p:Version=$VERSION

# --------------------------------------------------------
# 2) Runtime stage
# --------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS final

# install tzdata + wget (healthcheck)
RUN apk add --no-cache tzdata wget

WORKDIR /app
COPY --from=build /app/publish .

# Expose default Kestrel port for services
EXPOSE 8080

# Working environment defaults
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

# Optional health check (Docker + Kubernetes)
HEALTHCHECK --interval=30s --timeout=3s \
    CMD wget -qO- http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "RatingService.Api.dll"]
