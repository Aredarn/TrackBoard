# syntax=docker/dockerfile:1
# Versions are pinned deliberately — no 'latest' tags (roadmap phase 1).

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src

# Restore as its own layer so code edits do not invalidate the package cache.
# Directory.Build.props carries the TargetFramework, so restore needs it too.
COPY Directory.Build.props ./
COPY TrackBoard/TrackBoard.csproj TrackBoard/
RUN dotnet restore TrackBoard/TrackBoard.csproj

COPY . .
RUN dotnet publish TrackBoard/TrackBoard.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime
WORKDIR /app

# Run unprivileged; the base image ships a non-root 'app' user.
USER app

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "TrackBoard.dll"]
