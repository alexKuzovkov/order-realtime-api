# Задание 2. Обновления ордеров через SignalR

## Состав решения

- `OrdersController` — принимает `POST /api/orders` и передает операцию сервису;
- `OrdersHub` — подключает все соединения пользователя к его SignalR-группе и отправляет начальный список;
- `OrderService` — создает и отменяет ордера, затем публикует typed SignalR events;
- `MemoryOrderStore` — хранит ордера в `IMemoryCache`, используя `ConcurrentDictionary` для конкурентного доступа;
- `OrderExpirationService` — периодически находит просроченные ордера и отменяет их;
- `wwwroot/index.html` — минимальная страница ручной проверки.

## Основные решения

### Пользователь определяется сервером

`UserId` не принимается в `CreateOrderRequest`. REST endpoint и SignalR Hub получают один и тот же `ClaimTypes.NameIdentifier` из аутентифицированного пользователя. Поэтому клиент не может указать владельца ордера в payload.

В примере используется демонстрационная authentication scheme по `X-User-Id` для REST и `?userId=` для WebSocket handshake. Это позволяет запустить пример без Identity Provider. В production она заменяется на JWT Bearer; контроллер, Hub и сервисы при этом не меняются.

По той же причине demo использует ephemeral Data Protection provider и консольное логирование. Это исключает зависимость примера от пользовательского Windows keyring. В production ключи Data Protection должны быть постоянными и общими для всех экземпляров приложения.

### Typed Hub

```csharp
public interface IOrderClient
{
    Task ReceiveOrderUpdate(OrderDto order);
    Task ReceiveInitialOrders(IReadOnlyCollection<OrderDto> orders);
}

public sealed class OrdersHub : Hub<IOrderClient>
```

Typed Hub дает compile-time проверку имен и аргументов клиентских методов. Строковые вызовы вида `SendAsync("ReceiveOrderUpdate", ...)` не используются.

### Одна группа на пользователя

При подключении соединение добавляется в группу `orders:user:{userId}`. Обновление отправляется группе, поэтому его одновременно получают все вкладки и устройства пользователя, но не другие пользователи.

Сначала соединение добавляется в группу, затем получает snapshot активных ордеров. Такой порядок не допускает потери обновления между чтением snapshot и подпиской; в редкой гонке клиент может увидеть повтор, поэтому реальные клиенты также должны обрабатывать события идемпотентно по `Order.Id`.

### Потокобезопасный store

`IMemoryCache` содержит один `ConcurrentDictionary<Guid, Order>`. Сам словарь защищает добавление и перечисление, а переход `IsActive: true → false` выполняется через `Interlocked.Exchange`:

```csharp
public bool TryCancel() => Interlocked.Exchange(ref _isActive, 0) == 1;
```

Поэтому два конкурентных прохода фонового сервиса не смогут дважды отменить один ордер и дважды отправить событие.

### Один фоновый цикл вместо таймера на каждый ордер

`OrderExpirationService : BackgroundService` использует один `PeriodicTimer`, на каждом tick получает просроченные активные ордера и вызывает публичную операцию `CancelOrderAsync`. Логика отмены и отправки события не дублируется в background worker.

Время получаем через `TimeProvider`, а интервалы — из конфигурации:

```json
"OrderExpiration": {
  "LifetimeSeconds": 15,
  "ScanIntervalSeconds": 1
}
```

### DTO вместо domain model

SignalR и REST возвращают `OrderDto`, а не изменяемую внутреннюю сущность. Контракт не раскрывает `UserId` и не зависит от способа хранения состояния.

## Ключевые фрагменты

### Hub

```csharp
[Authorize]
public sealed class OrdersHub(IOrderService orderService) : Hub<IOrderClient>
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new HubException("Authenticated user identifier is missing.");

        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
        await Clients.Caller.ReceiveInitialOrders(orderService.GetActiveOrders(userId));
        await base.OnConnectedAsync();
    }

    public static string UserGroup(string userId) => $"orders:user:{userId}";
}
```

### Контроллер

```csharp
[ApiController]
[Authorize]
[Route("api/orders")]
public sealed class OrdersController(IOrderService orderService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<OrderDto>> CreateOrderAsync(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException();

        var order = await orderService.CreateOrderAsync(userId, request, cancellationToken);
        return Created($"/api/orders/{order.Id}", order);
    }
}
```

### Регистрация

```csharp
builder.Services.AddSignalR();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IOrderStore, MemoryOrderStore>();
builder.Services.AddSingleton<IOrderService, OrderService>();
builder.Services.AddHostedService<OrderExpirationService>();
builder.Services.AddSingleton(TimeProvider.System);

app.MapControllers();
app.MapHub<OrdersHub>("/hub/orders");
```

`OrderService` и store зарегистрированы как Singleton: состояние едино для REST, Hub и background worker. Эти классы не зависят от scoped-сервисов и разработаны для конкурентного использования.

## Проверка

```bash
dotnet run --project src/OrderRealtime.Api
```

Открыть URL приложения в браузере и нажать:

1. `Connect` — придет `ReceiveInitialOrders`;
2. `Create order` — REST вернет `201`, а Hub сразу пришлет активный ордер;
3. подождать около 15 секунд — Hub пришлет тот же ордер с `isActive: false`.

REST можно проверить отдельно:

```bash
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -H "X-User-Id: demo-user" \
  -d '{"symbol":"AAPL","price":225.50,"volume":10}'
```

Список активных ордеров текущего пользователя:

```bash
curl http://localhost:5000/api/orders/active -H "X-User-Id: demo-user"
```

Сразу после создания список содержит ордер, примерно через 15 секунд — уже нет.

Для проверки групп нужно открыть две вкладки с одинаковым `User ID` и одну с другим. Первые две получат обновление, третья — нет.

## Ограничения in-memory варианта

Решение соответствует условиям задания, но состояние теряется при перезапуске и не разделяется между несколькими экземплярами приложения. В production ордера хранятся в БД, обновление публикуется после фиксации транзакции через Outbox, а SignalR масштабируется через Redis backplane или Azure SignalR Service.
