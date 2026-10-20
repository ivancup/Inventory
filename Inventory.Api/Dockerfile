FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Inventory.Api/Inventory.Api.csproj", "Inventory.Api/"]
COPY ["Inventory.Domain/Inventory.Domain.csproj", "Inventory.Domain/"]
COPY ["Inventory.Repository/Inventory.Repository.csproj", "Inventory.Repository/"]
COPY ["Inventory.Service/Inventory.Service.csproj", "Inventory.Service/"]
COPY ["Inventory.Utils/Inventory.Utils.csproj", "Inventory.Utils/"]
COPY ["Directory.Packages.props", "."]
RUN dotnet restore "Inventory.Api/Inventory.Api.csproj"
COPY . .
RUN dotnet publish "Inventory.Api/Inventory.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Inventory.Api.dll"]
