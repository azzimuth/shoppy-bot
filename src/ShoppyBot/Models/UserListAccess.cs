namespace ShoppyBot.Models;

public enum ListRole
{
    User,
    Admin
}

public class UserListAccess
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ListId { get; set; }
    public ListRole Role { get; set; } = ListRole.User;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public ShoppingList List { get; set; } = null!;
}
