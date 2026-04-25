using System;
using CommunityToolkit.Mvvm.ComponentModel;
using IndustrySim.Core.Industries;

namespace IndustrySim.UI.ViewModels;

public partial class SimulationIndustryOptionViewModel : ViewModelBase
{
    [ObservableProperty] private bool _isSelected;

    public string         Name           { get; }
    public string         BuildCost      { get; }
    public decimal        BuildCostValue { get; }
    public Func<IIndustry> Factory       { get; }

    public SimulationIndustryOptionViewModel(string name, decimal buildCost, Func<IIndustry> factory)
    {
        Name           = name;
        BuildCostValue = buildCost;
        BuildCost      = $"${buildCost:N0}";
        Factory        = factory;
    }
}
