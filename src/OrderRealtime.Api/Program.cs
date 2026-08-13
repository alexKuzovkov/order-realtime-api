using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using OrderRealtime.Api.Authentication;
using OrderRealtime.Api.Infrastructure;
using OrderRealtime.Api.Orders;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var signalR = builder.Services.AddSignalR();
var redisConnection = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConnection))
{
    var redisOptions = ConfigurationOptions.Parse(redisConnection);
    redisOptions.AbortOnConnectFail = false;
    redisOptions.ConnectRetry = 3;
    redisOptions.ConnectTimeout = 5_000;
    redisOptions.AsyncTimeout = 5_000;
    redisOptions.ReconnectRetryPolicy = new ExponentialRetry(1_000);

    signalR.AddStackExchangeRedis(options => options.Configuration = redisOptions);
}
builder.Services.AddMemoryCache();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHealthChecks();
// Только для самодостаточного demo: production-инстансы должны разделять постоянный key ring.
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();

// Демонстрационная схема. В production заменяется на AddJwtBearer().
builder.Services.AddAuthentication(DemoAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, DemoAuthenticationHandler>(
        DemoAuthenticationHandler.SchemeName, _ => { });
builder.Services.AddAuthorization();

builder.Services.AddOptions<OrderExpirationOptions>()
    .BindConfiguration(OrderExpirationOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<OrderLimitsOptions>()
    .BindConfiguration(OrderLimitsOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<IOrderStore, MemoryOrderStore>();
builder.Services.AddSingleton<IOrderUpdateNotifier, SignalROrderUpdateNotifier>();
builder.Services.AddSingleton<IOrderBusinessValidator, OrderBusinessValidator>();
builder.Services.AddSingleton<IOrderService, OrderService>();
builder.Services.AddHostedService<OrderExpirationService>();
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<OrdersHub>("/hub/orders");
app.MapHealthChecks("/health");

app.Run();
