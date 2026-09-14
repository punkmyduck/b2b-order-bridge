# BaseApiController

Наследуйте BaseApiController и задавайте маршрут на конкретном контроллере.
Методы действий оставляйте типизированными: Task<ActionResult<OrderDetailsDto>>,
чтобы OpenAPI видел успешную модель. Ошибочные ответы документируйте атрибутами
ProducesResponseType(typeof(ProblemDetails), ...), соответствующими сценарию.

Примеры тела action после получения результата из ISender:

```csharp
return FromResult(result); // Result<T>: 200 с T; Result: 204 без тела
return FromPaginationResult(result); // 200 с Items и метаданными страницы
return FromCreatedResult(result, "GetOrder", dto => new { id = dto.Id });
return FromAcceptedResult(result, "GetOrder", dto => new { id = dto.Id });
```

Для Created/Accepted нужен существующий GET endpoint с Name = "GetOrder"
и совместимым route parameter id. Location формируется MVC при выполнении ответа.
201 означает создание ресурса; 202 — принятие фоновой работы.
Пустые страницы возвращают 200, а не 404.

ErrorType: Validation=400, NotFound=404, Conflict=409,
Unauthorized=401, Forbidden=403, Failure=500.
Тело ошибки — application/problem+json с code и traceId.
Для Failure детали логируются, клиент получает нейтральное сообщение.
Error.Description у остальных категорий должен быть безопасен для клиента.
401 из Result не заменяет authentication challenge: защищённые endpoints используют
Authorize и настроенную схему аутентификации, формирующую WWW-Authenticate.

Исключения из handler не являются Result и этими методами не перехватываются.
Для них нужен отдельный глобальный exception handler.
Автоматическая проверка ModelState обеспечивается ApiController и возвращает
стандартный ValidationProblemDetails; его errors — ошибки полей.
Result сейчас хранит одну Error без имени поля, поэтому FromError использует ProblemDetails.

Result<T>.Success(null) не интерпретируется как NotFound. При отсутствии ресурса
handler должен вернуть ErrorType.NotFound. Для явного отсутствия тела используйте Result.
