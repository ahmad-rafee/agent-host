FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["AgentHost.Worker.csproj", "."]
COPY ["../Shared/AgentHost.Shared.csproj", "../Shared/"]
RUN dotnet restore "./AgentHost.Worker.csproj"
COPY . .
COPY ../Shared/ ../Shared/
WORKDIR "/src/."
RUN dotnet build "AgentHost.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AgentHost.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AgentHost.Worker.dll"]
