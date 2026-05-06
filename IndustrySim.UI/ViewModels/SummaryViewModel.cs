using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using IndustrySim.Core.Game;
using IndustrySim.Core.Industries;
using IndustrySim.Core.Markets;

namespace IndustrySim.UI.ViewModels;

public class SummaryViewModel : ViewModelBase
{
    public ObservableCollection<ProductSummaryViewModel> Products { get; } = [];
    public ObservableCollection<FinanceSummaryViewModel> Finances { get; } = [];

    public void Refresh(Player player)
    {
        RefreshProducts(player);
        RefreshFinances(player);
    }

    private void RefreshProducts(Player player)
    {
        var produced    = new Dictionary<string, double>();
        var industryUse = new Dictionary<string, double>();
        var delivered   = new Dictionary<string, double>(); // Buy contracts: player → market
        var received    = new Dictionary<string, double>(); // Sell contracts: market → player

        foreach (var industry in player.Industries)
        {
            if (industry is MineBase mine && !mine.IsOpen) continue;
            if (industry.IsSuspended) continue;

            foreach (var output in industry.OutputsProduced)
                produced[output.Name] = produced.GetValueOrDefault(output.Name) + output.Quantity;

            foreach (var input in industry.InputsRequired)
                industryUse[input.Name] = industryUse.GetValueOrDefault(input.Name) + input.Quantity;
        }

        foreach (var contract in player.ActiveContracts)
        {
            if (contract.Type == OfferType.Buy) // player delivers resources
                delivered[contract.ResourceName] =
                    delivered.GetValueOrDefault(contract.ResourceName) + contract.QuantityPerTurn;
            else // Sell: market delivers resources to player
                received[contract.ResourceName] =
                    received.GetValueOrDefault(contract.ResourceName) + contract.QuantityPerTurn;
        }

        var allResources = produced.Keys
            .Union(industryUse.Keys)
            .Union(delivered.Keys)
            .Union(received.Keys)
            .OrderBy(r => r);

        Products.Clear();
        foreach (var resource in allResources)
            Products.Add(new ProductSummaryViewModel(
                resource,
                produced.GetValueOrDefault(resource),
                industryUse.GetValueOrDefault(resource),
                delivered.GetValueOrDefault(resource),
                received.GetValueOrDefault(resource)));
    }

    private void RefreshFinances(Player player)
    {
        var contractIncome = player.ActiveContracts
            .Where(c => c.Type == OfferType.Buy)
            .Sum(c => c.TotalPerTurn);

        var runningCosts = player.Industries
            .Where(i => i is not MineBase mine || mine.IsOpen)
            .Sum(i => i.IsSuspended ? i.RunningCost * 0.1m : i.RunningCost);

        var contractPayments = player.ActiveContracts
            .Where(c => c.Type == OfferType.Sell)
            .Sum(c => c.TotalPerTurn);

        var net = contractIncome - runningCosts - contractPayments;

        Finances.Clear();
        Finances.Add(new FinanceSummaryViewModel("Income from contracts", contractIncome));
        Finances.Add(new FinanceSummaryViewModel("Running costs",        -runningCosts));
        Finances.Add(new FinanceSummaryViewModel("Contract payments",    -contractPayments));
        Finances.Add(new FinanceSummaryViewModel("Net / turn",            net, isTotal: true));
    }

}
