namespace IndustrySim.UI.ViewModels;

public class PriceIndexViewModel : ViewModelBase
{
    public string Resource  { get; }
    public string FairPrice { get; }
    public string Trend     { get; }

    public PriceIndexViewModel(string resource, decimal price, decimal previousPrice)
    {
        Resource  = resource;
        FairPrice = $"${price:N2}";
        Trend     = price > previousPrice + 0.005m ? "↑" :
                    price < previousPrice - 0.005m ? "↓" : "→";
    }
}
