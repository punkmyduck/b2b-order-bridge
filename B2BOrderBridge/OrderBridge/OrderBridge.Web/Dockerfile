FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Razor files must exist during restore so the SDK includes Blazor framework assets.
COPY OrderBridge/OrderBridge.Web/ OrderBridge/OrderBridge.Web/
RUN dotnet restore OrderBridge/OrderBridge.Web/OrderBridge.Web.csproj
RUN dotnet publish OrderBridge/OrderBridge.Web/OrderBridge.Web.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
USER $APP_UID
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OrderBridge.Web.dll"]
