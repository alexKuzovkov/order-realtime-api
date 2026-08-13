using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using OrderRealtime.Api.Authentication;
using OrderRealtime.Api.Orders;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddMemoryCache();
builder.Services.AddProblemDetails();
// Только для самодостаточного demo: production-инстансы должны разделять постоянный key ring.
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();

// Демонстрационная схема. В production заменяется на AddJwtBearer().
builder.Services.AddAuthentication(DemoAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, DemoAuthenticationHandler>(
        DemoAuthenticationHandler.SchemeName, _ => { });
builder.Services.AddAuthorization();

builder.Services.Configure<OrderExpirationOptions>(
    builder.Configuration.GetSection(OrderExpirationOptions.SectionName));
builder.Services.AddSingleton<IOrderStore, MemoryOrderStore>();
builder.Services.AddSingleton<IOrderService, OrderService>();
builder.Services.AddHostedService<OrderExpirationService>();
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();
else
    app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<OrdersHub>("/hub/orders");

app.Run();
