# Shared building blocks

Общие библиотеки на .NET 10. Shared.Domain не зависит от фреймворков;
Shared.Application использует Shared.Domain и MediatR 14.2.0.

## Domain

- Entity<TId>: идентификатор сущности; сравнение остаётся ссылочным.
- AggregateRoot<TId>, IAggregateRoot: граница согласованности и накопление событий.
- IDomainEvent: EventId и OccurredAt. Время передаёт вызывающий код, например через TimeProvider.
- DomainException: нарушение инварианта с машинным кодом ошибки.

Value objects реализуются через record. DomainEvents не являются надёжной очередью:
сначала обработайте или сохраните события транзакционно, затем вызовите ClearDomainEvents.
Медиатор работает внутри процесса и не заменяет inbox/outbox или фоновые задачи.

## Application: CQRS

Контракты находятся в Shared.Application.Messaging:

- ICommand / ICommandHandler<TCommand>: команда с Result.
- ICommand<T> / ICommandHandler<TCommand, T>: команда с Result<T>.
- IQuery<T> / IQueryHandler<TQuery, T>: запрос с Result<T>.

Используется [MediatR](https://github.com/LuckyPennySoftware/MediatR).
Shared ICommand/IQuery наследуют IRequest, обработчики — IRequestHandler.
Обработчики возвращают Task<Result<T>>; source generator не используется.
`OrderBridge.Application.AddApplication` сканирует всю сборку Application и автоматически
регистрирует command/query handlers. Lifetime медиатора и обработчиков настроен как Scoped,
поэтому они используют тот же scoped DbContext в рамках запроса.
Ключ лицензии можно передать через MediatR__LicenseKey; условия лицензии:
https://github.com/LuckyPennySoftware/MediatR/blob/master/LICENSE.md

Пример будущего обработчика в OrderBridge.Application:

```csharp
using Shared.Application.Messaging;
using Shared.Application.Results;

public sealed record GetOrder(Guid Id) : IQuery<OrderDto>;

public sealed class GetOrderHandler(IOrderRepository orders)
    : IQueryHandler<GetOrder, OrderDto>
{
    public async Task<Result<OrderDto>> Handle(
        GetOrder query, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(query.Id, cancellationToken);
        if (order is null)
            return new Error("order.not_found", "Order not found.", ErrorType.NotFound);

        return new OrderDto(order.Id);
    }
}

public sealed record OrderDto(Guid Id);
```

Из endpoint/controller вызывайте await sender.Send(new GetOrder(id), cancellationToken),
где sender — MediatR.ISender.

Pipeline-контракт — MediatR.IPipelineBehavior<TRequest, TResponse>.
Behaviors регистрируются через AddBehavior/AddOpenBehavior в AddMediatR.

## Results

Result / Result<T> представляют ожидаемый исход операции. Error содержит Code,
Description и ErrorType. У успешного результата Error == null; чтение Value у
ошибочного результата бросает InvalidOperationException.
Result<T>.Success допускает null, если T nullable; обязательность данных проверяет обработчик.
Неожиданные исключения не следует автоматически превращать в бизнес-ошибки.
HTTP-коды и сериализация ошибок относятся к Presentation, а не к Shared.

Неявные преобразования:
- T → Result<T>: успешный результат.
- Error → Result / Result<T>: ошибка с сохранением исходного Error.
- Для команды без значения успешный исход остаётся return Result.Success().

В async Task<Result<T>> можно писать return value
или return error. В синхронном методе, возвращающем Task<Result<T>>, используйте
Task.FromResult<Result<T>>(value): generic-аргумент задаёт целевой тип преобразования.

Null в переменной типа Error отклоняется. Nullable-значение типа T сохраняется как успех.
Не пишите return null: это null-ссылка на сам Result; используйте Success(null)
или переменную nullable-типа значения. Если T — Error, выбирайте явно Success(error)
или Failure(error), чтобы избежать неоднозначности двух операторов.
Обратного неявного преобразования Result<T> → T нет: значение читается через Value.

## Repositories

IReadRepository<TEntity, TId> предоставляет GetByIdAsync и ExistsAsync.
IRepository<TEntity, TId> наследует его и добавляет Add, Update, Remove.
Оба ограничены агрегатами: дочерние сущности изменяются через корень.

```csharp
public interface IOrderRepository : IRepository<Order, Guid>
{
    Task<Order?> GetByExternalIdAsync(
        string source, string externalId, CancellationToken cancellationToken = default);
}
```

Методы записи только регистрируют изменения. Фиксация — отдельный
IUnitOfWork.SaveChangesAsync. При EF tracking вызов Update для загруженного агрегата
обычно не нужен. Реализация репозитория и unit of work должна использовать один scoped DbContext.
IQueryable наружу не отдаётся; проекции и специфичные операции чтения объявляются отдельно.

## Pagination

PaginationRequest: PageNumber от 1, PageSize от 1 до 100 (по умолчанию 20), Offset.
PaginationResult<T>: Items, TotalCount (long), PageNumber, PageSize, TotalPages,
HasPreviousPage, HasNextPage. Коллекция копируется и доступна только для чтения.

Тип результата запроса списка: Result<PaginationResult<OrderDto>>.
Пустой набор имеет TotalPages == 0; страница за концом выдачи возвращает пустой Items.
TotalCount и Items должны относиться к одному набору данных; если нужна строгая
согласованность при конкурентных изменениях, обеспечьте её на уровне запроса/транзакции.
До Skip/Take задавайте стабильную сортировку с уникальным полем, например CreatedAt, Id.

## Проверка

Из каталога B2BOrderBridge:

```shell
dotnet build B2BOrderBridge.slnx
dotnet test B2BOrderBridge.slnx
```

Shared.Tests проверяет CQRS dispatch, DI scopes, cancellation, события агрегата,
защиту Result и граничные случаи пагинации. Примерные сообщения находятся только в тестах.

## Guard

Guard в Shared.Domain возвращает нормализованное значение: его нужно присвоить полю.
Он не изменяет исходные аргументы автоматически и не подключён как глобальный middleware.

```csharp
CompanyName = Guard.RequiredString(companyName, maxLength: 200);
ContactEmail = Guard.OptionalString(contactEmail, maxLength: 254);
Id = Guard.NotEmpty(id);
Quantity = Guard.Positive(quantity);
UnitPrice = Guard.Money(unitPrice);
PaymentAmount = Guard.Money(paymentAmount, allowZero: false);
DiscountPercent = Guard.InRange(discountPercent, 0m, 100m);
Status = Guard.DefinedEnum(status);
```

RequiredString делает Trim и проверяет длину после нормализации.
OptionalString преобразует null/пустую/пробельную строку в null.
Внутренние пробелы и регистр сохраняются. Длина измеряется в UTF-16 code units.
Трим нужно вызывать только для полей, где пробелы по краям незначимы;
не применяйте его к секретам, подписям и исходному webhook payload.
Проверка формата email/ИНН и других реквизитов остаётся отдельным бизнес-правилом.

Money принимает decimal, отклоняет отрицательные суммы и лишнюю дробную точность,
по умолчанию допускает ноль и два знака после запятой. Округления нет:
1.234 отклоняется, 1.230 принимается. decimalPlaces можно задать от 0 до 28
согласно валюте/типу цены; Guard не определяет валюту и ограничения колонки БД.
Скидки, возвраты и другие подписанные суммы требуют своего правила.
Округление рассчитанного итога задаётся отдельно в бизнес-логике.

Positive, NonNegative и InRange работают с числовыми типами и отклоняют NaN/Infinity.
InRange включает обе границы. NotNull проверяет ссылку, NotEmpty — Guid.
DefinedEnum допускает только объявленные значения; составные Flags требуют отдельного правила.

Неверные данные дают DomainException с кодом guard.* и именем аргумента
(CallerArgumentExpression; можно передать parameterName явно).
Неверная конфигурация самого Guard, например отрицательная длина, даёт ArgumentException.
Guard не возвращает Result: преобразование доменной ошибки в ответ приложения/API
нужно реализовать на границе обработки запроса.


## FluentValidation

ValidationBehaviour<TRequest,TResponse> подключается через AddOpenBehavior в AddMediatR.
`AddApplication` регистрирует все валидаторы сборки Application как Transient.
Проверки выполняются последовательно через ValidateAsync с CancellationToken:
валидаторы могут использовать один scoped DbContext.
При ошибках выбрасывается FluentValidation.ValidationException, handler не вызывается.
Presentation преобразует исключение в 400 ValidationProblemDetails с errors по полям,
code = validation.failed и traceId. Result остаётся для ожидаемых исходов handler.
Прямой вызов handler.Handle обходит pipeline — для автоматической проверки вызывайте ISender.Send.

CreateOrderCommandValidator проверяет обязательные данные, длины после Trim,
поддерживаемую валюту, покупателя, позиции, количество, точность цены и переполнение сумм.
Он не изменяет DTO; нормализацию по-прежнему выполняет домен.
Для контактного email дополнительно проверяется базовый формат. Проверка формата ИНН
остаётся отдельным бизнес-правилом.
