using Microsoft.EntityFrameworkCore;
using Xunit;
using ShoppyBot.Data;
using ShoppyBot.Models;
using ShoppyBot.Services;

namespace ShoppyBot.Tests.Services;

public class UserServiceTests : IDisposable
{
    private readonly ShoppyBotContext _context;
    private readonly UserService _userService;

    public UserServiceTests()
    {
        var options = new DbContextOptionsBuilder<ShoppyBotContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ShoppyBotContext(options);
        _userService = new UserService(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task GetOrCreateUserAsync_CreatesNewUser_WhenUserDoesNotExist()
    {
        var telegramId = 12345L;
        var username = "testuser";
        var displayName = "Test User";

        var user = await _userService.GetOrCreateUserAsync(telegramId, username, displayName);

        Assert.NotNull(user);
        Assert.Equal(telegramId, user.TelegramId);
        Assert.Equal(username, user.Username);
        Assert.Equal(displayName, user.DisplayName);
    }

    [Fact]
    public async Task GetOrCreateUserAsync_ReturnsExistingUser_WhenUserExists()
    {
        var telegramId = 12345L;
        var existingUser = new User
        {
            TelegramId = telegramId,
            Username = "oldusername",
            DisplayName = "Old Name"
        };
        _context.Users.Add(existingUser);
        await _context.SaveChangesAsync();

        var user = await _userService.GetOrCreateUserAsync(telegramId, "newusername", "New Name");

        Assert.Equal(existingUser.Id, user.Id);
        Assert.Equal("newusername", user.Username);
        Assert.Equal("New Name", user.DisplayName);
    }

    [Fact]
    public async Task GetUserByTelegramIdAsync_ReturnsUser_WhenExists()
    {
        var telegramId = 12345L;
        var existingUser = new User
        {
            TelegramId = telegramId,
            Username = "testuser",
            DisplayName = "Test User"
        };
        _context.Users.Add(existingUser);
        await _context.SaveChangesAsync();

        var user = await _userService.GetUserByTelegramIdAsync(telegramId);

        Assert.NotNull(user);
        Assert.Equal(telegramId, user.TelegramId);
    }

    [Fact]
    public async Task GetUserByTelegramIdAsync_ReturnsNull_WhenNotExists()
    {
        var user = await _userService.GetUserByTelegramIdAsync(99999L);

        Assert.Null(user);
    }

    [Fact]
    public async Task GetUserByUsernameAsync_ReturnsUser_WithOrWithoutAtSign()
    {
        var existingUser = new User
        {
            TelegramId = 12345L,
            Username = "testuser",
            DisplayName = "Test User"
        };
        _context.Users.Add(existingUser);
        await _context.SaveChangesAsync();

        var userWithAt = await _userService.GetUserByUsernameAsync("@testuser");
        var userWithoutAt = await _userService.GetUserByUsernameAsync("testuser");

        Assert.NotNull(userWithAt);
        Assert.NotNull(userWithoutAt);
        Assert.Equal(existingUser.Id, userWithAt.Id);
        Assert.Equal(existingUser.Id, userWithoutAt.Id);
    }

    [Fact]
    public async Task SetCurrentListAsync_UpdatesUserCurrentList()
    {
        var user = new User
        {
            TelegramId = 12345L,
            Username = "testuser",
            DisplayName = "Test User"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var list = new ShoppingList
        {
            Name = "Test List",
            CreatorId = user.Id,
            ShareToken = "test-token"
        };
        _context.ShoppingLists.Add(list);
        await _context.SaveChangesAsync();

        await _userService.SetCurrentListAsync(user.Id, list.Id);

        var updatedUser = await _context.Users.FindAsync(user.Id);
        Assert.Equal(list.Id, updatedUser!.CurrentListId);
    }

    [Fact]
    public async Task GetCurrentListAsync_ReturnsCurrentList()
    {
        var user = new User
        {
            TelegramId = 12345L,
            Username = "testuser",
            DisplayName = "Test User"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var list = new ShoppingList
        {
            Name = "Test List",
            CreatorId = user.Id,
            ShareToken = "test-token"
        };
        _context.ShoppingLists.Add(list);
        await _context.SaveChangesAsync();

        user.CurrentListId = list.Id;
        await _context.SaveChangesAsync();

        var currentList = await _userService.GetCurrentListAsync(user.Id);

        Assert.NotNull(currentList);
        Assert.Equal(list.Id, currentList.Id);
    }
}
