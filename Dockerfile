# Etapa 1: compilar y publicar
FROM mcr.microsoft.com/dotnet/sdk:8.0.424 AS build
WORKDIR /src

# Copiar solo los .csproj primero para cachear la restauración de paquetes.
# Se copian todos: WMS.Api es el host y arrastra Api + las tres capas.
COPY src/Api/Api.csproj src/Api/
COPY src/WMS.Api/WMS.Api.csproj src/WMS.Api/
COPY src/WMS.Domain/WMS.Domain.csproj src/WMS.Domain/
COPY src/WMS.Application/WMS.Application.csproj src/WMS.Application/
COPY src/WMS.Infrastructure/WMS.Infrastructure.csproj src/WMS.Infrastructure/
RUN dotnet restore src/WMS.Api/WMS.Api.csproj

COPY src/ src/
RUN dotnet publish src/WMS.Api/WMS.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Etapa 2: imagen de ejecución (sin SDK, solo el runtime de ASP.NET)
# Nota: los tags de aspnet usan la versión del *runtime* (8.0.x), que no coincide
# con la del SDK (8.0.4xx). Un runtime 8.0.x más reciente ejecuta sin problema
# cualquier app net8.0 compilada con el SDK de global.json.
FROM mcr.microsoft.com/dotnet/aspnet:8.0.32 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Usuario no privilegiado incluido en las imágenes .NET 8 (uid 1654)
USER $APP_UID

ENTRYPOINT ["dotnet", "WMS.Api.dll"]
