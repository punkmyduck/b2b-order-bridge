# Integrations

Владеет планами интеграций, попытками, inbox и локальными снимками заказов.
REST API: `http://localhost:8089/swagger`; база `integrations` (локально порт 5434).
Получает OrderCreatedV1 из Kafka; для исполнения не обращается к Orders API или Orders DB.

## Проекты

- `OrderBridge.Integrations.Domain`: сущности и правила состояния.
- `OrderBridge.Integrations.Application`: CQRS, валидаторы, сценарии и интерфейсы.
- `OrderBridge.Integrations.Infrastructure`: собственная БД, EF Core, миграции и Kafka.
- `OrderBridge.Integrations.Api`: HTTP endpoints, DI и фоновые процессы сервиса.

Сборка отдельно от остальных сервисов:

```powershell
dotnet build Integrations.slnx
```

Запуск (из этой директории, PostgreSQL/Kafka должны быть доступны):

```powershell
$env:Persistence__ApplyMigrationsOnStartup = 'true'
dotnet run --project OrderBridge.Integrations.Api
```

Подключение к БД: `ConnectionStrings__Integrations`; брокер: `Kafka__BootstrapServers`.
В production миграции следует запускать отдельным шагом развёртывания.
