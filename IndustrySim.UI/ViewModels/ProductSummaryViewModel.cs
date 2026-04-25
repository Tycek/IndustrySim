namespace IndustrySim.UI.ViewModels;

public class ProductSummaryViewModel : ViewModelBase
{
    public string Resource             { get; }
    public string Produced             { get; }
    public string IndustryUse          { get; }
    public string DeliveredToContracts { get; }
    public string ReceivedFromContracts { get; }
    public string Net                  { get; }
    public double NetValue             { get; }

    public ProductSummaryViewModel(
        string resource, double produced, double industryUse,
        double deliveredToContracts, double receivedFromContracts)
    {
        Resource              = resource;
        Produced              = produced              > 0 ? produced.ToString("N0")              : "—";
        IndustryUse           = industryUse           > 0 ? industryUse.ToString("N0")           : "—";
        DeliveredToContracts  = deliveredToContracts  > 0 ? deliveredToContracts.ToString("N0")  : "—";
        ReceivedFromContracts = receivedFromContracts > 0 ? receivedFromContracts.ToString("N0") : "—";
        NetValue = produced + receivedFromContracts - industryUse - deliveredToContracts;
        Net      = NetValue > 0 ? $"+{NetValue:N0}" : NetValue.ToString("N0");
    }
}
