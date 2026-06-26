using System.Globalization;
using CirilloCash.Services;

namespace CirilloCash;

public partial class CatalogPage : ContentPage
{
    private MenuCategory currentCategory = MenuCategory.Drink;

    public CatalogPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Refresh();
    }

    private void OnDrinksTabClicked(object sender, EventArgs e)
    {
        currentCategory = MenuCategory.Drink;
        Refresh();
    }

    private void OnFoodTabClicked(object sender, EventArgs e)
    {
        currentCategory = MenuCategory.Food;
        Refresh();
    }

    private void Refresh()
    {
        ItemsView.ItemsSource = CatalogService.Instance.GetByCategory(currentCategory);
        DrinksTabBtn.FontAttributes = currentCategory == MenuCategory.Drink ? FontAttributes.Bold : FontAttributes.None;
        FoodTabBtn.FontAttributes   = currentCategory == MenuCategory.Food  ? FontAttributes.Bold : FontAttributes.None;
    }

    private async void OnAddClicked(object sender, EventArgs e)
    {
        var name = await DisplayPromptAsync("Nuovo articolo", "Nome:", "OK", "Annulla", "es. Panino");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var priceText = await DisplayPromptAsync("Nuovo articolo", $"Prezzo (€) per '{name.Trim()}':", "OK", "Annulla",
            "0.00", keyboard: Keyboard.Numeric);
        if (!TryParsePrice(priceText, out var price))
        {
            await DisplayAlertAsync("Catalogo", "Prezzo non valido.", "OK");
            return;
        }

        var nextOrder = CatalogService.Instance.GetByCategory(currentCategory).Count;
        CatalogService.Instance.Upsert(new CatalogItem
        {
            Name = name.Trim(),
            Price = price,
            Category = currentCategory,
            SortOrder = nextOrder
        });

        Refresh();
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not string id)
        {
            return;
        }

        var item = CatalogService.Instance.LoadAll().FirstOrDefault(i => i.Id == id);
        if (item is null)
        {
            return;
        }

        var name = await DisplayPromptAsync("Modifica articolo", "Nome:", "OK", "Annulla", initialValue: item.Name);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var priceText = await DisplayPromptAsync("Modifica articolo", "Prezzo (€):", "OK", "Annulla",
            initialValue: item.Price.ToString("0.00", CultureInfo.InvariantCulture), keyboard: Keyboard.Numeric);
        if (!TryParsePrice(priceText, out var price))
        {
            await DisplayAlertAsync("Catalogo", "Prezzo non valido.", "OK");
            return;
        }

        item.Name = name.Trim();
        item.Price = price;
        CatalogService.Instance.Upsert(item);
        Refresh();
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not string id)
        {
            return;
        }

        var item = CatalogService.Instance.LoadAll().FirstOrDefault(i => i.Id == id);
        if (item is null)
        {
            return;
        }

        var confirmed = await DisplayAlertAsync("Catalogo", $"Eliminare '{item.Name}'?", "Elimina", "Annulla");
        if (!confirmed)
        {
            return;
        }

        CatalogService.Instance.Delete(id);
        Refresh();
    }

    private static bool TryParsePrice(string? text, out double price)
    {
        price = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.Trim().Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out price) && price >= 0;
    }
}
