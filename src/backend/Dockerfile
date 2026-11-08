# syntax=docker/dockerfile:1
# OPS-01. Build context: the repository root (docker compose sets it).
#   target api         the API (ASP.NET Core runtime, non-root, HTTP on 8080 behind the web proxy)
#   target migrations  a one-shot EF Core migrations bundle; reads ConnectionStrings__FlowOps

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY .config/dotnet-tools.json .config/
COPY src/backend/FlowOps.Domain/FlowOps.Domain.csproj src/backend/FlowOps.Domain/
COPY src/backend/FlowOps.Application/FlowOps.Application.csproj src/backend/FlowOps.Application/
COPY src/backend/FlowOps.Infrastructure/FlowOps.Infrastructure.csproj src/backend/FlowOps.Infrastructure/
COPY src/backend/FlowOps.Api/FlowOps.Api.csproj src/backend/FlowOps.Api/
RUN dotnet restore src/backend/FlowOps.Api/FlowOps.Api.csproj && dotnet tool restore
COPY src/backend/ src/backend/
RUN dotnet publish src/backend/FlowOps.Api/FlowOps.Api.csproj -c Release -o /out/api --no-restore -p:UseAppHost=false

FROM build AS migrations-build
RUN dotnet ef migrations bundle \
      --project src/backend/FlowOps.Infrastructure --startup-project src/backend/FlowOps.Api \
      --configuration Release --output /out/efbundle --force

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS migrations
WORKDIR /app
COPY --from=migrations-build /out/efbundle ./efbundle
USER $APP_UID
ENTRYPOINT ["./efbundle"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
# Data-protection keys (cookies, antiforgery) persist here; compose mounts a volume so sessions survive restarts.
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys && chown -R $APP_UID /home/app/.aspnet
COPY --from=build /out/api ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "FlowOps.Api.dll"]
