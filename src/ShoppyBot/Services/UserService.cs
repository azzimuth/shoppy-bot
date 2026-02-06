using Microsoft.EntityFrameworkCore;
using ShoppyBot.Data;
using ShoppyBot.Models;

namespace ShoppyBot.Services;

public interface IUserService
{
    Task<User> GetOrCreateUserAsync(long telegramId, string? username, string displayName);
    Task<User?> GetUserByTelegramIdAsync(long telegramId);
    Task<User?> GetUserByIdAsync(int userId);
    Task<User?> GetUserByUsernameAsync(string username);
    Task SetCurrentListAsync(int userId, int? listId);
    Task<ShoppingList?> GetCurrentListAsync(int userId);
    Task SetConversationStateAsync(int userId, ConversationState state, string? pendingAction = null);
    Task ClearConversationStateAsync(int userId);
}

public class UserService : IUserService
{
    private readonly ShoppyBotContext _context;

    public UserService(ShoppyBotContext context)
    {
        _context = context;
    }

    public async Task<User> GetOrCreateUserAsync(long telegramId, string? username, string displayName)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.TelegramId == telegramId);

        if (user == null)
        {
            user = new User
            {
                TelegramId = telegramId,
                Username = username,
                DisplayName = displayName,
                CreatedAt = DateTime.UtcNow
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }
        else
        {
            var updated = false;
            if (user.Username != username)
            {
                user.Username = username;
                updated = true;
            }
            if (user.DisplayName != displayName)
            {
                user.DisplayName = displayName;
                updated = true;
            }
            if (updated)
            {
                await _context.SaveChangesAsync();
            }
        }

        return user;
    }

    public async Task<User?> GetUserByTelegramIdAsync(long telegramId)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.TelegramId == telegramId);
    }

    public async Task<User?> GetUserByIdAsync(int userId)
    {
        return await _context.Users.FindAsync(userId);
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        var normalizedUsername = username.TrimStart('@');
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Username != null &&
                u.Username.ToLower() == normalizedUsername.ToLower());
    }

    public async Task SetCurrentListAsync(int userId, int? listId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user != null)
        {
            user.CurrentListId = listId;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<ShoppingList?> GetCurrentListAsync(int userId)
    {
        var user = await _context.Users
            .Include(u => u.CurrentList)
            .FirstOrDefaultAsync(u => u.Id == userId);

        return user?.CurrentList;
    }

    public async Task SetConversationStateAsync(int userId, ConversationState state, string? pendingAction = null)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user != null)
        {
            user.ConversationState = state;
            user.PendingAction = pendingAction;
            await _context.SaveChangesAsync();
        }
    }

    public async Task ClearConversationStateAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user != null)
        {
            user.ConversationState = ConversationState.None;
            user.PendingAction = null;
            await _context.SaveChangesAsync();
        }
    }
}
