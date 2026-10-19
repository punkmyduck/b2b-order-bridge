# Orders

Владеет заказами, позициями, клиентским снимком и outbox событий.
REST API: `http://localhost:8088/swagger`; база `orders` (локально порт 5433).
Не читает таблицы сервиса Integrations.

## Проекты

- `OrderBridge.Orders.Domain`: сущности и правила состояния.
- `OrderBridge.Orders.Application`: CQRS, валидаторы, сценарии и интерфейсы.
- `OrderBridge.Orders.Infrastructure`: собственная БД, EF Core, миграции и Kafka.
- `OrderBridge.Orders.Api`: HTTP endpoints, DI и фоновые процессы сервиса.

Сборка отдельно от остальных сервисов:

```powershell
dotnet build Orders.slnx
```

Запуск (из этой директории, PostgreSQL/Kafka должны быть доступны):

```powershell
$env:Persistence__ApplyMigrationsOnStartup = 'true'
dotnet run --project OrderBridge.Orders.Api
```

Подключение к БД: `ConnectionStrings__Orders`; брокер: `Kafka__BootstrapServers`.
В production миграции следует запускать отдельным шагом развёртывания.
