using System;
using CommunityToolkit.Mvvm.Input;
using IndustrySim.Core.Markets;

namespace IndustrySim.UI.ViewModels;

public class MarketOfferViewModel : ViewModelBase
{
    private readonly MarketOffer _offer;

    // Display properties
    public string Type         => _offer.Type == OfferType.Buy ? "Buy" : "Sell";
    public string Resource     => _offer.ResourceName;
    public string Quantity     => _offer.Quantity.ToString("N0");
    public string PricePerUnit => $"${_offer.PricePerUnit:N2}";
    public string Total        => $"${_offer.TotalPrice:N2}";
    public string ExpiresIn    => $"{_offer.TurnsRemaining} turns";

    // Raw values used for sorting
    public double  QuantityValue     => _offer.Quantity;
    public decimal PricePerUnitValue => _offer.PricePerUnit;
    public decimal TotalValue        => _offer.TotalPrice;
    public int     TurnsLeft         => _offer.TurnsRemaining;

    public bool CanFulfill { get; }

    public IRelayCommand AcceptCommand { get; }

    public MarketOfferViewModel(MarketOffer offer, bool canFulfill, Action<MarketOffer> onAccept)
    {
        _offer    = offer;
        CanFulfill = canFulfill;
        AcceptCommand = new RelayCommand(() => onAccept(offer));
    }
}
