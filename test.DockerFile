FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /app

COPY . ./
RUN dotnet publish "./api/SteamServerManager.Cli/SteamServerManager.Cli.csproj" -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:10.0

# Enable i386 and install 32-bit glibc + friends
RUN dpkg --add-architecture i386 \
 && apt-get update \
 && apt-get install -y --no-install-recommends \
      bash ca-certificates curl tar xz-utils file \
      libc6:i386 lib32gcc-s1 lib32stdc++6 \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app
USER root
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "SteamServerManager.Cli.dll"]