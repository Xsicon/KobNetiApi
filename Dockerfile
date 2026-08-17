# Support API — container for Render / Docker
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY KobNeti.Api/KobNeti.Api.csproj KobNeti.Api/
RUN dotnet restore KobNeti.Api/KobNeti.Api.csproj
COPY KobNeti.Api/ KobNeti.Api/
RUN dotnet publish KobNeti.Api/KobNeti.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
# Render free tier hits inotify limits if FileConfigurationProvider watches appsettings.
ENV DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "KobNeti.Api.dll"]
