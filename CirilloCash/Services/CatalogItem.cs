namespace CirilloCash.Services;

public enum MenuCategory
{
    Drink = 0,
    Food = 1
}

public sealed class CatalogItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public double Price { get; set; }
    public MenuCategory Category { get; set; }
    public int SortOrder { get; set; }
}
