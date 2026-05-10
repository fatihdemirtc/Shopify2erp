FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/ShopifyErpSync.Dashboard/ShopifyErpSync.Dashboard.csproj",          "src/ShopifyErpSync.Dashboard/"]
COPY ["src/ShopifyErpSync.Core/ShopifyErpSync.Core.csproj",                    "src/ShopifyErpSync.Core/"]
COPY ["src/ShopifyErpSync.Infrastructure/ShopifyErpSync.Infrastructure.csproj", "src/ShopifyErpSync.Infrastructure/"]
RUN dotnet restore "src/ShopifyErpSync.Dashboard/ShopifyErpSync.Dashboard.csproj"

COPY . .
RUN dotnet publish "src/ShopifyErpSync.Dashboard/ShopifyErpSync.Dashboard.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ShopifyErpSync.Dashboard.dll"]
