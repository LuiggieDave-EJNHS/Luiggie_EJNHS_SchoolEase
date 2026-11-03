# Multi-stage build for SchoolEase.Web (ASP.NET Core 8 MVC).
# Used by GitHub-connected hosts such as Render, Fly.io and any Docker runtime.

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first so the NuGet layer is cached between builds.
COPY ["SchoolEase.Web/SchoolEase.Web.csproj", "SchoolEase.Web/"]
RUN dotnet restore "SchoolEase.Web/SchoolEase.Web.csproj"

COPY . .
RUN dotnet publish "SchoolEase.Web/SchoolEase.Web.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

# Hosts such as Render inject the port to listen on through $PORT.
CMD ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet SchoolEase.Web.dll"]
