using System;
using CommunityToolkit.Mvvm.Input;
using IndustrySim.Core.Industries;

namespace IndustrySim.UI.ViewModels;

public class OwnedIndustryViewModel : ViewModelBase
{
    private readonly IIndustry _industry;

    public OwnedIndustryViewModel(IIndustry industry, Action<IIndustry> onClose)
    {
        _industry = industry;
        CloseCommand = new RelayCommand(() => onClose(industry), () => industry is MineBase { IsOpen: false });
    }

    public string Name        => _industry.Name;
    public string RunningCost => $"${_industry.RunningCost:N0}";

    public string Status => _industry is MineBase mine
        ? (mine.IsOpen ? $"Open ({mine.Capacity:N0} left)" : "Depleted")
        : "Active";

    public IRelayCommand CloseCommand { get; }
}
