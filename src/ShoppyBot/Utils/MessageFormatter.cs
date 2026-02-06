using System.Text;
using ShoppyBot.Models;

namespace ShoppyBot.Utils;

public static class MessageFormatter
{
    public static string FormatListItems(IEnumerable<ListItem> items, bool showHidden = false)
    {
        var visibleItems = showHidden
            ? items.OrderBy(i => i.OrderIndex).ToList()
            : items.Where(i => !i.IsHidden).OrderBy(i => i.OrderIndex).ToList();

        if (!visibleItems.Any())
            return "📝 The list is empty.";

        var sb = new StringBuilder();
        var index = 1;

        foreach (var item in visibleItems)
        {
            var checkMark = item.IsChecked ? "✅" : "⬜";
            var hiddenMark = item.IsHidden ? " 👁️" : "";
            sb.AppendLine($"{index}. {checkMark} {item.ItemName}{hiddenMark}");
            index++;
        }

        return sb.ToString().TrimEnd();
    }

    public static string FormatUserLists(IEnumerable<(ShoppingList List, ListRole Role)> lists)
    {
        var listArray = lists.ToArray();

        if (!listArray.Any())
            return "📋 You don't have any lists yet. Create one with /newlist <name>";

        var sb = new StringBuilder();
        sb.AppendLine("📋 Your Shopping Lists:");
        sb.AppendLine();

        var index = 1;
        foreach (var (list, role) in listArray)
        {
            var roleIcon = role == ListRole.Admin ? "👑" : "👤";
            sb.AppendLine($"{index}. {list.Name} {roleIcon}");
            index++;
        }

        return sb.ToString().TrimEnd();
    }

    public static string FormatListMembers(IEnumerable<(User User, ListRole Role)> members)
    {
        var memberArray = members.ToArray();

        if (!memberArray.Any())
            return "No members found.";

        var sb = new StringBuilder();
        sb.AppendLine("👥 List Members:");
        sb.AppendLine();

        foreach (var (user, role) in memberArray)
        {
            var roleIcon = role == ListRole.Admin ? "👑" : "👤";
            var displayName = !string.IsNullOrEmpty(user.Username)
                ? $"@{user.Username}"
                : user.DisplayName;
            sb.AppendLine($"{roleIcon} {displayName}");
        }

        return sb.ToString().TrimEnd();
    }

    public static string FormatActivityLog(IEnumerable<ActivityLog> logs)
    {
        var logArray = logs.ToArray();

        if (!logArray.Any())
            return "📜 No recent activity.";

        var sb = new StringBuilder();
        sb.AppendLine("📜 Recent Activity:");
        sb.AppendLine();

        foreach (var log in logArray.Take(20))
        {
            var timestamp = log.CreatedAt.ToString("MM/dd HH:mm");
            var action = FormatActionType(log.ActionType);
            var details = !string.IsNullOrEmpty(log.Details) ? $": {log.Details}" : "";
            sb.AppendLine($"[{timestamp}] {action}{details}");
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatActionType(ActionType actionType)
    {
        return actionType switch
        {
            ActionType.ListCreated => "📝 List created",
            ActionType.ListRenamed => "✏️ List renamed",
            ActionType.ListDeleted => "🗑️ List deleted",
            ActionType.ItemAdded => "➕ Item added",
            ActionType.ItemChecked => "✅ Item checked",
            ActionType.ItemUnchecked => "⬜ Item unchecked",
            ActionType.ItemHidden => "👁️ Item hidden",
            ActionType.ItemShown => "👁️ Item shown",
            ActionType.UserJoined => "👋 User joined",
            ActionType.UserLeft => "👋 User left",
            ActionType.UserPromoted => "⬆️ User promoted",
            ActionType.UserDemoted => "⬇️ User demoted",
            ActionType.UserRemoved => "🚫 User removed",
            _ => "❓ Unknown action"
        };
    }

    public static string FormatNotification(ActionType actionType, string userName, string? details = null)
    {
        var action = actionType switch
        {
            ActionType.ItemAdded => $"➕ {userName} added",
            ActionType.ItemChecked => $"✅ {userName} checked",
            ActionType.ItemUnchecked => $"⬜ {userName} unchecked",
            ActionType.ItemHidden => $"👁️ {userName} hid",
            ActionType.ItemShown => $"👁️ {userName} showed",
            ActionType.UserJoined => $"👋 {userName} joined the list",
            ActionType.UserLeft => $"👋 {userName} left the list",
            ActionType.ListRenamed => $"✏️ {userName} renamed the list",
            _ => $"🔔 {userName} made changes"
        };

        return !string.IsNullOrEmpty(details) ? $"{action}: {details}" : action;
    }

    public static string EscapeMarkdown(string text)
    {
        var specialChars = new[] { '_', '*', '[', ']', '(', ')', '~', '`', '>', '#', '+', '-', '=', '|', '{', '}', '.', '!' };
        foreach (var c in specialChars)
        {
            text = text.Replace(c.ToString(), $"\\{c}");
        }
        return text;
    }
}
