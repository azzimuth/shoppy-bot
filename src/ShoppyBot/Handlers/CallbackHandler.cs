using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using ShoppyBot.Models;
using ShoppyBot.Services;
using ShoppyBot.Utils;
using BotUser = ShoppyBot.Models.User;

namespace ShoppyBot.Handlers;

public class CallbackHandler
{
    private const int ItemsPerPage = 6;
    private const int UsersPerPage = 6;
    private const int ListsPerPage = 6;

    private readonly ITelegramBotClient _botClient;
    private readonly IUserService _userService;
    private readonly IListService _listService;
    private readonly IItemService _itemService;
    private readonly IActivityLogService _activityLogService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<CallbackHandler> _logger;

    public CallbackHandler(
        ITelegramBotClient botClient,
        IUserService userService,
        IListService listService,
        IItemService itemService,
        IActivityLogService activityLogService,
        INotificationService notificationService,
        ILogger<CallbackHandler> logger)
    {
        _botClient = botClient;
        _userService = userService;
        _listService = listService;
        _itemService = itemService;
        _activityLogService = activityLogService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task HandleAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Data is not { } data)
            return;

        if (callbackQuery.Message is not { } message)
            return;

        var from = callbackQuery.From;
        var displayName = $"{from.FirstName} {from.LastName}".Trim();
        var user = await _userService.GetOrCreateUserAsync(from.Id, from.Username, displayName);
        var chatId = message.Chat.Id;
        var messageId = message.MessageId;

        try
        {
            var parts = data.Split(':');
            var action = parts[0];

            switch (action)
            {
                case "lists":
                    await HandleListsScreenAsync(callbackQuery, user, chatId, messageId, parts, cancellationToken);
                    break;
                case "list":
                    await HandleListActionAsync(callbackQuery, user, chatId, messageId, parts, cancellationToken);
                    break;
                case "items":
                    await HandleItemsScreenAsync(callbackQuery, user, chatId, messageId, parts, cancellationToken);
                    break;
                case "item":
                    await HandleItemActionAsync(callbackQuery, user, chatId, messageId, parts, cancellationToken);
                    break;
                case "manage":
                    await HandleManageScreenAsync(callbackQuery, user, chatId, messageId, parts, cancellationToken);
                    break;
                case "visibility":
                    await HandleVisibilityScreenAsync(callbackQuery, user, chatId, messageId, parts, cancellationToken);
                    break;
                case "users":
                    await HandleUsersScreenAsync(callbackQuery, user, chatId, messageId, parts, cancellationToken);
                    break;
                case "user":
                    await HandleUserActionAsync(callbackQuery, user, chatId, messageId, parts, cancellationToken);
                    break;
                case "noop":
                    await _botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
                    break;
                default:
                    await _botClient.AnswerCallbackQuery(callbackQuery.Id, "Unknown action", cancellationToken: cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling callback {Data}", data);
            await _botClient.AnswerCallbackQuery(callbackQuery.Id, "An error occurred.", cancellationToken: cancellationToken);
        }
    }

    #region Lists Screen

    private async Task HandleListsScreenAsync(CallbackQuery query, BotUser user, long chatId, int messageId, string[] parts, CancellationToken ct)
    {
        var page = parts.Length > 1 && int.TryParse(parts[1], out var p) ? p : 0;
        await ShowListsScreenAsync(query, user, chatId, messageId, page, ct);
    }

    private async Task ShowListsScreenAsync(CallbackQuery? query, BotUser user, long chatId, int? messageId, int page, CancellationToken ct)
    {
        var allLists = (await _listService.GetUserListsAsync(user.Id)).ToList();
        var totalPages = (int)Math.Ceiling(allLists.Count / (double)ListsPerPage);
        page = Math.Max(0, Math.Min(page, totalPages - 1));

        var listsOnPage = allLists.Skip(page * ListsPerPage).Take(ListsPerPage).ToList();

        var buttons = new List<List<InlineKeyboardButton>>();

        // Add list buttons in 2 columns
        for (int i = 0; i < listsOnPage.Count; i += 2)
        {
            var row = new List<InlineKeyboardButton>();
            row.Add(InlineKeyboardButton.WithCallbackData(
                $"📋 {listsOnPage[i].List.Name}",
                $"list:select:{listsOnPage[i].List.Id}"));

            if (i + 1 < listsOnPage.Count)
            {
                row.Add(InlineKeyboardButton.WithCallbackData(
                    $"📋 {listsOnPage[i + 1].List.Name}",
                    $"list:select:{listsOnPage[i + 1].List.Id}"));
            }
            buttons.Add(row);
        }

        // Navigation row
        if (totalPages > 1)
        {
            var navRow = new List<InlineKeyboardButton>();
            if (page > 0)
                navRow.Add(InlineKeyboardButton.WithCallbackData("⬅️ Previous", $"lists:{page - 1}"));
            else
                navRow.Add(InlineKeyboardButton.WithCallbackData(" ", "noop"));

            if (page < totalPages - 1)
                navRow.Add(InlineKeyboardButton.WithCallbackData("Next ➡️", $"lists:{page + 1}"));
            else
                navRow.Add(InlineKeyboardButton.WithCallbackData(" ", "noop"));

            buttons.Add(navRow);
        }

        var keyboard = new InlineKeyboardMarkup(buttons);
        var text = allLists.Count == 0
            ? "📋 You don't have any lists yet.\n\nUse /newlist to create one."
            : $"📋 Your Lists ({page + 1}/{totalPages})";

        if (query != null && messageId.HasValue)
        {
            await _botClient.AnswerCallbackQuery(query.Id, cancellationToken: ct);
            await _botClient.EditMessageText(chatId, messageId.Value, text, replyMarkup: keyboard, cancellationToken: ct);
        }
        else
        {
            await _botClient.SendMessage(chatId, text, replyMarkup: keyboard, cancellationToken: ct);
        }
    }

    #endregion

    #region List Actions

    private async Task HandleListActionAsync(CallbackQuery query, BotUser user, long chatId, int messageId, string[] parts, CancellationToken ct)
    {
        if (parts.Length < 2) return;

        var action = parts[1];
        switch (action)
        {
            case "select":
                if (parts.Length > 2 && int.TryParse(parts[2], out var listId))
                    await HandleListSelectAsync(query, user, chatId, messageId, listId, ct);
                break;
            case "share":
                await HandleListShareAsync(query, user, chatId, messageId, ct);
                break;
            case "rename":
                await HandleListRenameAsync(query, user, chatId, ct);
                break;
            case "delete":
                await HandleListDeleteAsync(query, user, chatId, messageId, ct);
                break;
            case "confirmdelete":
                await HandleListConfirmDeleteAsync(query, user, chatId, messageId, ct);
                break;
            case "leave":
                await HandleListLeaveAsync(query, user, chatId, messageId, ct);
                break;
            case "new":
                await HandleListNewAsync(query, user, chatId, ct);
                break;
            case "log":
                await HandleListLogAsync(query, user, chatId, messageId, ct);
                break;
        }
    }

    private async Task HandleListSelectAsync(CallbackQuery query, BotUser user, long chatId, int messageId, int listId, CancellationToken ct)
    {
        if (!await _listService.HasAccessAsync(listId, user.Id))
        {
            await _botClient.AnswerCallbackQuery(query.Id, "Access denied", cancellationToken: ct);
            return;
        }

        await _userService.SetCurrentListAsync(user.Id, listId);
        await ShowItemsScreenAsync(query, user, chatId, messageId, 0, ct);
    }

    private async Task HandleListShareAsync(CallbackQuery query, BotUser user, long chatId, int messageId, CancellationToken ct)
    {
        var list = await _userService.GetCurrentListAsync(user.Id);
        if (list == null)
        {
            await _botClient.AnswerCallbackQuery(query.Id, "No list selected", cancellationToken: ct);
            return;
        }

        if (!await _listService.IsAdminAsync(list.Id, user.Id))
        {
            await _botClient.AnswerCallbackQuery(query.Id, "Only admins can share", cancellationToken: ct);
            return;
        }

        var botInfo = await _botClient.GetMe(ct);
        var inviteLink = $"https://t.me/{botInfo.Username}?start=join_{list.ShareToken}";

        await _botClient.AnswerCallbackQuery(query.Id, "Link generated!", cancellationToken: ct);
        await _botClient.SendMessage(chatId,
            $"🔗 Share this link to invite others to \"{list.Name}\":\n\n{inviteLink}",
            cancellationToken: ct);
    }

    private async Task HandleListRenameAsync(CallbackQuery query, BotUser user, long chatId, CancellationToken ct)
    {
        var list = await _userService.GetCurrentListAsync(user.Id);
        if (list == null || !await _listService.IsAdminAsync(list.Id, user.Id))
        {
            await _botClient.AnswerCallbackQuery(query.Id, "Only admins can rename", cancellationToken: ct);
            return;
        }

        await _userService.SetConversationStateAsync(user.Id, ConversationState.WaitingForNewListName, "rename_back_to_manage");
        await _botClient.AnswerCallbackQuery(query.Id, cancellationToken: ct);
        await _botClient.SendMessage(chatId, $"✏️ Enter the new name for \"{list.Name}\":", cancellationToken: ct);
    }

    private async Task HandleListDeleteAsync(CallbackQuery query, BotUser user, long chatId, int messageId, CancellationToken ct)
    {
        var list = await _userService.GetCurrentListAsync(user.Id);
        if (list == null || !await _listService.IsAdminAsync(list.Id, user.Id))
        {
            await _botClient.AnswerCallbackQuery(query.Id, "Only admins can delete", cancellationToken: ct);
            return;
        }

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("⚠️ Yes, delete", "list:confirmdelete"),
                InlineKeyboardButton.WithCallbackData("❌ Cancel", "manage:list")
            }
        });

        await _botClient.AnswerCallbackQuery(query.Id, cancellationToken: ct);
        await _botClient.EditMessageText(chatId, messageId,
            $"⚠️ Are you sure you want to delete \"{list.Name}\"?\n\nThis cannot be undone!",
            replyMarkup: keyboard, cancellationToken: ct);
    }

    private async Task HandleListConfirmDeleteAsync(CallbackQuery query, BotUser user, long chatId, int messageId, CancellationToken ct)
    {
        var list = await _userService.GetCurrentListAsync(user.Id);
        if (list == null)
        {
            await _botClient.AnswerCallbackQuery(query.Id, "No list selected", cancellationToken: ct);
            return;
        }

        var listName = list.Name;
        await _listService.DeleteListAsync(list.Id, user.Id);
        await _userService.SetCurrentListAsync(user.Id, null);

        await _botClient.AnswerCallbackQuery(query.Id, "List deleted!", cancellationToken: ct);
        await _botClient.EditMessageText(chatId, messageId, $"✅ List \"{listName}\" has been deleted.", cancellationToken: ct);
    }

    private async Task HandleListLeaveAsync(CallbackQuery query, BotUser user, long chatId, int messageId, CancellationToken ct)
    {
        var list = await _userService.GetCurrentListAsync(user.Id);
        if (list == null)
        {
            await _botClient.AnswerCallbackQuery(query.Id, "No list selected", cancellationToken: ct);
            return;
        }

        if (list.CreatorId == user.Id)
        {
            await _botClient.AnswerCallbackQuery(query.Id, "Owners cannot leave", cancellationToken: ct);
            return;
        }

        var success = await _listService.LeaveListAsync(list.Id, user.Id);
        if (success)
        {
            await _activityLogService.LogActivityAsync(list.Id, user.Id, ActionType.UserLeft);
            await _notificationService.NotifyListUsersAsync(list.Id, user.Id, ActionType.UserLeft, user.DisplayName);
            await _userService.SetCurrentListAsync(user.Id, null);

            await _botClient.AnswerCallbackQuery(query.Id, "Left the list", cancellationToken: ct);
            await _botClient.EditMessageText(chatId, messageId, $"✅ You have left \"{list.Name}\".", cancellationToken: ct);
        }
        else
        {
            await _botClient.AnswerCallbackQuery(query.Id, "Could not leave", cancellationToken: ct);
        }
    }

    private async Task HandleListNewAsync(CallbackQuery query, BotUser user, long chatId, CancellationToken ct)
    {
        await _userService.SetConversationStateAsync(user.Id, ConversationState.WaitingForListName, "new_back_to_lists");
        await _botClient.AnswerCallbackQuery(query.Id, cancellationToken: ct);
        await _botClient.SendMessage(chatId, "📝 Enter the name for your new list:", cancellationToken: ct);
    }

    private async Task HandleListLogAsync(CallbackQuery query, BotUser user, long chatId, int messageId, CancellationToken ct)
    {
        var list = await _userService.GetCurrentListAsync(user.Id);
        if (list == null)
        {
            await _botClient.AnswerCallbackQuery(query.Id, "No list selected", cancellationToken: ct);
            return;
        }

        var logs = await _activityLogService.GetListActivityAsync(list.Id, 25);
        var logText = MessageFormatter.FormatActivityLog(logs);

        await _botClient.AnswerCallbackQuery(query.Id, cancellationToken: ct);
        await _botClient.SendMessage(chatId, logText, cancellationToken: ct);

        // Show manage list menu again
        await ShowManageListScreenAsync(null, user, chatId, null, ct);
    }

    #endregion

    #region Items Screen

    private async Task HandleItemsScreenAsync(CallbackQuery query, BotUser user, long chatId, int messageId, string[] parts, CancellationToken ct)
    {
        var page = parts.Length > 1 && int.TryParse(parts[1], out var p) ? p : 0;
        await ShowItemsScreenAsync(query, user, chatId, messageId, page, ct);
    }

    private async Task ShowItemsScreenAsync(CallbackQuery? query, BotUser user, long chatId, int? messageId, int page, CancellationToken ct)
    {
        var list = await _userService.GetCurrentListAsync(user.Id);
        if (list == null)
        {
            if (query != null)
                await _botClient.AnswerCallbackQuery(query.Id, "No list selected", cancellationToken: ct);
            return;
        }

        var allItems = (await _itemService.GetListItemsAsync(list.Id, includeHidden: false)).ToList();
        var totalPages = Math.Max(1, (int)Math.Ceiling(allItems.Count / (double)ItemsPerPage));
        page = Math.Max(0, Math.Min(page, totalPages - 1));

        var itemsOnPage = allItems.Skip(page * ItemsPerPage).Take(ItemsPerPage).ToList();

        var buttons = new List<List<InlineKeyboardButton>>();

        // Add item buttons in 2 columns - clicking toggles check state
        for (int i = 0; i < itemsOnPage.Count; i += 2)
        {
            var row = new List<InlineKeyboardButton>();
            var item1 = itemsOnPage[i];
            var icon1 = item1.IsChecked ? "✅" : "⬜";
            row.Add(InlineKeyboardButton.WithCallbackData(
                $"{icon1} {Truncate(item1.ItemName, 12)}",
                $"item:toggle:{item1.Id}"));

            if (i + 1 < itemsOnPage.Count)
            {
                var item2 = itemsOnPage[i + 1];
                var icon2 = item2.IsChecked ? "✅" : "⬜";
                row.Add(InlineKeyboardButton.WithCallbackData(
                    $"{icon2} {Truncate(item2.ItemName, 12)}",
                    $"item:toggle:{item2.Id}"));
            }
            buttons.Add(row);
        }

        // Navigation row
        if (totalPages > 1)
        {
            var navRow = new List<InlineKeyboardButton>();
            if (page > 0)
                navRow.Add(InlineKeyboardButton.WithCallbackData("⬅️ Previous", $"items:{page - 1}"));
            else
                navRow.Add(InlineKeyboardButton.WithCallbackData(" ", "noop"));

            if (page < totalPages - 1)
                navRow.Add(InlineKeyboardButton.WithCallbackData("Next ➡️", $"items:{page + 1}"));
            else
                navRow.Add(InlineKeyboardButton.WithCallbackData(" ", "noop"));

            buttons.Add(navRow);
        }

        // Action buttons
        buttons.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData("🛒 Manage items", "manage:items"),
            InlineKeyboardButton.WithCallbackData("⚙️ Manage list", "manage:list")
        });

        var keyboard = new InlineKeyboardMarkup(buttons);
        var text = allItems.Count == 0
            ? $"📝 {list.Name}\n\nNo items yet. Tap \"Manage items\" to add some!"
            : $"📝 {list.Name} ({page + 1}/{totalPages})";

        if (query != null)
            await _botClient.AnswerCallbackQuery(query.Id, cancellationToken: ct);

        if (messageId.HasValue)
        {
            await _botClient.EditMessageText(chatId, messageId.Value, text, replyMarkup: keyboard, cancellationToken: ct);
        }
        else
        {
            await _botClient.SendMessage(chatId, text, replyMarkup: keyboard, cancellationToken: ct);
        }
    }

    #endregion

    #region Item Actions

    private async Task HandleItemActionAsync(CallbackQuery query, BotUser user, long chatId, int messageId, string[] parts, CancellationToken ct)
    {
        if (parts.Length < 2) return;

        var action = parts[1];
        switch (action)
        {
            case "toggle":
                if (parts.Length > 2 && int.TryParse(parts[2], out var itemId))
                    await HandleItemToggleAsync(query, user, chatId, messageId, itemId, ct);
                break;
            case "add":
                await HandleItemAddAsync(query, user, chatId, ct);
                break;
            case "togglevis":
                if (parts.Length > 2 && int.TryParse(parts[2], out var visItemId))
                    await HandleItemToggleVisibilityAsync(query, user, chatId, messageId, visItemId, ct);
                break;
        }
    }

    private async Task HandleItemToggleAsync(CallbackQuery query, BotUser user, long chatId, int messageId, int itemId, CancellationToken ct)
    {
        var item = await _itemService.GetItemByIdAsync(itemId);
        if (item == null)
        {
            await _botClient.AnswerCallbackQuery(query.Id, "Item not found", cancellationToken: ct);
            return;
        }

        if (!await _listService.HasAccessAsync(item.ListId, user.Id))
        {
            await _botClient.AnswerCallbackQuery(query.Id, "Access denied", cancellationToken: ct);
            return;
        }

        if (item.IsChecked)
        {
            await _itemService.UncheckItemAsync(itemId);
            await _activityLogService.LogActivityAsync(item.ListId, user.Id, ActionType.ItemUnchecked, item.ItemName);
            await _notificationService.NotifyListUsersAsync(item.ListId, user.Id, ActionType.ItemUnchecked, user.DisplayName, item.ItemName);
        }
        else
        {
            await _itemService.CheckItemAsync(itemId);
            await _activityLogService.LogActivityAsync(item.ListId, user.Id, ActionType.ItemChecked, item.ItemName);
            await _notificationService.NotifyListUsersAsync(item.ListId, user.Id, ActionType.ItemChecked, user.DisplayName, item.ItemName);
        }

        // Refresh the items screen (stay on current page)
        await ShowItemsScreenAsync(query, user, chatId, messageId, 0, ct);
    }

    private async Task HandleItemAddAsync(CallbackQuery query, BotUser user, long chatId, CancellationToken ct)
    {
        var list = await _userService.GetCurrentListAsync(user.Id);
        if (list == null)
        {
            await _botClient.AnswerCallbackQuery(query.Id, "No list selected", cancellationToken: ct);
            return;
        }

        await _userService.SetConversationStateAsync(user.Id, ConversationState.WaitingForItemName, "add_back_to_manage");
        await _botClient.AnswerCallbackQuery(query.Id, cancellationToken: ct);
        await _botClient.SendMessage(chatId, $"🛒 Enter the item name to add to \"{list.Name}\":", cancellationToken: ct);
    }

    private async Task HandleItemToggleVisibilityAsync(CallbackQuery query, BotUser user, long chatId, int messageId, int itemId, CancellationToken ct)
    {
        var item = await _itemService.GetItemByIdAsync(itemId);
        if (item == null)
        {
            await _botClient.AnswerCallbackQuery(query.Id, "Item not found", cancellationToken: ct);
            return;
        }

        if (!await _listService.HasAccessAsync(item.ListId, user.Id))
        {
            await _botClient.AnswerCallbackQuery(query.Id, "Access denied", cancellationToken: ct);
            return;
        }

        if (item.IsHidden)
        {
            await _itemService.ShowItemAsync(itemId);
            await _activityLogService.LogActivityAsync(item.ListId, user.Id, ActionType.ItemShown, item.ItemName);
        }
        else
        {
            await _itemService.HideItemAsync(itemId);
            await _activityLogService.LogActivityAsync(item.ListId, user.Id, ActionType.ItemHidden, item.ItemName);
        }

        // Refresh the visibility screen
        await ShowVisibilityScreenAsync(query, user, chatId, messageId, 0, ct);
    }

    #endregion

    #region Manage Screens

    private async Task HandleManageScreenAsync(CallbackQuery query, BotUser user, long chatId, int messageId, string[] parts, CancellationToken ct)
    {
        if (parts.Length < 2) return;

        var screen = parts[1];
        switch (screen)
        {
            case "items":
                await ShowManageItemsScreenAsync(query, user, chatId, messageId, ct);
                break;
            case "list":
                await ShowManageListScreenAsync(query, user, chatId, messageId, ct);
                break;
        }
    }

    private async Task ShowManageItemsScreenAsync(CallbackQuery? query, BotUser user, long chatId, int? messageId, CancellationToken ct)
    {
        var list = await _userService.GetCurrentListAsync(user.Id);
        if (list == null)
        {
            if (query != null)
                await _botClient.AnswerCallbackQuery(query.Id, "No list selected", cancellationToken: ct);
            return;
        }

        var buttons = new List<List<InlineKeyboardButton>>
        {
            new()
            {
                InlineKeyboardButton.WithCallbackData("➕ Add", "item:add"),
                InlineKeyboardButton.WithCallbackData("👁️ Visibility", "visibility:0")
            },
            new()
            {
                InlineKeyboardButton.WithCallbackData("⬅️ Back", "items:0"),
                InlineKeyboardButton.WithCallbackData("📋 List all", "visibility:0")
            }
        };

        var keyboard = new InlineKeyboardMarkup(buttons);
        var text = $"🛒 Manage Items - {list.Name}";

        if (query != null)
            await _botClient.AnswerCallbackQuery(query.Id, cancellationToken: ct);

        if (messageId.HasValue)
        {
            await _botClient.EditMessageText(chatId, messageId.Value, text, replyMarkup: keyboard, cancellationToken: ct);
        }
        else
        {
            await _botClient.SendMessage(chatId, text, replyMarkup: keyboard, cancellationToken: ct);
        }
    }

    private async Task ShowManageListScreenAsync(CallbackQuery? query, BotUser user, long chatId, int? messageId, CancellationToken ct)
    {
        var list = await _userService.GetCurrentListAsync(user.Id);
        if (list == null)
        {
            if (query != null)
                await _botClient.AnswerCallbackQuery(query.Id, "No list selected", cancellationToken: ct);
            return;
        }

        var isAdmin = await _listService.IsAdminAsync(list.Id, user.Id);
        var isOwner = list.CreatorId == user.Id;

        var buttons = new List<List<InlineKeyboardButton>>();

        // Row 1: Share, Rename (admin only)
        if (isAdmin)
        {
            buttons.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData("🔗 Share", "list:share"),
                InlineKeyboardButton.WithCallbackData("✏️ Rename", "list:rename")
            });
        }

        // Row 2: Delete (admin), Leave (non-owner)
        var row2 = new List<InlineKeyboardButton>();
        if (isAdmin)
            row2.Add(InlineKeyboardButton.WithCallbackData("🗑️ Delete", "list:delete"));
        if (!isOwner)
            row2.Add(InlineKeyboardButton.WithCallbackData("🚪 Leave", "list:leave"));
        if (row2.Count > 0)
            buttons.Add(row2);

        // Row 3: New, Log
        buttons.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData("➕ New", "list:new"),
            InlineKeyboardButton.WithCallbackData("📜 Log", "list:log")
        });

        // Row 4: Back, Users
        buttons.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData("⬅️ Back", "items:0"),
            InlineKeyboardButton.WithCallbackData("👥 Users", "users:0")
        });

        var keyboard = new InlineKeyboardMarkup(buttons);
        var roleText = isOwner ? "Owner" : (isAdmin ? "Admin" : "Member");
        var text = $"⚙️ Manage List - {list.Name}\nYour role: {roleText}";

        if (query != null)
            await _botClient.AnswerCallbackQuery(query.Id, cancellationToken: ct);

        if (messageId.HasValue)
        {
            await _botClient.EditMessageText(chatId, messageId.Value, text, replyMarkup: keyboard, cancellationToken: ct);
        }
        else
        {
            await _botClient.SendMessage(chatId, text, replyMarkup: keyboard, cancellationToken: ct);
        }
    }

    #endregion

    #region Visibility Screen

    private async Task HandleVisibilityScreenAsync(CallbackQuery query, BotUser user, long chatId, int messageId, string[] parts, CancellationToken ct)
    {
        var page = parts.Length > 1 && int.TryParse(parts[1], out var p) ? p : 0;
        await ShowVisibilityScreenAsync(query, user, chatId, messageId, page, ct);
    }

    private async Task ShowVisibilityScreenAsync(CallbackQuery? query, BotUser user, long chatId, int? messageId, int page, CancellationToken ct)
    {
        var list = await _userService.GetCurrentListAsync(user.Id);
        if (list == null)
        {
            if (query != null)
                await _botClient.AnswerCallbackQuery(query.Id, "No list selected", cancellationToken: ct);
            return;
        }

        var allItems = (await _itemService.GetListItemsAsync(list.Id, includeHidden: true)).ToList();
        var totalPages = Math.Max(1, (int)Math.Ceiling(allItems.Count / (double)ItemsPerPage));
        page = Math.Max(0, Math.Min(page, totalPages - 1));

        var itemsOnPage = allItems.Skip(page * ItemsPerPage).Take(ItemsPerPage).ToList();

        var buttons = new List<List<InlineKeyboardButton>>();

        // Item buttons - show visibility status, clicking toggles
        for (int i = 0; i < itemsOnPage.Count; i += 2)
        {
            var row = new List<InlineKeyboardButton>();
            var item1 = itemsOnPage[i];
            var visIcon1 = item1.IsHidden ? "🙈" : "👁️";
            var checkIcon1 = item1.IsChecked ? "✅" : "⬜";
            row.Add(InlineKeyboardButton.WithCallbackData(
                $"{visIcon1}{checkIcon1} {Truncate(item1.ItemName, 10)}",
                $"item:togglevis:{item1.Id}"));

            if (i + 1 < itemsOnPage.Count)
            {
                var item2 = itemsOnPage[i + 1];
                var visIcon2 = item2.IsHidden ? "🙈" : "👁️";
                var checkIcon2 = item2.IsChecked ? "✅" : "⬜";
                row.Add(InlineKeyboardButton.WithCallbackData(
                    $"{visIcon2}{checkIcon2} {Truncate(item2.ItemName, 10)}",
                    $"item:togglevis:{item2.Id}"));
            }
            buttons.Add(row);
        }

        // Navigation row
        if (totalPages > 1)
        {
            var navRow = new List<InlineKeyboardButton>();
            if (page > 0)
                navRow.Add(InlineKeyboardButton.WithCallbackData("⬅️ Previous", $"visibility:{page - 1}"));
            else
                navRow.Add(InlineKeyboardButton.WithCallbackData(" ", "noop"));

            if (page < totalPages - 1)
                navRow.Add(InlineKeyboardButton.WithCallbackData("Next ➡️", $"visibility:{page + 1}"));
            else
                navRow.Add(InlineKeyboardButton.WithCallbackData(" ", "noop"));

            buttons.Add(navRow);
        }

        // Bottom navigation
        buttons.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData("⬅️ Back", "manage:items"),
            InlineKeyboardButton.WithCallbackData("📋 Back to list", "items:0")
        });

        var keyboard = new InlineKeyboardMarkup(buttons);
        var text = allItems.Count == 0
            ? $"👁️ {list.Name} - All Items\n\nNo items yet."
            : $"👁️ {list.Name} - All Items ({page + 1}/{totalPages})\n\nTap to toggle visibility (🙈 hidden / 👁️ visible)";

        if (query != null)
            await _botClient.AnswerCallbackQuery(query.Id, cancellationToken: ct);

        if (messageId.HasValue)
        {
            await _botClient.EditMessageText(chatId, messageId.Value, text, replyMarkup: keyboard, cancellationToken: ct);
        }
        else
        {
            await _botClient.SendMessage(chatId, text, replyMarkup: keyboard, cancellationToken: ct);
        }
    }

    #endregion

    #region Users Screen

    private async Task HandleUsersScreenAsync(CallbackQuery query, BotUser user, long chatId, int messageId, string[] parts, CancellationToken ct)
    {
        var page = parts.Length > 1 && int.TryParse(parts[1], out var p) ? p : 0;
        await ShowUsersScreenAsync(query, user, chatId, messageId, page, ct);
    }

    private async Task ShowUsersScreenAsync(CallbackQuery? query, BotUser user, long chatId, int? messageId, int page, CancellationToken ct)
    {
        var list = await _userService.GetCurrentListAsync(user.Id);
        if (list == null)
        {
            if (query != null)
                await _botClient.AnswerCallbackQuery(query.Id, "No list selected", cancellationToken: ct);
            return;
        }

        var allMembers = (await _listService.GetListMembersAsync(list.Id)).ToList();
        var totalPages = Math.Max(1, (int)Math.Ceiling(allMembers.Count / (double)UsersPerPage));
        page = Math.Max(0, Math.Min(page, totalPages - 1));

        var membersOnPage = allMembers.Skip(page * UsersPerPage).Take(UsersPerPage).ToList();
        var isAdmin = await _listService.IsAdminAsync(list.Id, user.Id);

        var buttons = new List<List<InlineKeyboardButton>>();

        // User buttons in 2 columns
        for (int i = 0; i < membersOnPage.Count; i += 2)
        {
            var row = new List<InlineKeyboardButton>();
            var member1 = membersOnPage[i];
            var icon1 = member1.Role == ListRole.Admin ? "👑" : "👤";
            var name1 = member1.User.Username != null ? $"@{member1.User.Username}" : member1.User.DisplayName;

            // Only allow clicking on other users if current user is admin
            if (isAdmin && member1.User.Id != user.Id)
            {
                row.Add(InlineKeyboardButton.WithCallbackData(
                    $"{icon1} {Truncate(name1, 12)}",
                    $"user:select:{member1.User.Id}"));
            }
            else
            {
                row.Add(InlineKeyboardButton.WithCallbackData(
                    $"{icon1} {Truncate(name1, 12)}{(member1.User.Id == user.Id ? " (you)" : "")}",
                    "noop"));
            }

            if (i + 1 < membersOnPage.Count)
            {
                var member2 = membersOnPage[i + 1];
                var icon2 = member2.Role == ListRole.Admin ? "👑" : "👤";
                var name2 = member2.User.Username != null ? $"@{member2.User.Username}" : member2.User.DisplayName;

                if (isAdmin && member2.User.Id != user.Id)
                {
                    row.Add(InlineKeyboardButton.WithCallbackData(
                        $"{icon2} {Truncate(name2, 12)}",
                        $"user:select:{member2.User.Id}"));
                }
                else
                {
                    row.Add(InlineKeyboardButton.WithCallbackData(
                        $"{icon2} {Truncate(name2, 12)}{(member2.User.Id == user.Id ? " (you)" : "")}",
                        "noop"));
                }
            }
            buttons.Add(row);
        }

        // Navigation
        if (totalPages > 1)
        {
            var navRow = new List<InlineKeyboardButton>();
            if (page > 0)
                navRow.Add(InlineKeyboardButton.WithCallbackData("⬅️ Previous", $"users:{page - 1}"));
            else
                navRow.Add(InlineKeyboardButton.WithCallbackData(" ", "noop"));

            if (page < totalPages - 1)
                navRow.Add(InlineKeyboardButton.WithCallbackData("Next ➡️", $"users:{page + 1}"));
            else
                navRow.Add(InlineKeyboardButton.WithCallbackData(" ", "noop"));

            buttons.Add(navRow);
        }

        // Bottom nav
        buttons.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData("⬅️ Back", "manage:list"),
            InlineKeyboardButton.WithCallbackData("📋 Back to list", "items:0")
        });

        var keyboard = new InlineKeyboardMarkup(buttons);
        var text = $"👥 {list.Name} - Members ({page + 1}/{totalPages})\n\n👑 = Admin, 👤 = Member" +
                   (isAdmin ? "\n\nTap a user to manage them." : "");

        if (query != null)
            await _botClient.AnswerCallbackQuery(query.Id, cancellationToken: ct);

        if (messageId.HasValue)
        {
            await _botClient.EditMessageText(chatId, messageId.Value, text, replyMarkup: keyboard, cancellationToken: ct);
        }
        else
        {
            await _botClient.SendMessage(chatId, text, replyMarkup: keyboard, cancellationToken: ct);
        }
    }

    #endregion

    #region User Actions

    private async Task HandleUserActionAsync(CallbackQuery query, BotUser user, long chatId, int messageId, string[] parts, CancellationToken ct)
    {
        if (parts.Length < 2) return;

        var action = parts[1];
        var targetUserId = parts.Length > 2 && int.TryParse(parts[2], out var id) ? id : 0;

        switch (action)
        {
            case "select":
                await ShowUserActionsScreenAsync(query, user, chatId, messageId, targetUserId, ct);
                break;
            case "promote":
                await HandleUserPromoteAsync(query, user, chatId, messageId, targetUserId, ct);
                break;
            case "demote":
                await HandleUserDemoteAsync(query, user, chatId, messageId, targetUserId, ct);
                break;
            case "remove":
                await HandleUserRemoveAsync(query, user, chatId, messageId, targetUserId, ct);
                break;
        }
    }

    private async Task ShowUserActionsScreenAsync(CallbackQuery query, BotUser user, long chatId, int messageId, int targetUserId, CancellationToken ct)
    {
        var list = await _userService.GetCurrentListAsync(user.Id);
        if (list == null)
        {
            await _botClient.AnswerCallbackQuery(query.Id, "No list selected", cancellationToken: ct);
            return;
        }

        if (!await _listService.IsAdminAsync(list.Id, user.Id))
        {
            await _botClient.AnswerCallbackQuery(query.Id, "Only admins can manage users", cancellationToken: ct);
            return;
        }

        var targetUser = await _userService.GetUserByIdAsync(targetUserId);
        if (targetUser == null)
        {
            await _botClient.AnswerCallbackQuery(query.Id, "User not found", cancellationToken: ct);
            return;
        }

        var members = (await _listService.GetListMembersAsync(list.Id)).ToList();
        var targetMember = members.FirstOrDefault(m => m.User.Id == targetUserId);
        if (targetMember.User == null)
        {
            await _botClient.AnswerCallbackQuery(query.Id, "User not in list", cancellationToken: ct);
            return;
        }

        var isTargetOwner = list.CreatorId == targetUserId;
        var isTargetAdmin = targetMember.Role == ListRole.Admin;

        var buttons = new List<List<InlineKeyboardButton>>();

        // Row 1: Promote/Demote
        var row1 = new List<InlineKeyboardButton>();
        if (!isTargetAdmin && !isTargetOwner)
            row1.Add(InlineKeyboardButton.WithCallbackData("⬆️ Promote", $"user:promote:{targetUserId}"));
        if (isTargetAdmin && !isTargetOwner)
            row1.Add(InlineKeyboardButton.WithCallbackData("⬇️ Demote", $"user:demote:{targetUserId}"));
        if (row1.Count > 0)
            buttons.Add(row1);

        // Row 2: Remove (not for owner)
        if (!isTargetOwner)
        {
            buttons.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData("🚫 Remove", $"user:remove:{targetUserId}")
            });
        }

        // Row 3: Navigation
        buttons.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData("⬅️ Back", "users:0"),
            InlineKeyboardButton.WithCallbackData("📋 Back to list", "items:0")
        });

        var keyboard = new InlineKeyboardMarkup(buttons);
        var name = targetUser.Username != null ? $"@{targetUser.Username}" : targetUser.DisplayName;
        var roleIcon = isTargetOwner ? "👑 Owner" : (isTargetAdmin ? "👑 Admin" : "👤 Member");
        var text = $"👤 Manage User\n\n{name}\nRole: {roleIcon}";

        await _botClient.AnswerCallbackQuery(query.Id, cancellationToken: ct);
        await _botClient.EditMessageText(chatId, messageId, text, replyMarkup: keyboard, cancellationToken: ct);
    }

    private async Task HandleUserPromoteAsync(CallbackQuery query, BotUser user, long chatId, int messageId, int targetUserId, CancellationToken ct)
    {
        var list = await _userService.GetCurrentListAsync(user.Id);
        if (list == null || !await _listService.IsAdminAsync(list.Id, user.Id))
        {
            await _botClient.AnswerCallbackQuery(query.Id, "Permission denied", cancellationToken: ct);
            return;
        }

        var targetUser = await _userService.GetUserByIdAsync(targetUserId);
        if (targetUser == null)
        {
            await _botClient.AnswerCallbackQuery(query.Id, "User not found", cancellationToken: ct);
            return;
        }

        var success = await _listService.PromoteUserAsync(list.Id, user.Id, targetUserId);
        if (success)
        {
            await _activityLogService.LogActivityAsync(list.Id, user.Id, ActionType.UserPromoted, targetUser.DisplayName);
            await _botClient.AnswerCallbackQuery(query.Id, $"Promoted {targetUser.DisplayName}!", cancellationToken: ct);
        }
        else
        {
            await _botClient.AnswerCallbackQuery(query.Id, "Could not promote", cancellationToken: ct);
        }

        await ShowUsersScreenAsync(null, user, chatId, messageId, 0, ct);
    }

    private async Task HandleUserDemoteAsync(CallbackQuery query, BotUser user, long chatId, int messageId, int targetUserId, CancellationToken ct)
    {
        var list = await _userService.GetCurrentListAsync(user.Id);
        if (list == null || !await _listService.IsAdminAsync(list.Id, user.Id))
        {
            await _botClient.AnswerCallbackQuery(query.Id, "Permission denied", cancellationToken: ct);
            return;
        }

        var targetUser = await _userService.GetUserByIdAsync(targetUserId);
        if (targetUser == null)
        {
            await _botClient.AnswerCallbackQuery(query.Id, "User not found", cancellationToken: ct);
            return;
        }

        var success = await _listService.DemoteUserAsync(list.Id, user.Id, targetUserId);
        if (success)
        {
            await _activityLogService.LogActivityAsync(list.Id, user.Id, ActionType.UserDemoted, targetUser.DisplayName);
            await _botClient.AnswerCallbackQuery(query.Id, $"Demoted {targetUser.DisplayName}!", cancellationToken: ct);
        }
        else
        {
            await _botClient.AnswerCallbackQuery(query.Id, "Could not demote", cancellationToken: ct);
        }

        await ShowUsersScreenAsync(null, user, chatId, messageId, 0, ct);
    }

    private async Task HandleUserRemoveAsync(CallbackQuery query, BotUser user, long chatId, int messageId, int targetUserId, CancellationToken ct)
    {
        var list = await _userService.GetCurrentListAsync(user.Id);
        if (list == null || !await _listService.IsAdminAsync(list.Id, user.Id))
        {
            await _botClient.AnswerCallbackQuery(query.Id, "Permission denied", cancellationToken: ct);
            return;
        }

        var targetUser = await _userService.GetUserByIdAsync(targetUserId);
        if (targetUser == null)
        {
            await _botClient.AnswerCallbackQuery(query.Id, "User not found", cancellationToken: ct);
            return;
        }

        var success = await _listService.RemoveUserAsync(list.Id, user.Id, targetUserId);
        if (success)
        {
            await _activityLogService.LogActivityAsync(list.Id, user.Id, ActionType.UserRemoved, targetUser.DisplayName);
            await _botClient.AnswerCallbackQuery(query.Id, $"Removed {targetUser.DisplayName}!", cancellationToken: ct);
        }
        else
        {
            await _botClient.AnswerCallbackQuery(query.Id, "Could not remove", cancellationToken: ct);
        }

        await ShowUsersScreenAsync(null, user, chatId, messageId, 0, ct);
    }

    #endregion

    #region Helpers

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return text.Length <= maxLength ? text : text[..(maxLength - 1)] + "…";
    }

    #endregion

    #region Public Methods for CommandHandler

    public async Task ShowListsAsync(long chatId, BotUser user, CancellationToken ct)
    {
        await ShowListsScreenAsync(null, user, chatId, null, 0, ct);
    }

    public async Task ShowItemsAsync(long chatId, BotUser user, CancellationToken ct)
    {
        await ShowItemsScreenAsync(null, user, chatId, null, 0, ct);
    }

    public async Task ShowManageItemsAsync(long chatId, BotUser user, CancellationToken ct)
    {
        await ShowManageItemsScreenAsync(null, user, chatId, null, ct);
    }

    public async Task ShowManageListAsync(long chatId, BotUser user, CancellationToken ct)
    {
        await ShowManageListScreenAsync(null, user, chatId, null, ct);
    }

    #endregion
}
