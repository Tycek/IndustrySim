using System.Collections.Generic;
using System.Linq;
using IndustrySim.Core.AiCompanies;
using IndustrySim.Core.Industries;
using IndustrySim.Core.Markets;

namespace IndustrySim.UI.ViewModels;

public class AiCompanyViewModel : ViewModelBase
{
    public string Name                     { get; }
    public string Balance                  { get; }
    public string IndustryCount            { get; }
    public string IndustrySummary          { get; }
    public string ContractCount            { get; }
    public string ExecutingContractSummary { get; }
    public string PostedContractSummary    { get; }
    public bool   HasPostedContracts       { get; }
    public string StockpileSummary         { get; }

    public AiCompanyViewModel(AiCompany company)
    {
        Name    = company.Name;
        Balance = $"${company.Balance:N0}";

        IndustryCount   = company.Industries.Count.ToString();
        IndustrySummary = company.Industries.Count == 0
            ? "—"
            : string.Join(", ", company.Industries.Select(FormatIndustry));

        // Contracts the AI is executing (accepted from market or another participant).
        var executing = company.ActiveContracts.Where(c => !c.IsCounterpartyView).ToList();
        ContractCount            = executing.Count.ToString();
        ExecutingContractSummary = executing.Count == 0
            ? "—"
            : string.Join(", ", executing.Select(c =>
                $"{(c.Type == OfferType.Buy ? "Sell" : "Buy")} {c.QuantityPerTurn:N0} {c.ResourceName}/turn"));

        // Contracts the AI posted that another participant accepted (mirrors).
        // Source on a mirror is the executor's name.
        var posted = company.ActiveContracts.Where(c => c.IsCounterpartyView).ToList();
        HasPostedContracts    = posted.Count > 0;
        PostedContractSummary = posted.Count == 0
            ? "—"
            : string.Join(", ", posted.Select(c =>
                $"{(c.Type == OfferType.Buy ? "Sell" : "Buy")} {c.QuantityPerTurn:N0} {c.ResourceName}/turn → {c.Source}"));

        var stock = company.Inventory
            .Where(kv => kv.Value > 0)
            .OrderBy(kv => kv.Key)
            .ToList();
        StockpileSummary = stock.Count == 0
            ? "—"
            : string.Join(", ", stock.Select(kv => $"{kv.Key}: {kv.Value:N0}"));
    }

    private static string FormatIndustry(IIndustry industry) =>
        industry is MineBase mine
            ? mine.IsOpen ? $"{mine.Name} ({mine.Capacity:N0} left)" : $"{mine.Name} (depleted)"
            : industry.Name;
}
