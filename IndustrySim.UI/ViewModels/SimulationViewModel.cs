using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IndustrySim.Core.Industries;
using IndustrySim.Core.Models;
using IndustrySim.Core.Simulation;

namespace IndustrySim.UI.ViewModels;

public partial class SimulationViewModel : ViewModelBase
{
    private readonly SimulationRunner _runner = new();

    [ObservableProperty] private int     _turnCount      = 20;
    [ObservableProperty] private decimal _coalPrice      = 5m;
    [ObservableProperty] private decimal _ironOrePrice   = 8m;
    [ObservableProperty] private decimal _coalCokePrice  = 15m;
    [ObservableProperty] private decimal _steelIngotPrice = 40m;
    [ObservableProperty] private string  _summary        = string.Empty;
    [ObservableProperty] private bool    _hasResults;

    public ObservableCollection<SimulationIndustryOptionViewModel> Industries { get; } =
    [
        new("Coal Mine",        500m, () => new CoalMine()),
        new("Iron Ore Mine",    600m, () => new IronOreMine()),
        new("Coke Oven",        300m, () => new CokeOven()),
        new("Iron Ore Smelter", 800m, () => new IronOreSmelter()),
    ];

    public ObservableCollection<SimulationTurnViewModel> Results { get; } = [];

    [RelayCommand]
    private void RunSimulation()
    {
        var selectedFactories = Industries.Where(i => i.IsSelected).Select(i => i.Factory);

        var prices = new Dictionary<string, decimal>
        {
            [ResourceNames.Coal]       = CoalPrice,
            [ResourceNames.IronOre]    = IronOrePrice,
            [ResourceNames.CoalCoke]   = CoalCokePrice,
            [ResourceNames.SteelIngot] = SteelIngotPrice,
        };

        var result = _runner.Run(selectedFactories, prices, TurnCount);

        Results.Clear();
        foreach (var t in result.Turns)
            Results.Add(new SimulationTurnViewModel(t));

        decimal avgNet       = result.Turns.Count > 0 ? result.NetProfit / result.Turns.Count : 0;
        decimal totalBuild   = Industries.Where(i => i.IsSelected).Sum(i => i.BuildCostValue);
        string  breakEven    = avgNet > 0
            ? $"~{(int)Math.Ceiling(totalBuild / avgNet)} turns to recoup build cost"
            : "never recoups build cost";

        Summary = $"Revenue: ${result.TotalRevenue:N0}   Costs: ${result.TotalRunningCosts:N0}   " +
                  $"Net: ${result.NetProfit:N0}   Avg/turn: ${avgNet:N1}   Break-even: {breakEven}";
        HasResults = true;
    }
}
