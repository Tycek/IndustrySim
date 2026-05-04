using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using IndustrySim.Core.Markets;

namespace IndustrySim.UI.ViewModels;

public class MarketIndexViewModel : ViewModelBase
{
    private readonly Dictionary<string, List<decimal>> _history = [];

    public ObservableCollection<PriceIndexViewModel> Rows { get; } = [];

    public void Refresh(Market market)
    {
        foreach (var resource in Market.BasePrices.Keys)
        {
            var price = market.PriceIndex.GetValueOrDefault(resource, Market.BasePrices[resource]);
            if (!_history.TryGetValue(resource, out var hist))
                _history[resource] = hist = [];
            hist.Add(price);
            if (hist.Count > 10) hist.RemoveAt(0);
        }

        Rows.Clear();
        foreach (var resource in Market.BasePrices.Keys.OrderBy(r => r))
        {
            var hist    = _history[resource];
            var current = hist[^1];
            var prev    = hist.Count > 1 ? hist[^2] : current;
            Rows.Add(new PriceIndexViewModel(resource, current, prev, hist.Min(), hist.Max()));
        }
    }
}
