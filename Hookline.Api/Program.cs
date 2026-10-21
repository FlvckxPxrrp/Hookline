using Hookline.Application.Services;
using Hookline.Infrastructure.BackgroundJobs;
using Hookline.Infrastructure.Persistence;
using Hookline.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Добавляем сервисы в контейнер (DI)
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 2. Подключение к PostgreSQL
// Строка подключения берется из appsettings.Development.json или user-secrets
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddHostedService<WebhookDeliveryWorker>();
builder.Services.AddScoped<ISignatureValidator, GitHubSignatureValidator>();

// 3. Health Checks
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Postgres")!);

var app = builder.Build();

// 4. Настройка конвейера обработки запросов (Middleware)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// 5. Эндпоинт для проверки здоровья
app.MapHealthChecks("/health");

app.Run();