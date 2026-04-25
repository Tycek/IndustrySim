using System;
using System.Linq;
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

    public string InputsSummary => _industry.InputsRequired.Count == 0
        ? "—"
        : string.Join(",  ", _industry.InputsRequired.Select(r => $"{r.Name} ×{r.Quantity:N0}"));

    public string OutputsSummary =>
        string.Join(",  ", _industry.OutputsProduced.Select(r => $"{r.Name} ×{r.Quantity:N0}"));

    public IRelayCommand CloseCommand { get; }
}
