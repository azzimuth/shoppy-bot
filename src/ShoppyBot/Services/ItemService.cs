using Microsoft.EntityFrameworkCore;
using ShoppyBot.Data;
using ShoppyBot.Models;

namespace ShoppyBot.Services;

public interface IItemService
{
    Task<ListItem> AddItemAsync(int listId, int userId, string itemName);
    Task<ListItem?> GetItemByIdAsync(int itemId);
    Task<IEnumerable<ListItem>> GetListItemsAsync(int listId, bool includeHidden = false);
    Task<ListItem?> GetItemByIndexAsync(int listId, int index, bool includeHidden = false);
    Task<bool> CheckItemAsync(int itemId);
    Task<bool> UncheckItemAsync(int itemId);
    Task<bool> HideItemAsync(int itemId);
    Task<bool> ShowItemAsync(int itemId);
    Task<bool> DeleteItemAsync(int itemId);
    Task<bool> ReorderItemAsync(int itemId, int newIndex);
}

public class ItemService : IItemService
{
    private readonly ShoppyBotContext _context;

    public ItemService(ShoppyBotContext context)
    {
        _context = context;
    }

    public async Task<ListItem> AddItemAsync(int listId, int userId, string itemName)
    {
        var maxOrder = await _context.ListItems
            .Where(i => i.ListId == listId)
            .MaxAsync(i => (int?)i.OrderIndex) ?? 0;

        var item = new ListItem
        {
            ListId = listId,
            ItemName = itemName,
            AddedById = userId,
            IsChecked = false,
            IsHidden = false,
            OrderIndex = maxOrder + 1,
            AddedAt = DateTime.UtcNow
        };

        _context.ListItems.Add(item);
        await _context.SaveChangesAsync();

        return item;
    }

    public async Task<ListItem?> GetItemByIdAsync(int itemId)
    {
        return await _context.ListItems.FindAsync(itemId);
    }

    public async Task<IEnumerable<ListItem>> GetListItemsAsync(int listId, bool includeHidden = false)
    {
        var query = _context.ListItems
            .Where(i => i.ListId == listId);

        if (!includeHidden)
            query = query.Where(i => !i.IsHidden);

        return await query
            .OrderBy(i => i.OrderIndex)
            .ToListAsync();
    }

    public async Task<ListItem?> GetItemByIndexAsync(int listId, int index, bool includeHidden = false)
    {
        var items = await GetListItemsAsync(listId, includeHidden);
        var itemList = items.ToList();

        if (index < 1 || index > itemList.Count)
            return null;

        return itemList[index - 1];
    }

    public async Task<bool> CheckItemAsync(int itemId)
    {
        var item = await _context.ListItems.FindAsync(itemId);
        if (item == null)
            return false;

        item.IsChecked = true;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UncheckItemAsync(int itemId)
    {
        var item = await _context.ListItems.FindAsync(itemId);
        if (item == null)
            return false;

        item.IsChecked = false;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> HideItemAsync(int itemId)
    {
        var item = await _context.ListItems.FindAsync(itemId);
        if (item == null)
            return false;

        item.IsHidden = true;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ShowItemAsync(int itemId)
    {
        var item = await _context.ListItems.FindAsync(itemId);
        if (item == null)
            return false;

        item.IsHidden = false;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteItemAsync(int itemId)
    {
        var item = await _context.ListItems.FindAsync(itemId);
        if (item == null)
            return false;

        _context.ListItems.Remove(item);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ReorderItemAsync(int itemId, int newIndex)
    {
        var item = await _context.ListItems.FindAsync(itemId);
        if (item == null)
            return false;

        var items = await _context.ListItems
            .Where(i => i.ListId == item.ListId && i.Id != itemId)
            .OrderBy(i => i.OrderIndex)
            .ToListAsync();

        items.Insert(Math.Max(0, Math.Min(newIndex - 1, items.Count)), item);

        for (var i = 0; i < items.Count; i++)
        {
            items[i].OrderIndex = i + 1;
        }

        await _context.SaveChangesAsync();
        return true;
    }
}
