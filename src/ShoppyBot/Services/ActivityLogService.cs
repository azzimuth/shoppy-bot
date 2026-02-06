using Microsoft.EntityFrameworkCore;
using ShoppyBot.Data;
using ShoppyBot.Models;

namespace ShoppyBot.Services;

public interface IActivityLogService
{
    Task LogActivityAsync(int listId, int userId, ActionType actionType, string? details = null);
    Task<IEnumerable<ActivityLog>> GetListActivityAsync(int listId, int limit = 20);
    Task CleanupOldLogsAsync(int daysToKeep = 30);
}

public class ActivityLogService : IActivityLogService
{
    private readonly ShoppyBotContext _context;

    public ActivityLogService(ShoppyBotContext context)
    {
        _context = context;
    }

    public async Task LogActivityAsync(int listId, int userId, ActionType actionType, string? details = null)
    {
        var log = new ActivityLog
        {
            ListId = listId,
            UserId = userId,
            ActionType = actionType,
            Details = details,
            CreatedAt = DateTime.UtcNow
        };

        _context.ActivityLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<ActivityLog>> GetListActivityAsync(int listId, int limit = 20)
    {
        return await _context.ActivityLogs
            .Include(l => l.User)
            .Where(l => l.ListId == listId)
            .OrderByDescending(l => l.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task CleanupOldLogsAsync(int daysToKeep = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(-daysToKeep);
        var oldLogs = await _context.ActivityLogs
            .Where(l => l.CreatedAt < cutoff)
            .ToListAsync();

        _context.ActivityLogs.RemoveRange(oldLogs);
        await _context.SaveChangesAsync();
    }
}
