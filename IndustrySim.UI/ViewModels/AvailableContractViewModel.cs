using System;
using CommunityToolkit.Mvvm.Input;
using IndustrySim.Core.Markets;

namespace IndustrySim.UI.ViewModels;

public class AvailableContractViewModel : ViewModelBase
{
    private readonly Contract _contract;

    public string Type            => _contract.Type == OfferType.Buy ? "Buy" : "Sell";
    public string Resource        => _contract.ResourceName;
    public string QuantityPerTurn => _contract.QuantityPerTurn.ToString("N0");
    public string PricePerUnit    => $"${_contract.PricePerUnit:N2}";
    public string TotalPerTurn    => $"${_contract.TotalPerTurn:N2}";
    public string Duration        => $"{_contract.DurationTurns} turns";
    public string AvailableFor    => $"{_contract.TurnsAvailable} turns";
    public bool   CanFulfill      { get; }

    public IRelayCommand AcceptCommand { get; }

    public AvailableContractViewModel(Contract contract, bool canFulfill, Action<Contract> onAccept)
    {
        _contract      = contract;
        CanFulfill     = canFulfill;
        AcceptCommand  = new RelayCommand(() => onAccept(contract));
    }
}
