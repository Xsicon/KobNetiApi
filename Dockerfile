# Support API — container for Render / Docker
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY Sominnercore.SupportApi/Sominnercore.SupportApi.csproj Sominnercore.SupportApi/
RUN dotnet restore Sominnercore.SupportApi/Sominnercore.SupportApi.csproj
COPY Sominnercore.SupportApi/ Sominnercore.SupportApi/
RUN dotnet publish Sominnercore.SupportApi/Sominnercore.SupportApi.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Sominnercore.SupportApi.dll"]
