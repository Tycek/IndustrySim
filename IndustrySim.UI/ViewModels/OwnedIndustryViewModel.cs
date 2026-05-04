using System;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using IndustrySim.Core.Industries;

namespace IndustrySim.UI.ViewModels;

public class OwnedIndustryViewModel : ViewModelBase
{
    private readonly IIndustry _industry;

    public OwnedIndustryViewModel(IIndustry industry, Action<IIndustry> onClose,
        Action<IIndustry> onSuspend, Action<IIndustry> onResume)
    {
        _industry      = industry;
        CloseCommand   = new RelayCommand(() => onClose(industry),   () => industry is MineBase { IsOpen: false });
        SuspendCommand = new RelayCommand(() => onSuspend(industry), () => !industry.IsSuspended && industry is not MineBase { IsOpen: false });
        ResumeCommand  = new RelayCommand(() => onResume(industry),  () => industry.IsSuspended);
    }

    public string Name => _industry.Name;

    public string RunningCost => _industry.IsSuspended
        ? $"${_industry.RunningCost * 0.1m:N0} (suspended)"
        : $"${_industry.RunningCost:N0}";

    public string Status => _industry switch
    {
        MineBase { IsOpen: false }         => "Depleted",
        MineBase mine when mine.IsSuspended => $"Suspended ({mine.Capacity:N0} left)",
        MineBase mine                       => $"Open ({mine.Capacity:N0} left)",
        { IsSuspended: true }              => "Suspended",
        _                                  => "Active",
    };

    public bool CanSuspend => !_industry.IsSuspended && _industry is not MineBase { IsOpen: false };
    public bool CanResume  => _industry.IsSuspended;
    public bool CanClose   => _industry is MineBase { IsOpen: false };

    public string InputsSummary => _industry.InputsRequired.Count == 0
        ? "—"
        : string.Join(",  ", _industry.InputsRequired.Select(r => $"{r.Name} ×{r.Quantity:N0}"));

    public string OutputsSummary =>
        string.Join(",  ", _industry.OutputsProduced.Select(r => $"{r.Name} ×{r.Quantity:N0}"));

    public IRelayCommand CloseCommand   { get; }
    public IRelayCommand SuspendCommand { get; }
    public IRelayCommand ResumeCommand  { get; }
}
