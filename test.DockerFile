FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /app
COPY . ./
RUN dotnet publish "./api/SteamServerManager.Cli/SteamServerManager.Cli.csproj" -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
ENV DEBIAN_FRONTEND=noninteractive
RUN dpkg --add-architecture i386 && \
    apt-get update && \
    echo steam steam/question select "I AGREE" | debconf-set-selections && \
    echo steam steam/license note '' | debconf-set-selections && \
    apt-get install -y wine64 && \
    apt-get clean autoclean && \
    apt-get autoremove -y && \
    rm -rf /var/lib/apt/lists/*
WORKDIR /app
USER root
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "SteamServerManager.Cli.dll"]