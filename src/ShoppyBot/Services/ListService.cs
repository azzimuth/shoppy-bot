using Microsoft.EntityFrameworkCore;
using ShoppyBot.Data;
using ShoppyBot.Models;
using ShoppyBot.Utils;

namespace ShoppyBot.Services;

public interface IListService
{
    Task<ShoppingList> CreateListAsync(int userId, string name);
    Task<ShoppingList?> GetListByIdAsync(int listId);
    Task<ShoppingList?> GetListByShareTokenAsync(string shareToken);
    Task<IEnumerable<(ShoppingList List, ListRole Role)>> GetUserListsAsync(int userId);
    Task<bool> RenameListAsync(int listId, int userId, string newName);
    Task<bool> DeleteListAsync(int listId, int userId);
    Task<string> RegenerateShareTokenAsync(int listId, int userId);
    Task<bool> JoinListAsync(int userId, string shareToken);
    Task<bool> LeaveListAsync(int listId, int userId);
    Task<ListRole?> GetUserRoleAsync(int listId, int userId);
    Task<bool> HasAccessAsync(int listId, int userId);
    Task<bool> IsAdminAsync(int listId, int userId);
    Task<IEnumerable<(User User, ListRole Role)>> GetListMembersAsync(int listId);
    Task<bool> PromoteUserAsync(int listId, int adminUserId, int targetUserId);
    Task<bool> DemoteUserAsync(int listId, int adminUserId, int targetUserId);
    Task<bool> RemoveUserAsync(int listId, int adminUserId, int targetUserId);
    Task<IEnumerable<User>> GetListUsersAsync(int listId);
}

public class ListService : IListService
{
    private readonly ShoppyBotContext _context;

    public ListService(ShoppyBotContext context)
    {
        _context = context;
    }

    public async Task<ShoppingList> CreateListAsync(int userId, string name)
    {
        var list = new ShoppingList
        {
            Name = name,
            CreatorId = userId,
            ShareToken = ShareTokenGenerator.GenerateToken(),
            CreatedAt = DateTime.UtcNow
        };

        _context.ShoppingLists.Add(list);
        await _context.SaveChangesAsync();

        var access = new UserListAccess
        {
            UserId = userId,
            ListId = list.Id,
            Role = ListRole.Admin,
            JoinedAt = DateTime.UtcNow
        };

        _context.UserListAccess.Add(access);
        await _context.SaveChangesAsync();

        return list;
    }

    public async Task<ShoppingList?> GetListByIdAsync(int listId)
    {
        return await _context.ShoppingLists
            .Include(l => l.Creator)
            .FirstOrDefaultAsync(l => l.Id == listId);
    }

    public async Task<ShoppingList?> GetListByShareTokenAsync(string shareToken)
    {
        return await _context.ShoppingLists
            .FirstOrDefaultAsync(l => l.ShareToken == shareToken);
    }

    public async Task<IEnumerable<(ShoppingList List, ListRole Role)>> GetUserListsAsync(int userId)
    {
        var access = await _context.UserListAccess
            .Include(a => a.List)
            .Where(a => a.UserId == userId)
            .ToListAsync();

        return access.Select(a => (a.List, a.Role));
    }

    public async Task<bool> RenameListAsync(int listId, int userId, string newName)
    {
        if (!await IsAdminAsync(listId, userId))
            return false;

        var list = await _context.ShoppingLists.FindAsync(listId);
        if (list == null)
            return false;

        list.Name = newName;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteListAsync(int listId, int userId)
    {
        if (!await IsAdminAsync(listId, userId))
            return false;

        var list = await _context.ShoppingLists.FindAsync(listId);
        if (list == null)
            return false;

        _context.ShoppingLists.Remove(list);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<string> RegenerateShareTokenAsync(int listId, int userId)
    {
        if (!await IsAdminAsync(listId, userId))
            return string.Empty;

        var list = await _context.ShoppingLists.FindAsync(listId);
        if (list == null)
            return string.Empty;

        list.ShareToken = ShareTokenGenerator.GenerateToken();
        await _context.SaveChangesAsync();
        return list.ShareToken;
    }

    public async Task<bool> JoinListAsync(int userId, string shareToken)
    {
        var list = await GetListByShareTokenAsync(shareToken);
        if (list == null)
            return false;

        var existingAccess = await _context.UserListAccess
            .FirstOrDefaultAsync(a => a.UserId == userId && a.ListId == list.Id);

        if (existingAccess != null)
            return true;

        var access = new UserListAccess
        {
            UserId = userId,
            ListId = list.Id,
            Role = ListRole.User,
            JoinedAt = DateTime.UtcNow
        };

        _context.UserListAccess.Add(access);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> LeaveListAsync(int listId, int userId)
    {
        var access = await _context.UserListAccess
            .FirstOrDefaultAsync(a => a.UserId == userId && a.ListId == listId);

        if (access == null)
            return false;

        var list = await _context.ShoppingLists.FindAsync(listId);
        if (list != null && list.CreatorId == userId)
            return false;

        _context.UserListAccess.Remove(access);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<ListRole?> GetUserRoleAsync(int listId, int userId)
    {
        var access = await _context.UserListAccess
            .FirstOrDefaultAsync(a => a.UserId == userId && a.ListId == listId);

        return access?.Role;
    }

    public async Task<bool> HasAccessAsync(int listId, int userId)
    {
        return await _context.UserListAccess
            .AnyAsync(a => a.UserId == userId && a.ListId == listId);
    }

    public async Task<bool> IsAdminAsync(int listId, int userId)
    {
        var access = await _context.UserListAccess
            .FirstOrDefaultAsync(a => a.UserId == userId && a.ListId == listId);

        return access?.Role == ListRole.Admin;
    }

    public async Task<IEnumerable<(User User, ListRole Role)>> GetListMembersAsync(int listId)
    {
        var access = await _context.UserListAccess
            .Include(a => a.User)
            .Where(a => a.ListId == listId)
            .ToListAsync();

        return access.Select(a => (a.User, a.Role));
    }

    public async Task<bool> PromoteUserAsync(int listId, int adminUserId, int targetUserId)
    {
        if (!await IsAdminAsync(listId, adminUserId))
            return false;

        var access = await _context.UserListAccess
            .FirstOrDefaultAsync(a => a.UserId == targetUserId && a.ListId == listId);

        if (access == null || access.Role == ListRole.Admin)
            return false;

        access.Role = ListRole.Admin;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DemoteUserAsync(int listId, int adminUserId, int targetUserId)
    {
        if (!await IsAdminAsync(listId, adminUserId))
            return false;

        var list = await _context.ShoppingLists.FindAsync(listId);
        if (list != null && list.CreatorId == targetUserId)
            return false;

        var access = await _context.UserListAccess
            .FirstOrDefaultAsync(a => a.UserId == targetUserId && a.ListId == listId);

        if (access == null || access.Role == ListRole.User)
            return false;

        access.Role = ListRole.User;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveUserAsync(int listId, int adminUserId, int targetUserId)
    {
        if (!await IsAdminAsync(listId, adminUserId))
            return false;

        var list = await _context.ShoppingLists.FindAsync(listId);
        if (list != null && list.CreatorId == targetUserId)
            return false;

        var access = await _context.UserListAccess
            .FirstOrDefaultAsync(a => a.UserId == targetUserId && a.ListId == listId);

        if (access == null)
            return false;

        _context.UserListAccess.Remove(access);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<User>> GetListUsersAsync(int listId)
    {
        return await _context.UserListAccess
            .Include(a => a.User)
            .Where(a => a.ListId == listId)
            .Select(a => a.User)
            .ToListAsync();
    }
}
