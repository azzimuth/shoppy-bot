namespace ShoppyBot.Models;

public enum ConversationState
{
    None,
    WaitingForListName,
    WaitingForItemName,
    WaitingForNewListName,  // for rename
    WaitingForItemNumber    // for check/uncheck/hide/show
}

public class User
{
    public int Id { get; set; }
    public long TelegramId { get; set; }
    public string? Username { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CurrentListId { get; set; }
    public ConversationState ConversationState { get; set; } = ConversationState.None;
    public string? PendingAction { get; set; }  // stores additional context like which action triggered the state

    public ShoppingList? CurrentList { get; set; }
    public ICollection<ShoppingList> CreatedLists { get; set; } = new List<ShoppingList>();
    public ICollection<UserListAccess> ListAccess { get; set; } = new List<UserListAccess>();
    public ICollection<ListItem> AddedItems { get; set; } = new List<ListItem>();
    public ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();
}
