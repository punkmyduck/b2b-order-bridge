# PostgreSQL persistence

OrderBridgeDbContext реализует IUnitOfWork через унаследованный SaveChangesAsync.
Все репозитории и unit of work в одном DI scope используют один DbContext.
Add/Update/Remove не фиксируют изменения; один SaveChangesAsync атомарно сохраняет
заказ, позиции и добавленные операции интеграции.

EfRepository<TEntity, TId> реализует IReadRepository и IRepository, допускает наследование
для специфичных операций. GetByIdAsync возвращает tracked-агрегат; Order.Lines загружается
через AutoInclude, Customer — owned-объект в таблице Orders.
Для отдельной DTO-проекции используйте AsNoTracking и при необходимости IgnoreAutoIncludes.
Update/Remove требуют предварительной загрузки в текущем scope: detached-объект
не содержит shadow concurrency token. Для обычных изменений tracked-агрегата Update не нужен.
ExistsAsync проверяет сохранённые строки, не локальные Added-сущности.

Денежные значения numeric(31,2) вмещают диапазон .NET decimal с двумя знаками.
Quantity хранится как numeric без ограничения scale, поскольку домен пока не ограничивает его.
Время приводится к UTC при записи; исходный offset не сохраняется.
CustomerSnapshot с get-only свойствами явно настроен и материализуется через конструктор.
DomainEvents не сохраняются, не публикуются и не очищаются автоматически.

Order и OrderIntegration используют shadow uint Version, сопоставленный с PostgreSQL xmin.
Конкурентная запись выбрасывает DbUpdateConcurrencyException; это ещё не lease/захват
фоновой операции и не механизм восстановления worker.
Индексы запрещают дубли внешнего номера заказа, операции заказа и ключа в рамках target.
DbUpdateException не переводится автоматически в Result: обработка конкретного
unique constraint и семантика идемпотентности относятся к сценарию приложения.

## Настройка и миграции

Из каталога `B2BOrderBridge` всю систему можно запустить одной командой:

```powershell
docker compose up --build
```

Swagger UI будет доступен по адресу `http://localhost:8088/swagger`.

Параметры по умолчанию в `docker-compose.yml`, design-time factory и локальном
`appsettings.Development.json` совпадают. Их можно переопределить переменными
`POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_PORT` для Docker и
`ConnectionStrings__OrderBridge` для API и `dotnet ef`. Локальный development-файл
игнорируется Git. В Development миграции применяются при старте, если включён параметр
`Persistence:ApplyMigrationsOnStartup`; Compose включает его для удобства локального запуска.
Версия `dotnet-ef` для этой схемы — 10.0.8.

```powershell
dotnet test B2BOrderBridge.slnx
```

Infrastructure.Tests требует запущенный Docker с Linux-контейнерами.
Testcontainers создаёт отдельную временную PostgreSQL 17, применяет миграции
и удаляет контейнер после тестов. Рабочая база приложения не используется.

## Следующие изменения CreateOrderCommand

Текущий обработчик сохранён: он создаёт только заказ.
Добавить нужно входные DTO вместо CustomerSnapshot/OrderLine, валидацию запроса,
создание начальных OrderIntegration до единственного SaveChangesAsync,
а затем inbox/Idempotency-Key и перевод ожидаемых конфликтов в Result.
DomainException пока не преобразуется автоматически в HTTP-ошибку.
Внешние API в обработчике создания не вызываются.
