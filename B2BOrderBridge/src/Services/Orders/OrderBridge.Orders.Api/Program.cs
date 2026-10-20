using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using OrderBridge.Orders.Application;
using OrderBridge.Orders.Infrastructure;
using OrderBridge.Orders.Infrastructure.Persistence;
using OrderBridge.Orders.Api.ExceptionHandlers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication(builder.Configuration["MediatR:LicenseKey"]);
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddMessaging(builder.Configuration);
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (builder.Configuration.GetValue<bool>("Persistence:ApplyMigrationsOnStartup"))
{
    await app.Services.ApplyPersistenceMigrationsAsync();
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health/live", () => Results.Ok(new { service = "Orders" }));
app.MapGet(
    "/health/ready",
    async (
        OrdersDbContext db,
        CancellationToken ct) => await db.Database.CanConnectAsync(ct) ? Results.Ok() : Results.StatusCode(503));
app.MapControllers();

await app.RunAsync();

public partial class Program;
