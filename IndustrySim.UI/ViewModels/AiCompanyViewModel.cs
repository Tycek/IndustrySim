using System.Collections.Generic;
using System.Linq;
using IndustrySim.Core.AiCompanies;
using IndustrySim.Core.Industries;
using IndustrySim.Core.Markets;

namespace IndustrySim.UI.ViewModels;

public class AiCompanyViewModel : ViewModelBase
{
    public string Name             { get; }
    public string Balance          { get; }
    public string IndustryCount    { get; }
    public string IndustrySummary  { get; }
    public string ContractCount    { get; }
    public string ContractSummary  { get; }

    public AiCompanyViewModel(AiCompany company)
    {
        Name = company.Name;
        Balance = $"${company.Balance:N0}";

        IndustryCount = company.Industries.Count.ToString();
        IndustrySummary = company.Industries.Count == 0
            ? "—"
            : string.Join(", ", company.Industries.Select(FormatIndustry));

        var activeCount = company.ActiveContracts.Count;
        ContractCount = activeCount.ToString();
        ContractSummary = activeCount == 0
            ? "—"
            : string.Join(", ", company.ActiveContracts
                .Select(c => $"{(c.Type == OfferType.Buy ? "Sell" : "Buy")} {c.QuantityPerTurn:N0} {c.ResourceName}/turn"));
    }

    private static string FormatIndustry(IIndustry industry) =>
        industry is MineBase mine
            ? mine.IsOpen ? $"{mine.Name} ({mine.Capacity:N0} left)" : $"{mine.Name} (depleted)"
            : industry.Name;
}
