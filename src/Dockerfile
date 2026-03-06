FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY SecureVault.Api/SecureVault.Api.csproj SecureVault.Api/
COPY SecureVault.Application/SecureVault.Application.csproj SecureVault.Application/
COPY SecureVault.Domain/SecureVault.Domain.csproj SecureVault.Domain/
COPY SecureVault.Infrastructure/SecureVault.Infrastructure.csproj SecureVault.Infrastructure/
RUN dotnet restore SecureVault.Api/SecureVault.Api.csproj

COPY SecureVault.Api/ SecureVault.Api/
COPY SecureVault.Application/ SecureVault.Application/
COPY SecureVault.Domain/ SecureVault.Domain/
COPY SecureVault.Infrastructure/ SecureVault.Infrastructure/

RUN dotnet publish SecureVault.Api/SecureVault.Api.csproj -c Release --no-restore -o /app/publish

FROM base AS final
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build --chown=app:app /app/publish .
RUN chown -R app:app /app
USER app
ENTRYPOINT ["dotnet", "SecureVault.Api.dll"]
