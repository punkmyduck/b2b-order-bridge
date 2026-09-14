# См. статью по ссылке https://aka.ms/customizecontainer, чтобы узнать как настроить контейнер отладки и как Visual Studio использует этот Dockerfile для создания образов для ускорения отладки.

# Этот этап используется при запуске из VS в быстром режиме (по умолчанию для конфигурации отладки)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# Этот этап используется для сборки проекта службы
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["OrderBridge/OrderBridge.Presentation/OrderBridge.Presentation.csproj", "OrderBridge/OrderBridge.Presentation/"]
COPY ["OrderBridge/OrderBridge.Application/OrderBridge.Application.csproj", "OrderBridge/OrderBridge.Application/"]
COPY ["OrderBridge/OrderBridge.Domain/OrderBridge.Domain.csproj", "OrderBridge/OrderBridge.Domain/"]
COPY ["Shared/Shared.Application/Shared.Application.csproj", "Shared/Shared.Application/"]
COPY ["Shared/Shared.Domain/Shared.Domain.csproj", "Shared/Shared.Domain/"]
COPY ["OrderBridge/OrderBridge.Infrastructure/OrderBridge.Infrastructure.csproj", "OrderBridge/OrderBridge.Infrastructure/"]
RUN dotnet restore "./OrderBridge/OrderBridge.Presentation/OrderBridge.Presentation.csproj"
COPY . .
WORKDIR "/src/OrderBridge/OrderBridge.Presentation"
RUN dotnet build "./OrderBridge.Presentation.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Этот этап используется для публикации проекта службы, который будет скопирован на последний этап
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./OrderBridge.Presentation.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Этот этап используется в рабочей среде или при запуске из VS в обычном режиме (по умолчанию, когда конфигурация отладки не используется)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OrderBridge.Presentation.dll"]

