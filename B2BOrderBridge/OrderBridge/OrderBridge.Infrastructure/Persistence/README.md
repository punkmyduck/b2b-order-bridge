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

Из каталога B2BOrderBridge установите переменную окружения в PowerShell, подставив
свои локальные параметры подключения (не сохраняйте пароль в репозитории):

```powershell
$env:ConnectionStrings__OrderBridge = 'Host=localhost;Port=5432;Database=orderbridge;Username=postgres;Password=<local-password>'
dotnet ef database update --project OrderBridge/OrderBridge.Infrastructure
dotnet run --project OrderBridge/OrderBridge.Presentation
```

Для dotnet ef используется design-time factory. Приложение требует
ConnectionStrings:OrderBridge при старте; миграции не применяются автоматически.
Версия dotnet-ef для этой схемы — 10.0.8.

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
