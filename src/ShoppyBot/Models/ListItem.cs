namespace ShoppyBot.Models;

public class ListItem
{
    public int Id { get; set; }
    public int ListId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public bool IsChecked { get; set; }
    public bool IsHidden { get; set; }
    public int AddedById { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public int OrderIndex { get; set; }

    public ShoppingList List { get; set; } = null!;
    public User AddedBy { get; set; } = null!;
}
