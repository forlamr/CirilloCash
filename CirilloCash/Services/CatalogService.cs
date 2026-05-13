using System.Text.Json;

namespace CirilloCash.Services;

public sealed class CatalogService
{
    private const string FileName = "catalog.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly Lazy<CatalogService> _instance = new(() => new CatalogService());
    public static CatalogService Instance => _instance.Value;

    private readonly object sync = new();
    private List<CatalogItem>? cache;

    private string FilePath => Path.Combine(FileSystem.AppDataDirectory, FileName);

    public List<CatalogItem> LoadAll()
    {
        lock (sync)
        {
            if (cache is not null)
            {
                return CloneList(cache);
            }

            cache = ReadFromDisk() ?? CreateDefaults();
            if (!File.Exists(FilePath))
            {
                WriteToDisk(cache);
            }

            return CloneList(cache);
        }
    }

    public List<CatalogItem> GetByCategory(MenuCategory category)
    {
        return LoadAll()
            .Where(i => i.Category == category)
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void SaveAll(IEnumerable<CatalogItem> items)
    {
        var snapshot = items
            .Where(i => !string.IsNullOrWhiteSpace(i.Name))
            .Select(i => new CatalogItem
            {
                Id = string.IsNullOrWhiteSpace(i.Id) ? Guid.NewGuid().ToString("N") : i.Id,
                Name = i.Name.Trim(),
                Price = i.Price,
                Category = i.Category,
                SortOrder = i.SortOrder
            })
            .ToList();

        lock (sync)
        {
            cache = snapshot;
            WriteToDisk(snapshot);
        }
    }

    public void Upsert(CatalogItem item)
    {
        var all = LoadAll();
        var idx = all.FindIndex(i => i.Id == item.Id);
        if (idx >= 0)
        {
            all[idx] = item;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(item.Id))
            {
                item.Id = Guid.NewGuid().ToString("N");
            }
            all.Add(item);
        }

        SaveAll(all);
    }

    public void Delete(string id)
    {
        var all = LoadAll();
        all.RemoveAll(i => i.Id == id);
        SaveAll(all);
    }

    private List<CatalogItem>? ReadFromDisk()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            var json = File.ReadAllText(FilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return JsonSerializer.Deserialize<List<CatalogItem>>(json, JsonOptions) ?? new List<CatalogItem>();
        }
        catch
        {
            return null;
        }
    }

    private void WriteToDisk(List<CatalogItem> items)
    {
        Directory.CreateDirectory(FileSystem.AppDataDirectory);
        var json = JsonSerializer.Serialize(items, JsonOptions);
        File.WriteAllText(FilePath, json);
    }

    private static List<CatalogItem> CreateDefaults()
    {
        return new List<CatalogItem>
        {
            new() { Name = "Bionda",  Price = 4.0, Category = MenuCategory.Drink, SortOrder = 0 },
            new() { Name = "Rossa",   Price = 4.5, Category = MenuCategory.Drink, SortOrder = 1 },
            new() { Name = "Spritz",  Price = 4.0, Category = MenuCategory.Drink, SortOrder = 2 },
            new() { Name = "Acqua",   Price = 1.0, Category = MenuCategory.Drink, SortOrder = 3 },
            new() { Name = "Bibita",  Price = 2.0, Category = MenuCategory.Drink, SortOrder = 4 },
            new() { Name = "Anguria", Price = 3.0, Category = MenuCategory.Drink, SortOrder = 5 }
        };
    }

    private static List<CatalogItem> CloneList(List<CatalogItem> source)
    {
        return source.Select(i => new CatalogItem
        {
            Id = i.Id,
            Name = i.Name,
            Price = i.Price,
            Category = i.Category,
            SortOrder = i.SortOrder
        }).ToList();
    }
}
