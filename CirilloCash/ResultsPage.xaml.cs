using System.Globalization;
using CirilloCash.Services;

namespace CirilloCash
{
    public partial class ResultsPage : ContentPage
    {
        private const string TotalKey = "TOTALE";
        private const string DateKey = "DATA";

        public ResultsPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            ResultsLabel.Text = string.Empty;
        }

        public async void OnCalculateClicked(object sender, EventArgs e)
        {
            ResultsLabel.Text = string.Empty;

            if (!TransactionsStorage.Exists())
            {
                ResultsLabel.Text = "Nessuna transazione registrata.";
                return;
            }

            var content = await TransactionsStorage.ReadAllAsync();
            var aggregate = Aggregate(content);
            ResultsLabel.Text = FormatAggregate(aggregate);
        }

        private static Aggregated Aggregate(string content)
        {
            var quantities = new Dictionary<string, double>();
            double total = 0;

            foreach (var line in content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                foreach (var pair in line.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    var kv = pair.Split('=', 2);
                    if (kv.Length != 2)
                    {
                        continue;
                    }

                    var key = kv[0].Trim();
                    var rawValue = kv[1].Trim();

                    if (key.Equals(DateKey, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!TryParseDecimal(rawValue, out var value))
                    {
                        continue;
                    }

                    if (key.Equals(TotalKey, StringComparison.OrdinalIgnoreCase))
                    {
                        total += value;
                        continue;
                    }

                    quantities[key] = quantities.TryGetValue(key, out var existing)
                        ? existing + value
                        : value;
                }
            }

            return new Aggregated(quantities, total);
        }

        private static bool TryParseDecimal(string text, out double value)
        {
            var normalized = text.Replace(',', '.');
            return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static string FormatAggregate(Aggregated agg)
        {
            if (agg.Quantities.Count == 0 && agg.Total == 0)
            {
                return "Nessuna transazione registrata.";
            }

            var catalog = CatalogService.Instance.LoadAll();
            var byName = catalog.ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);

            var drinkRows = new List<(string Name, double Qty, double Subtotal)>();
            var foodRows = new List<(string Name, double Qty, double Subtotal)>();
            var otherRows = new List<(string Name, double Qty)>();

            double drinkSubtotal = 0;
            double foodSubtotal = 0;

            foreach (var kvp in agg.Quantities.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (byName.TryGetValue(kvp.Key, out var item))
                {
                    var subtotal = kvp.Value * item.Price;
                    if (item.Category == MenuCategory.Drink)
                    {
                        drinkRows.Add((kvp.Key, kvp.Value, subtotal));
                        drinkSubtotal += subtotal;
                    }
                    else
                    {
                        foodRows.Add((kvp.Key, kvp.Value, subtotal));
                        foodSubtotal += subtotal;
                    }
                }
                else
                {
                    otherRows.Add((kvp.Key, kvp.Value));
                }
            }

            var sb = new System.Text.StringBuilder();

            if (drinkRows.Count > 0)
            {
                sb.AppendLine("DRINK");
                foreach (var r in drinkRows)
                {
                    sb.AppendLine($"  {r.Name}: {r.Qty:0.##}   {r.Subtotal:0.00} €");
                }
                sb.AppendLine($"  Subtotale DRINK: {drinkSubtotal:0.00} €");
                sb.AppendLine();
            }

            if (foodRows.Count > 0)
            {
                sb.AppendLine("FOOD");
                foreach (var r in foodRows)
                {
                    sb.AppendLine($"  {r.Name}: {r.Qty:0.##}   {r.Subtotal:0.00} €");
                }
                sb.AppendLine($"  Subtotale FOOD:  {foodSubtotal:0.00} €");
                sb.AppendLine();
            }

            if (otherRows.Count > 0)
            {
                sb.AppendLine("ALTRI (non più in catalogo)");
                foreach (var r in otherRows)
                {
                    sb.AppendLine($"  {r.Name}: {r.Qty:0.##}");
                }
                sb.AppendLine();
            }

            sb.AppendLine($"TOTALE: {agg.Total:0.00} €");

            return sb.ToString();
        }

        private sealed record Aggregated(IReadOnlyDictionary<string, double> Quantities, double Total);
    }
}
