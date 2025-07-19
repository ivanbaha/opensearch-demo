# Use the official .NET 9.0 runtime as base image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Use the .NET 9.0 SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["OpenSearchDemo.csproj", "."]
RUN dotnet restore "./OpenSearchDemo.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "OpenSearchDemo.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "OpenSearchDemo.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create data directory for sync statistics
RUN mkdir -p /app/data

ENTRYPOINT ["dotnet", "OpenSearchDemo.dll"]
