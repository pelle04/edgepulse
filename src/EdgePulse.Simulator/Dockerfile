FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/EdgePulse.Simulator/EdgePulse.Simulator.csproj", "src/EdgePulse.Simulator/"]
RUN dotnet restore "./src/EdgePulse.Simulator/EdgePulse.Simulator.csproj"
COPY . .
WORKDIR "/src/src/EdgePulse.Simulator"
RUN dotnet build "./EdgePulse.Simulator.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./EdgePulse.Simulator.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EdgePulse.Simulator.dll"]