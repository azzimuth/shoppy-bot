using Microsoft.EntityFrameworkCore;
using Xunit;
using ShoppyBot.Data;
using ShoppyBot.Models;
using ShoppyBot.Services;

namespace ShoppyBot.Tests.Services;

public class ListServiceTests : IDisposable
{
    private readonly ShoppyBotContext _context;
    private readonly ListService _listService;
    private readonly User _testUser;

    public ListServiceTests()
    {
        var options = new DbContextOptionsBuilder<ShoppyBotContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ShoppyBotContext(options);
        _listService = new ListService(_context);

        _testUser = new User
        {
            TelegramId = 12345L,
            Username = "testuser",
            DisplayName = "Test User"
        };
        _context.Users.Add(_testUser);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task CreateListAsync_CreatesListWithAdminAccess()
    {
        var listName = "Groceries";

        var list = await _listService.CreateListAsync(_testUser.Id, listName);

        Assert.NotNull(list);
        Assert.Equal(listName, list.Name);
        Assert.Equal(_testUser.Id, list.CreatorId);
        Assert.NotEmpty(list.ShareToken);

        var access = await _context.UserListAccess
            .FirstOrDefaultAsync(a => a.UserId == _testUser.Id && a.ListId == list.Id);
        Assert.NotNull(access);
        Assert.Equal(ListRole.Admin, access.Role);
    }

    [Fact]
    public async Task GetUserListsAsync_ReturnsUserLists()
    {
        var list1 = await _listService.CreateListAsync(_testUser.Id, "List 1");
        var list2 = await _listService.CreateListAsync(_testUser.Id, "List 2");

        var lists = (await _listService.GetUserListsAsync(_testUser.Id)).ToList();

        Assert.Equal(2, lists.Count);
        Assert.All(lists, l => Assert.Equal(ListRole.Admin, l.Role));
    }

    [Fact]
    public async Task RenameListAsync_RenamesList_WhenUserIsAdmin()
    {
        var list = await _listService.CreateListAsync(_testUser.Id, "Old Name");

        var result = await _listService.RenameListAsync(list.Id, _testUser.Id, "New Name");

        Assert.True(result);
        var updatedList = await _listService.GetListByIdAsync(list.Id);
        Assert.Equal("New Name", updatedList!.Name);
    }

    [Fact]
    public async Task RenameListAsync_ReturnsFalse_WhenUserIsNotAdmin()
    {
        var list = await _listService.CreateListAsync(_testUser.Id, "Old Name");

        var otherUser = new User
        {
            TelegramId = 99999L,
            Username = "other",
            DisplayName = "Other"
        };
        _context.Users.Add(otherUser);
        await _context.SaveChangesAsync();

        var result = await _listService.RenameListAsync(list.Id, otherUser.Id, "New Name");

        Assert.False(result);
    }

    [Fact]
    public async Task JoinListAsync_AddsUserToList()
    {
        var list = await _listService.CreateListAsync(_testUser.Id, "Test List");

        var otherUser = new User
        {
            TelegramId = 99999L,
            Username = "other",
            DisplayName = "Other"
        };
        _context.Users.Add(otherUser);
        await _context.SaveChangesAsync();

        var result = await _listService.JoinListAsync(otherUser.Id, list.ShareToken);

        Assert.True(result);
        Assert.True(await _listService.HasAccessAsync(list.Id, otherUser.Id));
        var role = await _listService.GetUserRoleAsync(list.Id, otherUser.Id);
        Assert.Equal(ListRole.User, role);
    }

    [Fact]
    public async Task JoinListAsync_ReturnsFalse_WhenTokenInvalid()
    {
        var result = await _listService.JoinListAsync(_testUser.Id, "invalid-token");

        Assert.False(result);
    }

    [Fact]
    public async Task LeaveListAsync_RemovesUserFromList()
    {
        var list = await _listService.CreateListAsync(_testUser.Id, "Test List");

        var otherUser = new User
        {
            TelegramId = 99999L,
            Username = "other",
            DisplayName = "Other"
        };
        _context.Users.Add(otherUser);
        await _context.SaveChangesAsync();

        await _listService.JoinListAsync(otherUser.Id, list.ShareToken);
        var result = await _listService.LeaveListAsync(list.Id, otherUser.Id);

        Assert.True(result);
        Assert.False(await _listService.HasAccessAsync(list.Id, otherUser.Id));
    }

    [Fact]
    public async Task LeaveListAsync_ReturnsFalse_WhenCreatorTriesToLeave()
    {
        var list = await _listService.CreateListAsync(_testUser.Id, "Test List");

        var result = await _listService.LeaveListAsync(list.Id, _testUser.Id);

        Assert.False(result);
    }

    [Fact]
    public async Task PromoteUserAsync_PromotesUserToAdmin()
    {
        var list = await _listService.CreateListAsync(_testUser.Id, "Test List");

        var otherUser = new User
        {
            TelegramId = 99999L,
            Username = "other",
            DisplayName = "Other"
        };
        _context.Users.Add(otherUser);
        await _context.SaveChangesAsync();

        await _listService.JoinListAsync(otherUser.Id, list.ShareToken);
        var result = await _listService.PromoteUserAsync(list.Id, _testUser.Id, otherUser.Id);

        Assert.True(result);
        Assert.True(await _listService.IsAdminAsync(list.Id, otherUser.Id));
    }

    [Fact]
    public async Task DemoteUserAsync_DemotesAdminToUser()
    {
        var list = await _listService.CreateListAsync(_testUser.Id, "Test List");

        var otherUser = new User
        {
            TelegramId = 99999L,
            Username = "other",
            DisplayName = "Other"
        };
        _context.Users.Add(otherUser);
        await _context.SaveChangesAsync();

        await _listService.JoinListAsync(otherUser.Id, list.ShareToken);
        await _listService.PromoteUserAsync(list.Id, _testUser.Id, otherUser.Id);
        var result = await _listService.DemoteUserAsync(list.Id, _testUser.Id, otherUser.Id);

        Assert.True(result);
        Assert.False(await _listService.IsAdminAsync(list.Id, otherUser.Id));
    }

    [Fact]
    public async Task DemoteUserAsync_ReturnsFalse_WhenDemotingCreator()
    {
        var list = await _listService.CreateListAsync(_testUser.Id, "Test List");

        var otherUser = new User
        {
            TelegramId = 99999L,
            Username = "other",
            DisplayName = "Other"
        };
        _context.Users.Add(otherUser);
        await _context.SaveChangesAsync();

        await _listService.JoinListAsync(otherUser.Id, list.ShareToken);
        await _listService.PromoteUserAsync(list.Id, _testUser.Id, otherUser.Id);

        var result = await _listService.DemoteUserAsync(list.Id, otherUser.Id, _testUser.Id);

        Assert.False(result);
    }

    [Fact]
    public async Task RemoveUserAsync_RemovesUserFromList()
    {
        var list = await _listService.CreateListAsync(_testUser.Id, "Test List");

        var otherUser = new User
        {
            TelegramId = 99999L,
            Username = "other",
            DisplayName = "Other"
        };
        _context.Users.Add(otherUser);
        await _context.SaveChangesAsync();

        await _listService.JoinListAsync(otherUser.Id, list.ShareToken);
        var result = await _listService.RemoveUserAsync(list.Id, _testUser.Id, otherUser.Id);

        Assert.True(result);
        Assert.False(await _listService.HasAccessAsync(list.Id, otherUser.Id));
    }

    [Fact]
    public async Task DeleteListAsync_DeletesList_WhenUserIsAdmin()
    {
        var list = await _listService.CreateListAsync(_testUser.Id, "Test List");

        var result = await _listService.DeleteListAsync(list.Id, _testUser.Id);

        Assert.True(result);
        var deletedList = await _listService.GetListByIdAsync(list.Id);
        Assert.Null(deletedList);
    }

    [Fact]
    public async Task GetListMembersAsync_ReturnsAllMembers()
    {
        var list = await _listService.CreateListAsync(_testUser.Id, "Test List");

        var otherUser = new User
        {
            TelegramId = 99999L,
            Username = "other",
            DisplayName = "Other"
        };
        _context.Users.Add(otherUser);
        await _context.SaveChangesAsync();

        await _listService.JoinListAsync(otherUser.Id, list.ShareToken);

        var members = (await _listService.GetListMembersAsync(list.Id)).ToList();

        Assert.Equal(2, members.Count);
        Assert.Contains(members, m => m.User.Id == _testUser.Id && m.Role == ListRole.Admin);
        Assert.Contains(members, m => m.User.Id == otherUser.Id && m.Role == ListRole.User);
    }
}
