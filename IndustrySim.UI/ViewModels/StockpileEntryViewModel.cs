namespace IndustrySim.UI.ViewModels;

public class StockpileEntryViewModel : ViewModelBase
{
    public string Resource { get; }
    public string Quantity { get; }

    public StockpileEntryViewModel(string resource, double quantity)
    {
        Resource = resource;
        Quantity = quantity.ToString("N0");
    }
}
