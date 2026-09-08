# Shared building blocks

Общие библиотеки на .NET 10. Shared.Domain не зависит от фреймворков;
Shared.Application использует Shared.Domain и Mediator.Abstractions 3.0.2.

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

Используется [Mediator](https://github.com/martinothamar/Mediator), лицензия MIT.
Обработчики реализуют ValueTask<Result<T>> Handle(..., CancellationToken).
Mediator.SourceGenerator устанавливается только в исполняемый проект;
в Presentation уже есть AddMediator со scoped lifetime. Генератор автоматически
обнаруживает конкретные сообщения и обработчики в доступных сборках.
Для отдельных worker-проектов нужна своя регистрация; на обработку задания создавайте DI scope.

Пример будущего обработчика в OrderBridge.Application:

```csharp
using Shared.Application.Messaging;
using Shared.Application.Results;

public sealed record GetOrder(Guid Id) : IQuery<OrderDto>;

public sealed class GetOrderHandler(IOrderRepository orders)
    : IQueryHandler<GetOrder, OrderDto>
{
    public async ValueTask<Result<OrderDto>> Handle(
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
где sender — Mediator.ISender. Не подключайте одновременно using Mediator и
using Shared.Application.Messaging при объявлении контрактов: имена ICommand/IQuery совпадают.

Общие pipeline-контракты уже предоставляет Mediator.IPipelineBehavior<TMessage, TResponse>.
Конкретные validation/logging behaviors добавляются в Application по необходимости
и регистрируются через options.PipelineBehaviors в AddMediator.

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

В async Task<Result<T>> / async ValueTask<Result<T>> можно писать return value
или return error. В синхронном методе, возвращающем ValueTask<Result<T>>, используйте
ValueTask.FromResult<Result<T>>(value): generic-аргумент задаёт целевой тип преобразования.

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
