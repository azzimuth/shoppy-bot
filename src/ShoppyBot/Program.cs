using Microsoft.EntityFrameworkCore;
using Serilog;
using Telegram.Bot;
using ShoppyBot.Data;
using ShoppyBot.Handlers;
using ShoppyBot.Services;

DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<ShoppyBotContext>(options =>
    options.UseNpgsql(connectionString));

var botToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN")
    ?? builder.Configuration["TelegramBot:Token"];

if (string.IsNullOrEmpty(botToken))
{
    throw new InvalidOperationException("TELEGRAM_BOT_TOKEN is not configured");
}

builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(botToken));

builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IListService, ListService>();
builder.Services.AddScoped<IItemService, ItemService>();
builder.Services.AddScoped<IActivityLogService, ActivityLogService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

builder.Services.AddScoped<CommandHandler>();
builder.Services.AddScoped<CallbackHandler>();

builder.Services.AddHostedService<BotService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ShoppyBotContext>();
    await db.Database.MigrateAsync();
}

app.UseSerilogRequestLogging();

app.MapPost("/api/webhook", async (
    HttpContext context,
    ITelegramBotClient botClient,
    IServiceProvider serviceProvider,
    CancellationToken cancellationToken) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync(cancellationToken);

    var update = System.Text.Json.JsonSerializer.Deserialize<Telegram.Bot.Types.Update>(body,
        new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

    if (update != null)
    {
        var botService = serviceProvider.GetRequiredService<BotService>();
        await botService.HandleUpdateAsync(botClient, update, cancellationToken);
    }

    return Results.Ok();
});

app.MapGet("/health", () => Results.Ok("Healthy"));

app.Run();
