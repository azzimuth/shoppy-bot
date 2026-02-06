namespace ShoppyBot.Models;

public class ShoppingList
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CreatorId { get; set; }
    public string ShareToken { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User Creator { get; set; } = null!;
    public ICollection<UserListAccess> UserAccess { get; set; } = new List<UserListAccess>();
    public ICollection<ListItem> Items { get; set; } = new List<ListItem>();
    public ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();
}
