using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using OrderRealtime.Api.Authentication;
using OrderRealtime.Api.Infrastructure;
using OrderRealtime.Api.Orders;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var signalR = builder.Services.AddSignalR();
var redisConnection = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConnection))
    signalR.AddStackExchangeRedis(redisConnection);
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

builder.Services.Configure<OrderExpirationOptions>(
    builder.Configuration.GetSection(OrderExpirationOptions.SectionName));
builder.Services.Configure<OrderLimitsOptions>(
    builder.Configuration.GetSection(OrderLimitsOptions.SectionName));
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
