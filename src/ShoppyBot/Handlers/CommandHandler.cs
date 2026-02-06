using Telegram.Bot;
using Telegram.Bot.Types;
using ShoppyBot.Models;
using ShoppyBot.Services;
using ShoppyBot.Utils;
using BotUser = ShoppyBot.Models.User;

namespace ShoppyBot.Handlers;

public class CommandHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly IUserService _userService;
    private readonly IListService _listService;
    private readonly IItemService _itemService;
    private readonly IActivityLogService _activityLogService;
    private readonly INotificationService _notificationService;
    private readonly CallbackHandler _callbackHandler;
    private readonly ILogger<CommandHandler> _logger;

    public CommandHandler(
        ITelegramBotClient botClient,
        IUserService userService,
        IListService listService,
        IItemService itemService,
        IActivityLogService activityLogService,
        INotificationService notificationService,
        CallbackHandler callbackHandler,
        ILogger<CommandHandler> logger)
    {
        _botClient = botClient;
        _userService = userService;
        _listService = listService;
        _itemService = itemService;
        _activityLogService = activityLogService;
        _notificationService = notificationService;
        _callbackHandler = callbackHandler;
        _logger = logger;
    }

    public async Task HandleAsync(Message message, CancellationToken cancellationToken)
    {
        if (message.Text is not { } text)
            return;

        var from = message.From;
        if (from == null)
            return;

        var chatId = message.Chat.Id;
        var displayName = $"{from.FirstName} {from.LastName}".Trim();
        var user = await _userService.GetOrCreateUserAsync(from.Id, from.Username, displayName);

        try
        {
            // Check if this is a command or a response to a prompt
            if (text.StartsWith("/"))
            {
                // Cancel any pending conversation state when a new command is issued
                if (user.ConversationState != ConversationState.None)
                {
                    await _userService.ClearConversationStateAsync(user.Id);
                }

                var parts = text.Split(' ', 2);
                var command = parts[0].ToLower().Split('@')[0];
                var argument = parts.Length > 1 ? parts[1].Trim() : string.Empty;

                await HandleCommandAsync(user, chatId, command, argument, cancellationToken);
            }
            else
            {
                // Handle response based on conversation state
                await HandleConversationResponseAsync(user, chatId, text, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message");
            await _botClient.SendMessage(
                chatId: chatId,
                text: "❌ An error occurred. Please try again.",
                cancellationToken: cancellationToken);
        }
    }

    private async Task HandleCommandAsync(BotUser user, long chatId, string command, string argument, CancellationToken ct)
    {
        switch (command)
        {
            case "/start":
                await HandleStartAsync(user, chatId, argument, ct);
                break;
            case "/help":
                await SendMessageAsync(chatId, GetHelpText(), ct);
                break;
            case "/cancel":
                await HandleCancelAsync(user, chatId, ct);
                break;
            case "/newlist":
                await HandleNewListAsync(user, chatId, argument, ct);
                break;
            case "/mylists":
                await _callbackHandler.ShowListsAsync(chatId, user, ct);
                break;
            case "/list":
                await HandleListCommandAsync(user, chatId, ct);
                break;
            case "/add":
                await HandleAddAsync(user, chatId, argument, ct);
                break;
            // Legacy commands still work but show button UI
            case "/check":
            case "/uncheck":
            case "/hide":
            case "/show":
            case "/listall":
                await HandleListCommandAsync(user, chatId, ct);
                break;
            default:
                // Unknown command - ignore
                break;
        }
    }

    private async Task HandleConversationResponseAsync(BotUser user, long chatId, string text, CancellationToken ct)
    {
        switch (user.ConversationState)
        {
            case ConversationState.WaitingForListName:
                await ProcessNewListNameAsync(user, chatId, text, ct);
                break;

            case ConversationState.WaitingForItemName:
                await ProcessAddItemAsync(user, chatId, text, ct);
                break;

            case ConversationState.WaitingForNewListName:
                await ProcessRenameListAsync(user, chatId, text, ct);
                break;

            default:
                // Ignore messages when not expecting input
                break;
        }
    }

    private async Task HandleStartAsync(BotUser user, long chatId, string argument, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(argument) && argument.StartsWith("join_"))
        {
            var token = argument.Substring(5);
            var joined = await _listService.JoinListAsync(user.Id, token);

            if (joined)
            {
                var list = await _listService.GetListByShareTokenAsync(token);
                if (list != null)
                {
                    await _userService.SetCurrentListAsync(user.Id, list.Id);
                    await _activityLogService.LogActivityAsync(list.Id, user.Id, ActionType.UserJoined);
                    await _notificationService.NotifyListUsersAsync(list.Id, user.Id, ActionType.UserJoined, user.DisplayName);

                    await SendMessageAsync(chatId, $"✅ You've joined the list \"{list.Name}\"!", ct);
                    await _callbackHandler.ShowItemsAsync(chatId, user, ct);
                    return;
                }
            }

            await SendMessageAsync(chatId, "❌ Invalid or expired invite link.", ct);
            return;
        }

        await SendMessageAsync(chatId,
            "👋 Welcome to Shoppy Bot!\n\n" +
            "I help you manage shared shopping lists with friends and family.\n\n" +
            "Commands:\n" +
            "/newlist - Create a new list\n" +
            "/mylists - View your lists\n" +
            "/help - See all commands", ct);
    }

    private string GetHelpText()
    {
        return "📖 Available Commands:\n\n" +
               "📋 List Management:\n" +
               "/newlist - Create a new list\n" +
               "/mylists - View and manage your lists\n" +
               "/list - View current list items\n" +
               "/add - Add item to current list\n\n" +
               "💡 Tip: Use the buttons in /mylists and /list for easy navigation!\n\n" +
               "/cancel - Cancel current operation\n" +
               "/help - Show this help";
    }

    private async Task HandleCancelAsync(BotUser user, long chatId, CancellationToken ct)
    {
        if (user.ConversationState == ConversationState.None)
        {
            await SendMessageAsync(chatId, "Nothing to cancel.", ct);
            return;
        }

        await _userService.ClearConversationStateAsync(user.Id);
        await SendMessageAsync(chatId, "✅ Cancelled.", ct);
    }

    private async Task HandleNewListAsync(BotUser user, long chatId, string name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            await _userService.SetConversationStateAsync(user.Id, ConversationState.WaitingForListName, "new_back_to_lists");
            await SendMessageAsync(chatId, "📝 What would you like to name your new list?", ct);
            return;
        }

        await ProcessNewListNameAsync(user, chatId, name, ct);
    }

    private async Task ProcessNewListNameAsync(BotUser user, long chatId, string name, CancellationToken ct)
    {
        var pendingAction = user.PendingAction;
        await _userService.ClearConversationStateAsync(user.Id);

        var list = await _listService.CreateListAsync(user.Id, name);
        await _userService.SetCurrentListAsync(user.Id, list.Id);
        await _activityLogService.LogActivityAsync(list.Id, user.Id, ActionType.ListCreated, name);

        await SendMessageAsync(chatId, $"✅ List \"{name}\" created!", ct);

        // Show appropriate screen based on where they came from
        if (pendingAction == "new_back_to_lists")
        {
            await _callbackHandler.ShowListsAsync(chatId, user, ct);
        }
        else
        {
            await _callbackHandler.ShowItemsAsync(chatId, user, ct);
        }
    }

    private async Task HandleListCommandAsync(BotUser user, long chatId, CancellationToken ct)
    {
        var list = await _userService.GetCurrentListAsync(user.Id);
        if (list == null)
        {
            await SendMessageAsync(chatId, "❌ No list selected. Use /mylists to select a list.", ct);
            return;
        }

        await _callbackHandler.ShowItemsAsync(chatId, user, ct);
    }

    private async Task HandleAddAsync(BotUser user, long chatId, string itemName, CancellationToken ct)
    {
        var list = await _userService.GetCurrentListAsync(user.Id);
        if (list == null)
        {
            await SendMessageAsync(chatId, "❌ No list selected. Use /mylists to select a list.", ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(itemName))
        {
            await _userService.SetConversationStateAsync(user.Id, ConversationState.WaitingForItemName, "add_back_to_manage");
            await SendMessageAsync(chatId, $"🛒 What item would you like to add to \"{list.Name}\"?", ct);
            return;
        }

        await ProcessAddItemAsync(user, chatId, itemName, ct);
    }

    private async Task ProcessAddItemAsync(BotUser user, long chatId, string itemName, CancellationToken ct)
    {
        var pendingAction = user.PendingAction;
        await _userService.ClearConversationStateAsync(user.Id);

        var list = await _userService.GetCurrentListAsync(user.Id);
        if (list == null)
        {
            await SendMessageAsync(chatId, "❌ No list selected. Use /mylists to select a list.", ct);
            return;
        }

        var item = await _itemService.AddItemAsync(list.Id, user.Id, itemName);
        await _activityLogService.LogActivityAsync(list.Id, user.Id, ActionType.ItemAdded, itemName);
        await _notificationService.NotifyListUsersAsync(list.Id, user.Id, ActionType.ItemAdded, user.DisplayName, itemName);

        await SendMessageAsync(chatId, $"✅ Added: {itemName}", ct);

        // Show manage items menu or items list based on where they came from
        if (pendingAction == "add_back_to_manage")
        {
            await _callbackHandler.ShowManageItemsAsync(chatId, user, ct);
        }
        else
        {
            await _callbackHandler.ShowItemsAsync(chatId, user, ct);
        }
    }

    private async Task ProcessRenameListAsync(BotUser user, long chatId, string newName, CancellationToken ct)
    {
        var pendingAction = user.PendingAction;
        await _userService.ClearConversationStateAsync(user.Id);

        var list = await _userService.GetCurrentListAsync(user.Id);
        if (list == null)
        {
            await SendMessageAsync(chatId, "❌ No list selected.", ct);
            return;
        }

        if (!await _listService.IsAdminAsync(list.Id, user.Id))
        {
            await SendMessageAsync(chatId, "❌ Only admins can rename the list.", ct);
            return;
        }

        var oldName = list.Name;
        await _listService.RenameListAsync(list.Id, user.Id, newName);
        await _activityLogService.LogActivityAsync(list.Id, user.Id, ActionType.ListRenamed, $"{oldName} → {newName}");
        await _notificationService.NotifyListUsersAsync(list.Id, user.Id, ActionType.ListRenamed, user.DisplayName, $"{oldName} → {newName}");

        await SendMessageAsync(chatId, $"✅ List renamed to \"{newName}\"", ct);

        // Go back to manage list screen
        if (pendingAction == "rename_back_to_manage")
        {
            await _callbackHandler.ShowManageListAsync(chatId, user, ct);
        }
    }

    private async Task SendMessageAsync(long chatId, string text, CancellationToken ct)
    {
        await _botClient.SendMessage(chatId, text, cancellationToken: ct);
    }
}
