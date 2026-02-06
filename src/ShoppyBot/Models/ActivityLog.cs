namespace ShoppyBot.Models;

public enum ActionType
{
    ListCreated,
    ListRenamed,
    ListDeleted,
    ItemAdded,
    ItemChecked,
    ItemUnchecked,
    ItemHidden,
    ItemShown,
    UserJoined,
    UserLeft,
    UserPromoted,
    UserDemoted,
    UserRemoved
}

public class ActivityLog
{
    public int Id { get; set; }
    public int ListId { get; set; }
    public int UserId { get; set; }
    public ActionType ActionType { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ShoppingList List { get; set; } = null!;
    public User User { get; set; } = null!;
}
