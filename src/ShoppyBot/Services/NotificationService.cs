using System.Collections.Concurrent;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using ShoppyBot.Models;
using ShoppyBot.Utils;

namespace ShoppyBot.Services;

public interface INotificationService
{
    Task NotifyListUsersAsync(int listId, int excludeUserId, ActionType actionType, string userName, string? details = null);
    Task SendMessageAsync(long chatId, string message);
}

public class NotificationService : INotificationService
{
    private readonly ITelegramBotClient _botClient;
    private readonly IListService _listService;
    private readonly ILogger<NotificationService> _logger;
    private readonly ConcurrentDictionary<int, DateTime> _lastNotification = new();
    private static readonly TimeSpan ThrottleInterval = TimeSpan.FromSeconds(2);

    public NotificationService(
        ITelegramBotClient botClient,
        IListService listService,
        ILogger<NotificationService> logger)
    {
        _botClient = botClient;
        _listService = listService;
        _logger = logger;
    }

    public async Task NotifyListUsersAsync(int listId, int excludeUserId, ActionType actionType, string userName, string? details = null)
    {
        if (_lastNotification.TryGetValue(listId, out var lastTime))
        {
            if (DateTime.UtcNow - lastTime < ThrottleInterval)
            {
                await Task.Delay(ThrottleInterval);
            }
        }

        _lastNotification[listId] = DateTime.UtcNow;

        var users = await _listService.GetListUsersAsync(listId);
        var message = MessageFormatter.FormatNotification(actionType, userName, details);

        foreach (var user in users.Where(u => u.Id != excludeUserId))
        {
            try
            {
                await _botClient.SendMessage(
                    chatId: user.TelegramId,
                    text: message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send notification to user {UserId}", user.Id);
            }
        }
    }

    public async Task SendMessageAsync(long chatId, string message)
    {
        try
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send message to chat {ChatId}", chatId);
        }
    }
}
