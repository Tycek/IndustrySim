using System;
using CommunityToolkit.Mvvm.Input;
using IndustrySim.Core.Industries;

namespace IndustrySim.UI.ViewModels;

public class CatalogIndustryViewModel : ViewModelBase
{
    public string Name        { get; }
    public string BuildCost   { get; }
    public string RunningCost { get; }
    public IRelayCommand BuildCommand { get; }

    public CatalogIndustryViewModel(
        string name, decimal buildCost, decimal runningCost,
        Func<IIndustry> factory, Action<IIndustry> onBuild)
    {
        Name        = name;
        BuildCost   = $"${buildCost:N0}";
        RunningCost = $"${runningCost:N0}";
        BuildCommand = new RelayCommand(() => onBuild(factory()));
    }
}
