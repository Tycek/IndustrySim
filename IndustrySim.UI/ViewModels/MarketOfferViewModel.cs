using System;
using CommunityToolkit.Mvvm.Input;
using IndustrySim.Core.Markets;

namespace IndustrySim.UI.ViewModels;

public class MarketOfferViewModel : ViewModelBase
{
    private readonly MarketOffer _offer;

    public string Type         => _offer.Type == OfferType.Buy ? "Buy" : "Sell";
    public string Resource     => _offer.ResourceName;
    public string Quantity     => _offer.Quantity.ToString("N0");
    public string PricePerUnit => $"${_offer.PricePerUnit:N2}";
    public string Total        => $"${_offer.TotalPrice:N2}";
    public string ExpiresIn    => $"{_offer.TurnsRemaining} turns";
    public string Source       => _offer.Source;

    public double  QuantityValue     => _offer.Quantity;
    public decimal PricePerUnitValue => _offer.PricePerUnit;
    public decimal TotalValue        => _offer.TotalPrice;
    public int     TurnsLeft         => _offer.TurnsRemaining;

    public bool CanFulfill       { get; }
    public bool IsPlayerOwned    { get; }
    /// <summary>True when the offer price is below the current fair-price index for this resource.</summary>
    public bool IsBelowFairPrice { get; }

    public IRelayCommand AcceptCommand  { get; }
    public IRelayCommand CancelCommand  { get; }

    public MarketOfferViewModel(MarketOffer offer, bool canFulfill, bool isPlayerOwned,
        decimal fairPrice,
        Action<MarketOffer> onAccept, Action<MarketOffer> onCancel)
    {
        _offer           = offer;
        CanFulfill       = canFulfill;
        IsPlayerOwned    = isPlayerOwned;
        IsBelowFairPrice = offer.PricePerUnit < fairPrice;
        AcceptCommand    = new RelayCommand(() => onAccept(offer), () => !isPlayerOwned);
        CancelCommand    = new RelayCommand(() => onCancel(offer), () => isPlayerOwned);
    }
}
