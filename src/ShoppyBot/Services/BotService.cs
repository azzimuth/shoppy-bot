using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using ShoppyBot.Handlers;

namespace ShoppyBot.Services;

public class BotService : BackgroundService
{
    private readonly ITelegramBotClient _botClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BotService> _logger;
    private readonly IConfiguration _configuration;

    public BotService(
        ITelegramBotClient botClient,
        IServiceProvider serviceProvider,
        ILogger<BotService> logger,
        IConfiguration configuration)
    {
        _botClient = botClient;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var webhookUrl = _configuration["TelegramBot:WebhookUrl"];

        if (!string.IsNullOrEmpty(webhookUrl))
        {
            _logger.LogInformation("Setting up webhook at {WebhookUrl}", webhookUrl);
            await _botClient.SetWebhook(
                url: $"{webhookUrl}/api/webhook",
                cancellationToken: stoppingToken);
        }
        else
        {
            _logger.LogInformation("Starting bot in polling mode");
            await _botClient.DeleteWebhook(cancellationToken: stoppingToken);

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery]
            };

            _botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandleErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: stoppingToken);

            var me = await _botClient.GetMe(stoppingToken);
            _logger.LogInformation("Bot @{Username} started in polling mode", me.Username);
        }

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        try
        {
            if (update.Message is { } message)
            {
                var commandHandler = scope.ServiceProvider.GetRequiredService<CommandHandler>();
                await commandHandler.HandleAsync(message, cancellationToken);
            }
            else if (update.CallbackQuery is { } callbackQuery)
            {
                var callbackHandler = scope.ServiceProvider.GetRequiredService<CallbackHandler>();
                await callbackHandler.HandleAsync(callbackQuery, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling update {UpdateId}", update.Id);
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Error receiving update from {Source}", source);
        return Task.CompletedTask;
    }
}
