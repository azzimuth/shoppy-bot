using Microsoft.EntityFrameworkCore;
using Xunit;
using ShoppyBot.Data;
using ShoppyBot.Models;
using ShoppyBot.Services;

namespace ShoppyBot.Tests.Services;

public class ItemServiceTests : IDisposable
{
    private readonly ShoppyBotContext _context;
    private readonly ItemService _itemService;
    private readonly User _testUser;
    private readonly ShoppingList _testList;

    public ItemServiceTests()
    {
        var options = new DbContextOptionsBuilder<ShoppyBotContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ShoppyBotContext(options);
        _itemService = new ItemService(_context);

        _testUser = new User
        {
            TelegramId = 12345L,
            Username = "testuser",
            DisplayName = "Test User"
        };
        _context.Users.Add(_testUser);
        _context.SaveChanges();

        _testList = new ShoppingList
        {
            Name = "Test List",
            CreatorId = _testUser.Id,
            ShareToken = "test-token"
        };
        _context.ShoppingLists.Add(_testList);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task AddItemAsync_AddsItemToList()
    {
        var itemName = "Milk";

        var item = await _itemService.AddItemAsync(_testList.Id, _testUser.Id, itemName);

        Assert.NotNull(item);
        Assert.Equal(itemName, item.ItemName);
        Assert.Equal(_testList.Id, item.ListId);
        Assert.Equal(_testUser.Id, item.AddedById);
        Assert.False(item.IsChecked);
        Assert.False(item.IsHidden);
    }

    [Fact]
    public async Task AddItemAsync_AssignsCorrectOrderIndex()
    {
        await _itemService.AddItemAsync(_testList.Id, _testUser.Id, "Item 1");
        await _itemService.AddItemAsync(_testList.Id, _testUser.Id, "Item 2");
        var item3 = await _itemService.AddItemAsync(_testList.Id, _testUser.Id, "Item 3");

        Assert.Equal(3, item3.OrderIndex);
    }

    [Fact]
    public async Task GetListItemsAsync_ReturnsVisibleItemsOnly_ByDefault()
    {
        var item1 = await _itemService.AddItemAsync(_testList.Id, _testUser.Id, "Visible Item");
        var item2 = await _itemService.AddItemAsync(_testList.Id, _testUser.Id, "Hidden Item");
        await _itemService.HideItemAsync(item2.Id);

        var items = (await _itemService.GetListItemsAsync(_testList.Id)).ToList();

        Assert.Single(items);
        Assert.Equal("Visible Item", items[0].ItemName);
    }

    [Fact]
    public async Task GetListItemsAsync_ReturnsAllItems_WhenIncludeHiddenIsTrue()
    {
        var item1 = await _itemService.AddItemAsync(_testList.Id, _testUser.Id, "Visible Item");
        var item2 = await _itemService.AddItemAsync(_testList.Id, _testUser.Id, "Hidden Item");
        await _itemService.HideItemAsync(item2.Id);

        var items = (await _itemService.GetListItemsAsync(_testList.Id, includeHidden: true)).ToList();

        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task GetItemByIndexAsync_ReturnsCorrectItem()
    {
        await _itemService.AddItemAsync(_testList.Id, _testUser.Id, "Item 1");
        await _itemService.AddItemAsync(_testList.Id, _testUser.Id, "Item 2");
        await _itemService.AddItemAsync(_testList.Id, _testUser.Id, "Item 3");

        var item = await _itemService.GetItemByIndexAsync(_testList.Id, 2);

        Assert.NotNull(item);
        Assert.Equal("Item 2", item.ItemName);
    }

    [Fact]
    public async Task GetItemByIndexAsync_ReturnsNull_WhenIndexOutOfRange()
    {
        await _itemService.AddItemAsync(_testList.Id, _testUser.Id, "Item 1");

        var item = await _itemService.GetItemByIndexAsync(_testList.Id, 99);

        Assert.Null(item);
    }

    [Fact]
    public async Task CheckItemAsync_MarksItemAsChecked()
    {
        var item = await _itemService.AddItemAsync(_testList.Id, _testUser.Id, "Test Item");

        var result = await _itemService.CheckItemAsync(item.Id);

        Assert.True(result);
        var updatedItem = await _itemService.GetItemByIdAsync(item.Id);
        Assert.True(updatedItem!.IsChecked);
    }

    [Fact]
    public async Task UncheckItemAsync_MarksItemAsUnchecked()
    {
        var item = await _itemService.AddItemAsync(_testList.Id, _testUser.Id, "Test Item");
        await _itemService.CheckItemAsync(item.Id);

        var result = await _itemService.UncheckItemAsync(item.Id);

        Assert.True(result);
        var updatedItem = await _itemService.GetItemByIdAsync(item.Id);
        Assert.False(updatedItem!.IsChecked);
    }

    [Fact]
    public async Task HideItemAsync_MarksItemAsHidden()
    {
        var item = await _itemService.AddItemAsync(_testList.Id, _testUser.Id, "Test Item");

        var result = await _itemService.HideItemAsync(item.Id);

        Assert.True(result);
        var updatedItem = await _itemService.GetItemByIdAsync(item.Id);
        Assert.True(updatedItem!.IsHidden);
    }

    [Fact]
    public async Task ShowItemAsync_MarksItemAsVisible()
    {
        var item = await _itemService.AddItemAsync(_testList.Id, _testUser.Id, "Test Item");
        await _itemService.HideItemAsync(item.Id);

        var result = await _itemService.ShowItemAsync(item.Id);

        Assert.True(result);
        var updatedItem = await _itemService.GetItemByIdAsync(item.Id);
        Assert.False(updatedItem!.IsHidden);
    }

    [Fact]
    public async Task DeleteItemAsync_RemovesItem()
    {
        var item = await _itemService.AddItemAsync(_testList.Id, _testUser.Id, "Test Item");

        var result = await _itemService.DeleteItemAsync(item.Id);

        Assert.True(result);
        var deletedItem = await _itemService.GetItemByIdAsync(item.Id);
        Assert.Null(deletedItem);
    }

    [Fact]
    public async Task CheckItemAsync_ReturnsFalse_WhenItemNotFound()
    {
        var result = await _itemService.CheckItemAsync(99999);

        Assert.False(result);
    }

    [Fact]
    public async Task ReorderItemAsync_ChangesItemOrder()
    {
        var item1 = await _itemService.AddItemAsync(_testList.Id, _testUser.Id, "Item 1");
        var item2 = await _itemService.AddItemAsync(_testList.Id, _testUser.Id, "Item 2");
        var item3 = await _itemService.AddItemAsync(_testList.Id, _testUser.Id, "Item 3");

        await _itemService.ReorderItemAsync(item3.Id, 1);

        var items = (await _itemService.GetListItemsAsync(_testList.Id)).ToList();
        Assert.Equal("Item 3", items[0].ItemName);
        Assert.Equal("Item 1", items[1].ItemName);
        Assert.Equal("Item 2", items[2].ItemName);
    }
}
