# Как работает OrderBridge

Документ описывает код после разделения Orders и Integrations, по состоянию на 19.10.2026.
Пунктирные связи и отдельные примечания обозначают предусмотренные расширения, которые ещё не работают.
Диаграммы написаны на Mermaid и отображаются в GitHub. Их исходники находятся прямо в этом файле.

## 1. Какие процессы запущены

У системы два backend-микросервиса и отдельный Blazor Server. Каждый backend имеет собственные
Domain, Application, Infrastructure, API, PostgreSQL и миграции. Kafka связывает сервисы.

```mermaid
flowchart TB
    Browser[Браузер оператора] -->|Blazor Server / HTTP и SignalR| Web["Blazor Web :8090"]
    Client[Внешний HTTP-клиент / Swagger] -->|POST и GET /api/orders| OA
    Web -->|Создание и чтение заказов| OA
    Web -->|Чтение интеграций и попыток| IA
    Client -->|GET /api/order-integrations| IA

    subgraph Orders["Микросервис Orders — один процесс"]
        OA["Orders API :8088"] --> OH["MediatR / validators / handlers"]
        OH --> ODB[("Orders PostgreSQL\nOrders / OrderLines / OutboxMessages")]
        OD[OutboxDispatcher] -->|Читает outbox и записывает результат публикации| ODB
        OTI[KafkaTopicInitializer]
    end

    OD -->|"OrderCreatedV1 / key = OrderId"| KT["Kafka: orderbridge.orders.created.v1"]
    OTI -->|Создание topics| Kafka[Kafka broker]
    KT --- Kafka

    subgraph Integrations["Микросервис Integrations — один процесс"]
        IA["Integrations API :8089"] --> IQ["MediatR / read queries"]
        IC[OrderCreatedConsumer] --> IP[OrderCreatedProcessor]
        IP --> PL[OrderIntegrationPlanner]
        IP --> PS[IntegrationPlanningStore]
        PS --> IDB[("Integrations PostgreSQL\nIntegrationOrders / InboxMessages\nOrderIntegrations / IntegrationAttempts")]
        IQ --> IDB
        IW[IntegrationWorker] --> JS[IIntegrationJobStore]
        JS --> IDB
        IW -->|Локальная команда с IntegrationId| EX[ProcessOrderIntegrationCommandHandler]
        EX --> IDB
        EX --> R[Resolver: Target + Operation]
        R --> TM[TelegramNotificationIntegrationMockHandler]
        ITI[KafkaTopicInitializer]
    end

    KT --> IC
    IC -->|Некорректное сообщение| DLQ["Kafka: orderbridge.orders.created.v1.dlq"]
    ITI -->|Создание topics| Kafka
```

**Что здесь не является отдельным микросервисом:** OutboxDispatcher, consumer, planner, worker
и Telegram mock — это классы внутри двух API-процессов. При остановке `integrations-api`
останавливаются одновременно его HTTP API, Kafka consumer и исполнитель задач.

Отдельных сервисов Bitrix24/1С/Диадок/Telegram сейчас нет. RabbitMQ нет.
Конкретные задания интеграций хранятся в PostgreSQL, а не в отдельных Kafka topics.

## 2. Навигация по исходникам и зависимости слоёв

Относительные пути в этом документе считаются от `B2BOrderBridge/`, кроме ссылок Markdown.

```text
src/
  Services/
    Orders/
      Orders.slnx
      OrderBridge.Orders.Domain/
      OrderBridge.Orders.Application/
      OrderBridge.Orders.Infrastructure/
      OrderBridge.Orders.Api/
    Integrations/
      Integrations.slnx
      OrderBridge.Integrations.Domain/
      OrderBridge.Integrations.Application/
      OrderBridge.Integrations.Infrastructure/
      OrderBridge.Integrations.Api/
  Contracts/OrderBridge.Contracts/
  BuildingBlocks/Shared.Domain/
  BuildingBlocks/Shared.Application/
  Web/OrderBridge.Web/
tests/
  Services/Orders/
  Services/Integrations/
  BuildingBlocks/
  Web/
```

Внутри каждого сервиса направления ссылок на проекты одинаковые:

```mermaid
flowchart LR
    Api[API: composition root и HTTP] --> App[Application: сценарии и интерфейсы]
    Api --> Infra[Infrastructure: EF, Kafka, BackgroundService]
    Infra --> App
    App --> Domain[Domain: сущности и переходы состояний]
    App --> Contracts[Contracts: формат межсервисных событий]
    App --> SA[Shared.Application]
    Domain --> SD[Shared.Domain]
```

Infrastructure реализует интерфейсы Application; интерфейсы регистрируются в DI.
Например, Application использует `IUnitOfWork`, а при выполнении это собственный DbContext сервиса.
API собирает зависимости в `Program.cs`, вызывая `AddApplication`, `AddPersistence`, `AddMessaging`.

| Проект/слой | Что в нём находится |
|---|---|
| Orders.Domain | `Order`, `OrderLine`, `CustomerSnapshot`, валюты, статусы заказа и оплаты, доменные события |
| Orders.Application | Команда создания, запросы чтения, валидаторы, DTO, интерфейс `IOrderCreatedOutbox` |
| Orders.Infrastructure | `OrdersDbContext`, EF-репозитории, outbox, Kafka producer, dispatcher, миграции |
| Orders.Api | HTTP-контроллер, запросы API, преобразование Result в HTTP, обработка validation errors, health/Swagger |
| Integrations.Domain | `OrderIntegration`, Target/Operation/Status, правила переходов состояний |
| Integrations.Application | Планирование, выполнение, resolver, retry policy, `IntegrationAttempt`, запросы аудита, snapshot и интерфейсы хранилищ |
| Integrations.Infrastructure | Собственная БД, Kafka consumer, атомарное сохранение плана, выборка заданий, worker |
| Integrations.Api | Чтение статусов и попыток через HTTP, health/Swagger, composition root |
| Contracts | Только сериализуемые `OrderCreatedV1`, `OrderSnapshotV1`, `CustomerSnapshotV1`, `OrderLineV1` |
| Shared.Domain | Базовые Entity/AggregateRoot, Guard, доменные абстракции |
| Shared.Application | Result/Error, CQRS-контракты поверх MediatR, validation behaviour, pagination, общие интерфейсы |
| Web | Собственные модели представления, HTTP-клиенты, страницы; без ссылок на backend-проекты |

У сервисов нет ProjectReference друг на друга и нет общего бизнес-домена. Общие Contracts
связывают их форматом сообщения, поэтому изменение wire-контракта требует совместимости.
Telegram mock пока расположен в Application и зарегистрирован там — это фактическое текущее размещение.

## 3. Полный путь создания заказа

```mermaid
sequenceDiagram
    autonumber
    participant C as HTTP-клиент / Blazor
    participant A as Orders API
    participant H as CreateOrderCommandHandler
    participant D as Orders DB
    participant O as OutboxDispatcher
    participant K as Kafka
    participant R as OrderCreatedConsumer
    participant P as Processor / Planner / Store
    participant I as Integrations DB
    C->>A: POST /api/orders
    A->>H: MediatR.Send(CreateOrderCommand), после validation
    H->>H: Создать Order и OrderLine, рассчитать сумму
    H->>D: SaveChanges: Order + Lines + OutboxMessage
    Note over H,D: Одна локальная транзакция
    D-->>H: COMMIT
    H-->>A: Result<CreateOrderResponse>
    A-->>C: 201 Created + DTO + Location
    Note over C,I: 201 подтверждает сохранение заказа, но не завершение интеграций
    O->>D: BEGIN, SELECT 1 due outbox FOR UPDATE SKIP LOCKED
    O->>K: Publish OrderCreatedV1, key = OrderId
    K-->>O: Подтверждение публикации
    O->>D: PublishedAt, PublishAttempts; COMMIT
    K->>R: Запись topic: key + JSON + partition/offset
    R->>R: Проверить JSON, scenario, идентификаторы, key
    R->>P: ProcessAsync(OrderCreatedV1)
    P->>P: Snapshot + список OrderIntegration по сценарию
    P->>I: BEGIN; inbox + snapshot + jobs; COMMIT
    I-->>P: Сохранено либо EventId уже обработан
    P-->>R: Успех
    R->>K: Commit offset
```

1. `OrderController` переводит HTTP DTO в `CreateOrderCommand` и вызывает `ISender.Send`.
2. `ValidationBehaviour` находит валидаторы этой команды, выполняет их последовательно,
   затем передаёт управление хендлеру. Последовательность важна для валидаторов с общим DbContext.
3. Хендлер создаёт доменный заказ и позиции. Guard нормализует значения и защищает инварианты.
   Заказ начинается в `Received`, оплата — в `Pending`.
4. `IRepository.Add` и `IOrderCreatedOutbox.Add` только добавляют объекты в один scoped EF-контекст.
   Один `SaveChangesAsync` сохраняет заказ, позиции и outbox атомарно.
5. Хендлер **не вызывает Kafka** и **не создаёт OrderIntegration**. Поэтому временно недоступная
   Kafka не мешает сохранить заказ.
6. HTTP возвращает `201 Created`. К этому моменту интеграций в другом сервисе ещё может не быть.

**Сейчас сценарий жёстко задан как `FullCycle` в CreateOrderCommandHandler.** Внешний клиент
не выбирает его в CreateOrderRequest. В planner предусмотрены другие сценарии, но отдельных
HTTP-сценариев создания для них ещё нет.

## 4. Какие сообщения существуют

Следует различать вызовы внутри процесса и сообщения между процессами.

| Вид | Имя/содержимое | Откуда → куда | Транспорт / хранение |
|---|---|---|---|
| HTTP-запрос | Создание и чтение заказа | Web/клиент → Orders | HTTP JSON |
| Локальная команда | `CreateOrderCommand` | Контроллер → хендлер | MediatR, только память текущего процесса |
| Межсервисное событие | `OrderCreatedV1` | Orders → Integrations | Outbox → Kafka → Inbox |
| Техническое сообщение | DLQ envelope | Consumer → DLQ | Kafka, исходный payload + причина отказа |
| Локальная команда | `ProcessOrderIntegrationCommand(IntegrationId)` | Worker → хендлер | MediatR внутри Integrations |
| Контекст выполнения | `IntegrationExecutionContext` | Хендлер выполнения → адаптер | Обычный вызов метода, не брокер |
| Результат выполнения | `IntegrationExecutionResult` | Адаптер → хендлер выполнения | Обычный возвращаемый объект |
| Локальные read queries | Заказы, детали, интеграции, попытки | Контроллеры → query handlers | MediatR → read repositories |
| Доменные события | `OrderCancelledEvent`, `PaidCancelledOrderEvent` | Методы `Order` → коллекция DomainEvents | Пока только память; автоматической публикации нет |

Нет событий `IntegrationSucceeded`, `OrderUpdated`, `OrderPaid` и обратных сообщений из
Integrations в Orders. Изменение статуса интеграции не меняет статус заказа в Orders.
Наличие `DomainEvents` у AggregateRoot само по себе ничего не публикует:
DbContext игнорирует это свойство, dispatcher доменных событий сейчас не реализован.

### 4.1. Основное событие

Topic: `orderbridge.orders.created.v1`.
Тип контракта: `OrderCreatedV1`; константа EventType: `order.created.v1`.
Kafka key: `OrderId.ToString("N")`, то есть GUID без дефисов.
Value: JSON с camelCase. Упрощённый, но соответствующий контракту пример:

```json
{
  "eventId": "102a273b-4101-45b6-a028-a433783568d0",
  "occurredAt": "2026-10-19T12:00:00+00:00",
  "processingScenario": "FullCycle",
  "order": {
    "id": "3a0fb03a-302e-4c21-a1a7-d9d52fa9b5a6",
    "sourceSystem": "marketplace",
    "externalOrderId": "ORDER-421",
    "customer": {
      "companyName": "ACME",
      "taxId": "7701234567",
      "contactEmail": "orders@example.com"
    },
    "currency": "RUB",
    "totalAmount": 2000.00,
    "lines": [
      {
        "id": "80fb85a9-df56-4df2-a77c-a88087d688af",
        "sku": "SKU-1",
        "name": "Товар",
        "quantity": 2,
        "unitPrice": 1000.00,
        "lineTotal": 2000.00
      }
    ]
  }
}
```

Это снимок на момент создания. В нём нет всей доменной модели, статуса оплаты и ссылок на EF.
Локальный `OrderExecutionSnapshot` сохраняет нужные исполнителю поля; текущий mapper не переносит
Id и LineTotal позиций: локальная DTO позиции содержит SKU, Name, Quantity, UnitPrice.

`EventType` хранится в строке outbox, но отдельного поля `eventType` в JSON этого контракта нет.
Kafka headers с traceparent/correlationId сейчас не добавляются.

### 4.2. Настройки Kafka и подтверждения

| Настройка | Фактическое значение |
|---|---|
| Consumer group | `orderbridge-integration-planner-v1` |
| Основной topic | `orderbridge.orders.created.v1` |
| DLQ topic | `orderbridge.orders.created.v1.dlq` |
| Создание topics | `KafkaTopicInitializer` в обоих сервисах; повторное создание допускается |
| Topics при создании | 3 partitions, replication factor 1, retention 7 дней |
| Producer | `EnableIdempotence=true`, `Acks=All`, message timeout 10 секунд |
| Consumer | auto commit и auto offset store выключены; offset подтверждается явно |
| Начальная позиция без сохранённого offset | `Earliest` — с самого раннего ещё сохранённого сообщения |
| Обработка consumer | По одному сообщению за раз в одном экземпляре consumer |
| Перезапуск consumer после ошибки | Через 5 секунд, чтение от подтверждённых offsets |

Partitions позволяют распределять сообщения между экземплярами одной consumer group.
Одинаковый ключ направляет события заказа в одну partition при неизменной конфигурации partitioning;
общего порядка между разными partitions нет. Порядок выполнения интеграций worker этим не задаётся.

**Подтверждение Kafka producer не означает, что Integrations уже обработал событие.**
Оно означает завершение публикации с настроенным уровнем acknowledgements.
Idempotent producer не делает PostgreSQL и Kafka одной транзакцией: приложение всё ещё может
повторно опубликовать один EventId после перезапуска. Поэтому необходим inbox.
В локальном Compose брокер один: `Acks=All` не даёт отказоустойчивой репликации между узлами.

### 4.3. DLQ

Если JSON не читается, обязательные поля/проверяемые значения некорректны, сценарий неизвестен
или Kafka key не соответствует OrderId, consumer отправляет технический JSON в
`orderbridge.orders.created.v1.dlq`:

```json
{
  "sourceTopic": "orderbridge.orders.created.v1",
  "partition": 1,
  "offset": 42,
  "key": "original-key",
  "payload": "original raw JSON",
  "error": "The Kafka key must match the order ID.",
  "failedAt": "2026-10-19T12:00:05+00:00"
}
```

DLQ key: `sourceTopic:partition:offset`. Сначала подтверждается публикация DLQ, затем consumer
коммитит исходный offset. Если DLQ недоступна, offset не продвигается.
Сбой между этими двумя действиями может создать дубликат DLQ-сообщения с тем же ключом.
Автоматического потребителя DLQ, интерфейса её просмотра и replay сейчас нет.

Проверка входного события не равна полной бизнес-валидации всех вложенных полей.
Например, null-элемент в Lines не проверяется ReadScenario и может вызвать исключение при mapping.
Такое исключение пойдёт в общий механизм повторного чтения, а не обязательно в DLQ.

## 5. Outbox, inbox и границы транзакций

Три независимые системы хранения не объединены распределённой транзакцией.

| Граница | Что коммитится вместе | Что находится за её пределами |
|---|---|---|
| Создание заказа | Order + OrderLines + OutboxMessage | Публикация Kafka |
| Публикация outbox | Блокировка строки, счётчик, PublishedAt либо LastError/NextAttemptAt | Kafka не участник PostgreSQL-транзакции, хотя вызов сделан при удерживаемой блокировке |
| Приём события | InboxMessage + IntegrationOrder + новые OrderIntegration | Kafka offset коммитится после PostgreSQL |
| Старт попытки | Processing + AttemptCount + IntegrationAttempt(Processing) | Вызов адаптера |
| Завершение попытки | Итог OrderIntegration + итог IntegrationAttempt | Уже выполненное внешнее действие |

**OutboxDispatcher** делает проход сразу после запуска, затем с паузой 2 секунды.
За проход он до 20 раз вызывает `PublishNextAsync`; каждый вызов выбирает одну доступную строку
`PublishedAt IS NULL AND NextAttemptAt <= now` через `FOR UPDATE SKIP LOCKED`.
Блокировка удерживается во время ожидания Kafka и до коммита БД.
Это защищает одновременных publishers, но означает, что медленная Kafka держит транзакцию открытой.

При ошибке публикации увеличивается `PublishAttempts`, записываются `LastError` и
`NextAttemptAt = now + 10 секунд`. Лимита публикаций outbox сейчас нет.
При успехе записывается `PublishedAt`; это состояние доставки события, не состояние интеграций.

**IntegrationPlanningStore.SaveOnceAsync**:

1. Открывает транзакцию Integrations DB.
2. Вставляет inbox с ключом `(Consumer, EventId)` через `ON CONFLICT DO NOTHING`.
   Если запись уже есть, повторно план не сохраняется.
3. Сохраняет JSON снимка в `IntegrationOrders`, если такого OrderId ещё нет.
   Первый принятый снимок не перезаписывается.
4. Добавляет ещё отсутствующие пары Target + Operation для заказа.
5. Коммитит транзакцию. Только затем consumer подтверждает Kafka offset.

Строка `Consumer` в inbox: `order-created-integration-planner-v1`.
Это отдельный идентификатор обработчика, **он не совпадает** с Kafka consumer group.
Consumer group хранит позицию чтения в Kafka, inbox хранит факт применения события в своей БД.

## 6. Как из события получается план

Planner получает `(orderId, scenario, occurredAt)` и возвращает `IReadOnlyList<OrderIntegration>`.
Он не сохраняет БД, не вызывает внешние системы и не запускает задачи.

| Scenario | Target | Operation | Начальный статус |
|---|---|---|---|
| Internal | — | — | Нет интеграций |
| Accounting | OneC | CreateSalesOrder | Pending |
| FullCycle | Bitrix24 | CreateDeal | Pending |
| FullCycle | OneC | CreateSalesOrder | Pending |
| FullCycle | Telegram | SendOrderReceivedNotification | Pending |
| FullCycle | Diadoc | CreateDocument | WaitingForPrerequisite |

На каждую новую интеграцию planner создаёт GUID. `IdempotencyKey = integration.Id.ToString("N")`.
При последующих попытках той же интеграции ключ не меняется.

Сейчас только Telegram имеет зарегистрированный `IOrderIntegrationHandler`.
Bitrix24 и OneC остаются Pending: resolver возвращает ошибку до старта попытки,
поэтому AttemptCount не увеличивается, строки IntegrationAttempt не создаются.
Worker снова выберет их на следующем проходе и снова запишет ошибку отсутствующего handler в лог.
Это не работающий retry внешнего API, а отсутствие реализации адаптера.

Diadoc не выбирается worker вообще, пока находится в WaitingForPrerequisite.
У доменной модели есть `Release`, но вызывающего её сценария пока нет.
Платёжный webhook и автоматическая цепочка «оплата → документ» пока не реализованы.

## 7. Исполнение интеграции

```mermaid
sequenceDiagram
    autonumber
    participant W as IntegrationWorker
    participant J as IntegrationJobStore
    participant H as ProcessOrderIntegrationCommandHandler
    participant D as Integrations DB
    participant R as Resolver
    participant A as Integration handler / mock
    W->>J: GetReadyIntegrationIdsAsync(now, 50)
    J->>D: Pending или due RetryScheduled, без tracking
    D-->>W: До 50 IntegrationId
    Note over W,A: До 8 задач параллельно, отдельный DI scope и DbContext на задачу
    W->>H: MediatR.Send(ProcessOrderIntegrationCommand(id))
    H->>D: Загрузить tracked OrderIntegration и локальный snapshot
    H->>R: Resolve(Target, Operation)
    R-->>H: IOrderIntegrationHandler
    H->>H: StartAttempt; AttemptCount++; создать IntegrationAttempt
    H->>D: SaveChanges: Processing + попытка Processing
    Note over H,D: Concurrency token защищает от одновременного старта
    H->>A: ExecuteAsync(IntegrationExecutionContext, token)
    Note over H,A: Здесь нет открытой транзакции PostgreSQL вокруг внешнего вызова
    A-->>H: Succeeded / TransientFailure / PermanentFailure
    H->>H: Применить результат и retry policy, завершить попытку
    H->>D: SaveChanges: итог интеграции + итог попытки
    H-->>W: Result.Success: результат попытки сохранён
```

`IntegrationWorker` делает первый проход сразу, затем использует `PeriodicTimer` с периодом
10 секунд. Перекрывающихся batch-вызовов в одном worker нет: текущая пачка ожидается через await.
При долгой обработке это не обещание точного интервала 10 секунд между стартами.

`IIntegrationJobStore` — интерфейс доступа к очереди в PostgreSQL, **не background service**.
Его реализация `IntegrationJobStore` только выбирает идентификаторы:

```text
Pending
ИЛИ
RetryScheduled с NextAttemptAt <= now

ORDER BY (NextAttemptAt ?? CreatedAt), CreatedAt
LIMIT 50
```

WaitingForPrerequisite, Processing, Succeeded, Failed, Cancelled выборка не возвращает.
Сама выборка не захватывает задания. Два worker могут прочитать один Id.
Захват фактически происходит на первом SaveChanges перехода в Processing:
EF использует PostgreSQL row version (`xmin`, shadow property `Version`).
Проигравший конкурентный запуск получает `DbUpdateConcurrencyException` до вызова адаптера.
Worker перехватывает это исключение отдельно.

У каждой параллельной задачи собственный scope и DbContext, поэтому задачи не используют
один непотокобезопасный EF-контекст. Ограничение 8 действует на один процесс, а не на весь кластер.

### 7.1. Кто за что отвечает

| Компонент | Ответственность |
|---|---|
| Worker | Таймер, пачка, ограничение параллелизма, scope, Activity и обработка технических исключений |
| JobStore | Найти доступные IntegrationId |
| CommandHandler | Загрузить состояние, запустить попытку, сохранить её, вызвать адаптер, применить результат, сохранить завершение |
| Resolver | Найти один handler по `(IntegrationTarget, IntegrationOperation)` |
| Adapter/handler | Выполнить конкретный внешний вызов и классифицировать ответ |
| RetryPolicy | Решить, доступен ли повтор и когда |
| OrderIntegration | Разрешить/запретить переход состояния, обновить счётчик и состояние |
| IntegrationAttempt | История одного запуска, включая неуспешный перед будущим успешным повтором |

Адаптер не должен искать OrderIntegration по IdempotencyKey, вызывать StartAttempt или сохранять статус.
Ему уже переданы все входные данные:

```text
IntegrationExecutionContext
  IntegrationId
  IdempotencyKey
  AttemptNumber
  Order: OrderExecutionSnapshot
```

Повторная регистрация двух handlers с одной парой Target + Operation вызовет исключение
при построении словаря resolver. Один resolver выбирает одну реализацию, не запускает несколько.

### 7.2. Результаты и повторы

| Результат адаптера | Что делает command handler |
|---|---|
| `IntegrationSucceeded(externalId, statusCode)` | Succeeded, ExternalEntityId, CompletedAt; попытка Succeeded |
| `IntegrationTransientFailure(code, message, statusCode, retryAfter)` | RetryScheduled, если лимит не исчерпан; текущая попытка Failed |
| Та же временная ошибка на последней допустимой попытке | Failed, CompletedAt; попытка Failed |
| `IntegrationPermanentFailure(code, message, statusCode)` | Сразу Failed; попытка Failed |
| Неожиданное исключение адаптера | Превращается в transient failure `integration.unexpected_error` |
| Отмена через переданный CancellationToken | Попытка классифицируется как transient `integration.cancelled`; сохранение без отменённого token, затем исключение отмены пробрасывается |

Текущая retry policy: **не более 10 общих попыток**, базовая пауза **30 секунд**.
Если `RetryAfter` больше 30 секунд, используется он. Экспоненциального backoff и jitter нет.
Число попыток уже увеличено StartAttempt до проверки лимита.

503 не перехватывается автоматически где-то в worker. Реальный HTTP-адаптер должен
перевести такой ответ в `IntegrationTransientFailure`. Worker затем увидит RetryScheduled
в БД, когда наступит NextAttemptAt. Домен дополнительно запрещает старт раньше этого времени.

`Result.Success()` из ProcessOrderIntegrationCommand означает «попытка обработана и её итог
сохранён», а не «внешняя интеграция обязательно завершилась успешно».
Истинный бизнес-итог находится в IntegrationStatus/IntegrationAttempt.

Telegram mock ничего не отправляет по сети. На каждый запуск он случайно возвращает:

- 50% — success со случайным ExternalEntityId;
- 30% — transient failure, 503, RetryAfter 60 секунд;
- 20% — permanent failure, 400, без автоматического повтора.

Поэтому одна неуспешная попытка с 400 — ожидаемое поведение, а не пропущенный retry.
Mock не хранит полученные idempotency keys и сам не обеспечивает дедупликацию внешнего действия.

## 8. Диаграмма состояний OrderIntegration

```mermaid
stateDiagram-v2
    [*] --> Pending: Create без ожидания
    [*] --> WaitingForPrerequisite: Create с ожиданием
    WaitingForPrerequisite --> Pending: Release
    Pending --> Processing: StartAttempt / AttemptCount++
    RetryScheduled --> Processing: StartAttempt, когда наступил NextAttemptAt
    Processing --> Succeeded: MarkSucceeded
    Processing --> RetryScheduled: transient failure, лимит позволяет
    Processing --> Failed: permanent failure или лимит исчерпан
    Failed --> Pending: RequestManualRetry
    WaitingForPrerequisite --> Cancelled: Cancel
    Pending --> Cancelled: Cancel
    RetryScheduled --> Cancelled: Cancel
```

Release, RequestManualRetry и Cancel существуют как доменные методы, но публичных команд/API
для них сейчас нет. RequestManualRetry не сбрасывает AttemptCount: это общий счётчик истории.
Из Processing нет операции автоматического возврата по timeout.

Статус попытки проще: `Processing → Succeeded` либо `Processing → Failed`.
RetryScheduled — состояние задачи интеграции; завершившаяся неуспешная попытка остаётся Failed.

## 9. Данные, связи и идентификаторы

```mermaid
erDiagram
    ORDERS_DB_ORDERS ||--|{ ORDERS_DB_ORDER_LINES : owns
    ORDERS_DB_ORDERS ||..o{ ORDERS_DB_OUTBOX : "OrderId, логическая связь"
    INTEGRATIONS_DB_INTEGRATION_ORDERS ||--o{ INTEGRATIONS_DB_ORDER_INTEGRATIONS : "локальный FK OrderId"
    INTEGRATIONS_DB_ORDER_INTEGRATIONS ||--o{ INTEGRATIONS_DB_ATTEMPTS : "FK OrderIntegrationId"
    INTEGRATIONS_DB_INBOX {
        string Consumer PK
        uuid EventId PK
        timestamp ProcessedAt
    }
    INTEGRATIONS_DB_INTEGRATION_ORDERS {
        uuid Id PK
        jsonb Payload
        timestamp ReceivedAt
    }
```

Между двумя БД **нет FK и JOIN**. Id заказа в snapshot совпадает с Id оригинала,
но локальный FK OrderIntegrations указывает на IntegrationOrders в той же БД Integrations.
В outbox есть OrderId, однако EF-конфигурация не объявляет FK на Orders.
Inbox хранит идентификатор обработанного события, а не весь payload Kafka.

| Данные | Что хранится / ключевые ограничения |
|---|---|
| Orders | Источник, внешний номер, customer snapshot, сумма/валюта, статусы, даты; unique `(SourceSystem, ExternalOrderId)` |
| OrderLines | Позиции заказа, локальный FK, количество/цена/сумма |
| OutboxMessages | EventId как Id, OrderId, EventType, JSON payload, даты публикации/повтора, PublishAttempts, LastError |
| IntegrationOrders | OrderId как Id, JSON execution snapshot, ReceivedAt |
| InboxMessages | Unique/PK `(Consumer, EventId)` и ProcessedAt |
| OrderIntegrations | OrderId, Target/Operation/Status, стабильный IdempotencyKey, ExternalEntityId, AttemptCount, NextAttemptAt, последняя ошибка, даты |
| IntegrationAttempts | Id, OrderIntegrationId, AttemptNumber, Outcome, ResponseStatusCode, ErrorCode/Message, TraceId, StartedAt/CompletedAt |

Защита от повторов действует на разных уровнях:

| Идентификатор / ограничение | От какого повтора защищает |
|---|---|
| `(SourceSystem, ExternalOrderId)` | Не позволяет создать вторую запись того же внешнего заказа |
| EventId + inbox Consumer | Повторная доставка одного события не применяет план второй раз |
| `(OrderId, Target, Operation)` | Не позволяет создать второй одинаковый тип интеграции для заказа |
| `(Target, IdempotencyKey)` | Не позволяет назначить один ключ двум задачам одного Target |
| `(OrderIntegrationId, AttemptNumber)` | Не позволяет записать два одинаковых номера попытки |
| Version/xmin | Не позволяет двум конкурентным обработчикам успешно сохранить один переход из одной версии |
| IdempotencyKey, передаваемый внешнему API | Должен защищать внешний побочный эффект; требует поддержки в адаптере и внешней системе |

Unique constraint заказа предотвращает вторую строку, но текущий CreateOrderCommandHandler
не возвращает существующий заказ при повторном POST и не преобразует unique violation в Result.Conflict.
Поэтому **полноценного идемпотентного HTTP-контракта пока нет**; ошибка может дойти до общего 500.
Наличие поддержки 409 в BaseApiController или Web само по себе этот сценарий не реализует.

EventId, OrderId, IntegrationId, AttemptId — разные идентификаторы.
Один OrderId может иметь четыре IntegrationId, каждый — до нескольких AttemptId.
Все попытки одной IntegrationId используют один IdempotencyKey.

TraceId — диагностический идентификатор Activity, не ключ идемпотентности.
Worker создаёт Activity на обработку задачи; её 32-символьный TraceId сохраняется в попытке.
Следующий запуск может иметь новый TraceId. Сквозная трасса HTTP → Kafka → worker сейчас
не переносится через headers, и экспортёр OpenTelemetry не подключён.

## 10. Чтение и интерфейс оператора

```mermaid
sequenceDiagram
    participant B as Браузер
    participant W as Blazor Server
    participant O as Orders API
    participant I as Integrations API
    B->>W: Открыть /orders/{id} или обновить
    W->>O: GET /api/orders/{id}/details
    O-->>W: Заказ и позиции
    W->>I: GET /api/order-integrations?OrderId=id, pages по 100
    alt Integrations доступен
        I-->>W: Статусы интеграций
        W-->>B: Карточка: заказ + интеграции
    else Ошибка/timeout Integrations
        W-->>B: Заказ + сообщение о недоступности статусов
    end
    B->>W: Открыть историю интеграции
    W->>I: GET /api/order-integrations/attempts?OrderIntegrationId=id
    I-->>W: Страница попыток по 5, StartedAt DESC
    W-->>B: История попыток
```

Blazor работает в серверном interactive-режиме. Браузер общается с Web,
а HTTP-запросы к двум backend выполняет серверный `OrderBridgeApiClient`.
Это композиция ответов на стороне Web, отдельного API gateway/BFF-проекта нет.

Список заказов читает только Orders. Карточка сначала получает заказ, затем интеграции.
Если Integrations недоступен, `IntegrationsAvailable=false` и выводится предупреждение;
это отличается от доступного сервиса, вернувшего пустой список сразу после создания заказа.
Таймаут HTTP-клиента Orders — 30 секунд, Integrations — 5 секунд на запрос.
Автообновления статусов и push-уведомлений нет: используется ручное обновление.
Ответ двух сервисов не является единым транзакционным снимком.

| API | Маршруты | Локальный query/command |
|---|---|---|
| Orders | `POST /api/orders` | CreateOrderCommand |
| Orders | `GET /api/orders` | GetPagedOrdersQuery |
| Orders | `GET /api/orders/{id}` | GetOrderByIdQuery |
| Orders | `GET /api/orders/{id}/details` | GetOrderDetailsByIdQuery |
| Integrations | `GET /api/order-integrations` | GetPagedOrderIntegrationsQuery |
| Integrations | `GET /api/order-integrations/attempts` | GetPagedIntegrationAttemptsQuery |
| Integrations | `GET /api/order-integrations/attempts/{id}` | GetIntegrationAttemptByIdQuery |

Чтение использует фильтры, сортировку, PaginationResult и DTO/проекции без tracking.
Изменения агрегатов используют tracked repository и общий в рамках scope UnitOfWork.
CQRS здесь означает разные команды/запросы в коде, а не отдельную read database внутри каждого сервиса.

Контроллеры не отдают обёртку Result клиенту: успех превращается в DTO/страницу,
ошибки Result — в ProblemDetails с соответствующим HTTP-статусом.
FluentValidationException обрабатывается отдельно как 400 с полевыми ошибками.
Не все исключения БД/домена сейчас преобразованы в прикладные ошибки.

## 11. Что происходит при сбоях

| Сбой | Поведение текущего кода |
|---|---|
| Ошибка до коммита создания | Заказ и outbox откатываются вместе |
| Kafka недоступна | Заказы сохраняются; outbox остаётся неопубликованным и повторяется |
| Kafka приняла событие, процесс упал до PublishedAt | Событие может быть опубликовано повторно; inbox защищает применение |
| Integrations остановлен | События остаются в Kafka в пределах retention; после запуска чтение продолжается с offsets |
| Ошибка Integrations DB при планировании | Inbox/snapshot/jobs откатываются; offset не подтверждается |
| План сохранён, но offset не подтверждён | Событие придёт повторно; inbox не даст повторно применить его |
| Некорректное событие, обработанное как JsonException | DLQ, затем commit исходного offset |
| Временный ответ адаптера | Попытка Failed, задача RetryScheduled; позже worker запускает новую попытку |
| Постоянная ошибка или 10-я временная ошибка | Задача Failed, автоматическая выборка её больше не включает |
| Плавная отмена во время адаптера | Хендлер пытается сохранить transient cancellation и запланировать повтор |
| Жёсткое падение после сохранения старта | Возможны OrderIntegration=Processing и незавершённая IntegrationAttempt; автоматического восстановления нет |
| Внешняя система выполнила действие, но финальный SaveChanges не удался | Локальный итог неизвестен/Processing; для безопасного повторного вызова необходима внешняя идемпотентность |
| Orders API остановлен | Integrations продолжает выполнение по собственной БД и snapshot |
| Integrations API недоступен Web | Заказ отображается без статусов с явным предупреждением |

Следовательно, система обеспечивает **at least once для межсервисного события с дедупликацией
применения**, но не универсальное exactly once для всех внешних действий.

Жёсткий сбой после старта — существенная открытая часть надёжности. Нужны будут lease/timeout
и восстановление зависших Processing, а также проверка внешнего результата по стабильному ключу.
Это не реализовано текущими retry policy и concurrency token.

## 12. Развёртывание и эксплуатационные границы

| Контейнер | Внешний порт | Доступ к сетям Compose |
|---|---|---|
| web | 8090 | edge |
| orders-api | 8088 | edge, messaging, orders-data |
| integrations-api | 8089 | edge, messaging, integrations-data |
| orders-db | 127.0.0.1:5433 | orders-data |
| integrations-db | 127.0.0.1:5434 | integrations-data |
| kafka | 127.0.0.1:9092 | messaging |

Внутри Docker API слушают 8080, PostgreSQL — 5432, Kafka для приложений — `kafka:19092`.
У БД отдельные пользователи, базы и persistent volumes; API одного сервиса не подключён
к data-сети другого. Dockerfiles копируют только свой сервис, Contracts и BuildingBlocks.

API зависят при старте от здоровья своей БД, Web — от Orders API.
Kafka не включена в обязательную start-зависимость API: фоновые компоненты повторяют подключение.
В Compose установлено `ApplyMigrationsOnStartup=true`; в production миграции стоит выделить
в управляемый deployment-шаг. Это уже отмечено в README, но такой production pipeline пока не создан.

`/health/live` подтверждает работу HTTP-процесса; `/health/ready` проверяет подключение к своей БД.
Ready **не проверяет** Kafka lag, работу worker, наличие адаптеров или зависшие Processing.
Swagger включён в Development. Метрики, полноценный OTel exporter и аутентификация пока не подключены.

Outbox/inbox/attempts автоматически не очищаются. Kafka retention не заменяет политику
хранения аудита БД. Старый монолитный PostgreSQL после разового импорта сохранён отдельно;
он не участвует в runtime и не синхронизируется с новыми БД.

## 13. Как читать код самостоятельно

Для первого прохода достаточно следующей последовательности; каждый следующий файл отвечает
на вопрос, возникающий после предыдущего:

1. [OrderController](../B2BOrderBridge/src/Services/Orders/OrderBridge.Orders.Api/Controllers/OrderController.cs) — вход HTTP и ответы.
2. [CreateOrderCommand](../B2BOrderBridge/src/Services/Orders/OrderBridge.Orders.Application/Features/Orders/Commands/CreateOrderCommand.cs) — что именно сохраняется при создании.
3. [OrderCreatedV1](../B2BOrderBridge/src/Contracts/OrderBridge.Contracts/Events/OrderCreatedV1.cs) — контракт через границу сервисов.
4. [OrderCreatedOutbox](../B2BOrderBridge/src/Services/Orders/OrderBridge.Orders.Infrastructure/Messaging/OrderCreatedOutbox.cs) и [OutboxDispatcher](../B2BOrderBridge/src/Services/Orders/OrderBridge.Orders.Infrastructure/Messaging/OutboxDispatcher.cs) — как событие доходит до Kafka.
5. [OrderCreatedConsumer](../B2BOrderBridge/src/Services/Integrations/OrderBridge.Integrations.Infrastructure/Messaging/OrderCreatedConsumer.cs) — JSON, offsets, DLQ и повторное чтение.
6. [OrderCreatedProcessor](../B2BOrderBridge/src/Services/Integrations/OrderBridge.Integrations.Application/Messaging/OrderCreatedProcessor.cs), [planner](../B2BOrderBridge/src/Services/Integrations/OrderBridge.Integrations.Application/Features/OrderIntegrations/Services/Implementations/OrderIntegrationPlanner.cs), [IntegrationPlanningStore](../B2BOrderBridge/src/Services/Integrations/OrderBridge.Integrations.Infrastructure/Persistence/IntegrationPlanningStore.cs) — как появляются snapshot и задания.
7. [IntegrationWorker](../B2BOrderBridge/src/Services/Integrations/OrderBridge.Integrations.Infrastructure/BackgroundJobs/IntegrationWorker.cs) и [IntegrationJobStore](../B2BOrderBridge/src/Services/Integrations/OrderBridge.Integrations.Infrastructure/Services/IntegrationJobStore.cs) — как выбираются и запускаются задачи.
8. [ProcessOrderIntegrationCommand](../B2BOrderBridge/src/Services/Integrations/OrderBridge.Integrations.Application/Features/OrderIntegrations/Commands/ProcessOrderIntegrationCommand.cs) — две записи состояния, вызов адаптера, завершение попытки.
9. [OrderIntegration](../B2BOrderBridge/src/Services/Integrations/OrderBridge.Integrations.Domain/Models/OrderIntegration.cs) и [retry policy](../B2BOrderBridge/src/Services/Integrations/OrderBridge.Integrations.Application/Features/OrderIntegrations/Services/Implementations/BaseOrderIntegrationRetryPolicy.cs) — разрешённые переходы и расписание повторов.
10. [OrderBridgeApiClient](../B2BOrderBridge/src/Web/OrderBridge.Web/Services/OrderBridgeApiClient.cs) — сбор карточки из двух API.

Для проверки понимания удобно проследить один OrderId: найти Orders/Outbox в первой БД,
его EventId в inbox второй БД, snapshot, четыре задания и попытки Telegram.
Ни один отдельный статус не описывает всю цепочку: `PublishedAt` — доставка в Kafka,
inbox — применение события, `IntegrationStatus` — состояние задачи, `Outcome` — итог одной попытки.
