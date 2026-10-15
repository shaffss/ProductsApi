# Kontekst budowania: katalog główny repozytorium. Wersje obrazów przypięte - powtarzalne budowanie.
FROM mcr.microsoft.com/dotnet/sdk:10.0.402-noble AS build
WORKDIR /src
# .editorconfig: te same reguły analizatorów co lokalnie (ostrzeżenia są błędami)
COPY nuget.config Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/Products.Api/Products.Api.csproj src/Products.Api/
COPY src/Products.Core/Products.Core.csproj src/Products.Core/
COPY src/Products.Infrastructure/Products.Infrastructure.csproj src/Products.Infrastructure/
COPY src/Products.Persistence/Products.Persistence.csproj src/Products.Persistence/
COPY src/Products.Persistence.EfCore/Products.Persistence.EfCore.csproj src/Products.Persistence.EfCore/
COPY src/Products.Persistence.Dapper/Products.Persistence.Dapper.csproj src/Products.Persistence.Dapper/
COPY src/Products.Migrations.SqlServer/Products.Migrations.SqlServer.csproj src/Products.Migrations.SqlServer/
COPY src/Products.Migrations.Postgres/Products.Migrations.Postgres.csproj src/Products.Migrations.Postgres/
RUN dotnet restore src/Products.Api/Products.Api.csproj
COPY src/ src/
RUN dotnet publish src/Products.Api/Products.Api.csproj -c Release -o /app --no-restore

# Chiseled: bez powłoki i menedżera pakietów - mniejszy obraz i mniejsza powierzchnia ataku.
# Wariant extra zawiera ICU i strefy czasowe (Microsoft.Data.SqlClient nie działa w InvariantGlobalization).
FROM mcr.microsoft.com/dotnet/aspnet:10.0.13-noble-chiseled-extra
WORKDIR /app
COPY --from=build /app .
# użytkownik bez uprawnień administratora z obrazu .NET (zamiast root)
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Products.Api.dll"]
