using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using ShoppyBot.Handlers;
using ShoppyBot.Models;
using ShoppyBot.Services;

namespace ShoppyBot.Tests.Handlers;

public class CommandHandlerTests
{
    private readonly Mock<ITelegramBotClient> _mockBotClient;
    private readonly Mock<IUserService> _mockUserService;
    private readonly Mock<IListService> _mockListService;
    private readonly Mock<IItemService> _mockItemService;
    private readonly Mock<IActivityLogService> _mockActivityLogService;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly Mock<ILogger<CommandHandler>> _mockLogger;
    private readonly CommandHandler _commandHandler;
    private readonly Models.User _testUser;

    public CommandHandlerTests()
    {
        _mockBotClient = new Mock<ITelegramBotClient>();
        _mockUserService = new Mock<IUserService>();
        _mockListService = new Mock<IListService>();
        _mockItemService = new Mock<IItemService>();
        _mockActivityLogService = new Mock<IActivityLogService>();
        _mockNotificationService = new Mock<INotificationService>();
        _mockLogger = new Mock<ILogger<CommandHandler>>();

        // Create a real CallbackHandler with mocked dependencies
        var mockCallbackLogger = new Mock<ILogger<CallbackHandler>>();
        var callbackHandler = new CallbackHandler(
            _mockBotClient.Object,
            _mockUserService.Object,
            _mockListService.Object,
            _mockItemService.Object,
            _mockActivityLogService.Object,
            _mockNotificationService.Object,
            mockCallbackLogger.Object);

        _commandHandler = new CommandHandler(
            _mockBotClient.Object,
            _mockUserService.Object,
            _mockListService.Object,
            _mockItemService.Object,
            _mockActivityLogService.Object,
            _mockNotificationService.Object,
            callbackHandler,
            _mockLogger.Object);

        _testUser = new Models.User
        {
            Id = 1,
            TelegramId = 12345L,
            Username = "testuser",
            DisplayName = "Test User"
        };

        _mockUserService.Setup(x => x.GetOrCreateUserAsync(It.IsAny<long>(), It.IsAny<string?>(), It.IsAny<string>()))
            .ReturnsAsync(_testUser);
    }

    private Message CreateMessage(string text, long chatId = 12345L)
    {
        return new Message
        {
            Text = text,
            Chat = new Chat { Id = chatId },
            From = new Telegram.Bot.Types.User
            {
                Id = 12345L,
                FirstName = "Test",
                LastName = "User",
                Username = "testuser"
            }
        };
    }

    [Fact]
    public async Task HandleAsync_StartCommand_SendsWelcomeMessage()
    {
        var message = CreateMessage("/start");

        await _commandHandler.HandleAsync(message, CancellationToken.None);

        _mockBotClient.Verify(x => x.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text.Contains("Welcome")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_HelpCommand_SendsCommandList()
    {
        var message = CreateMessage("/help");

        await _commandHandler.HandleAsync(message, CancellationToken.None);

        _mockBotClient.Verify(x => x.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text.Contains("Available Commands")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_NewListCommand_WithName_CreatesNewList()
    {
        var message = CreateMessage("/newlist Groceries");
        var createdList = new ShoppingList
        {
            Id = 1,
            Name = "Groceries",
            CreatorId = _testUser.Id,
            ShareToken = "test-token"
        };

        _mockListService.Setup(x => x.CreateListAsync(_testUser.Id, "Groceries"))
            .ReturnsAsync(createdList);
        _mockListService.Setup(x => x.GetUserListsAsync(_testUser.Id))
            .ReturnsAsync(new List<(ShoppingList, ListRole)>());

        await _commandHandler.HandleAsync(message, CancellationToken.None);

        _mockListService.Verify(x => x.CreateListAsync(_testUser.Id, "Groceries"), Times.Once);
        _mockUserService.Verify(x => x.SetCurrentListAsync(_testUser.Id, createdList.Id), Times.Once);
        _mockActivityLogService.Verify(x => x.LogActivityAsync(
            createdList.Id, _testUser.Id, ActionType.ListCreated, "Groceries"), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_NewListCommand_WithoutName_PromptsForName()
    {
        var message = CreateMessage("/newlist");

        await _commandHandler.HandleAsync(message, CancellationToken.None);

        _mockUserService.Verify(x => x.SetConversationStateAsync(
            _testUser.Id, ConversationState.WaitingForListName, It.IsAny<string?>()),
            Times.Once);
        _mockBotClient.Verify(x => x.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text.Contains("name")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AddCommand_WithoutCurrentList_ReturnsError()
    {
        var message = CreateMessage("/add Milk");

        _mockUserService.Setup(x => x.GetCurrentListAsync(_testUser.Id))
            .ReturnsAsync((ShoppingList?)null);

        await _commandHandler.HandleAsync(message, CancellationToken.None);

        _mockBotClient.Verify(x => x.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text.Contains("No list selected")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AddCommand_WithCurrentList_AddsItem()
    {
        var message = CreateMessage("/add Milk");
        var currentList = new ShoppingList
        {
            Id = 1,
            Name = "Groceries",
            CreatorId = _testUser.Id,
            ShareToken = "test-token"
        };
        var newItem = new ListItem
        {
            Id = 1,
            ListId = currentList.Id,
            ItemName = "Milk",
            AddedById = _testUser.Id
        };

        _mockUserService.Setup(x => x.GetCurrentListAsync(_testUser.Id))
            .ReturnsAsync(currentList);
        _mockListService.Setup(x => x.HasAccessAsync(currentList.Id, _testUser.Id))
            .ReturnsAsync(true);
        _mockItemService.Setup(x => x.AddItemAsync(currentList.Id, _testUser.Id, "Milk"))
            .ReturnsAsync(newItem);
        _mockItemService.Setup(x => x.GetListItemsAsync(currentList.Id, false))
            .ReturnsAsync(new List<ListItem> { newItem });

        await _commandHandler.HandleAsync(message, CancellationToken.None);

        _mockItemService.Verify(x => x.AddItemAsync(currentList.Id, _testUser.Id, "Milk"), Times.Once);
        _mockActivityLogService.Verify(x => x.LogActivityAsync(
            currentList.Id, _testUser.Id, ActionType.ItemAdded, "Milk"), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_StartWithJoinToken_JoinsUser()
    {
        var message = CreateMessage("/start join_abc123");
        var list = new ShoppingList
        {
            Id = 1,
            Name = "Groceries",
            CreatorId = 999,
            ShareToken = "abc123"
        };

        _mockListService.Setup(x => x.JoinListAsync(_testUser.Id, "abc123"))
            .ReturnsAsync(true);
        _mockListService.Setup(x => x.GetListByShareTokenAsync("abc123"))
            .ReturnsAsync(list);
        _mockItemService.Setup(x => x.GetListItemsAsync(list.Id, false))
            .ReturnsAsync(new List<ListItem>());

        await _commandHandler.HandleAsync(message, CancellationToken.None);

        _mockListService.Verify(x => x.JoinListAsync(_testUser.Id, "abc123"), Times.Once);
        _mockUserService.Verify(x => x.SetCurrentListAsync(_testUser.Id, list.Id), Times.Once);
        _mockActivityLogService.Verify(x => x.LogActivityAsync(
            list.Id, _testUser.Id, ActionType.UserJoined, null), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CancelCommand_WithNoState_ReturnsNothingToCancel()
    {
        var message = CreateMessage("/cancel");
        _testUser.ConversationState = ConversationState.None;

        await _commandHandler.HandleAsync(message, CancellationToken.None);

        _mockBotClient.Verify(x => x.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text.Contains("Nothing to cancel")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
