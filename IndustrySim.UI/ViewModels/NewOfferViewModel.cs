using System;
using System.Collections.Generic;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IndustrySim.Core.Markets;
using IndustrySim.Core.Models;

namespace IndustrySim.UI.ViewModels;

public partial class NewOfferViewModel : ViewModelBase
{
    private readonly Func<OfferType, string, double, decimal, int, bool> _onPost;

    [ObservableProperty] private string _selectedType     = "Sell";
    [ObservableProperty] private string _selectedResource = ResourceNames.Coal;
    [ObservableProperty] private string _quantityText     = "10";
    [ObservableProperty] private string _priceText        = "5.00";
    [ObservableProperty] private string _turnsText        = "5";
    [ObservableProperty] private string _errorMessage     = string.Empty;

    public IReadOnlyList<string> TypeOptions     { get; } = ["Sell", "Buy"];
    public IReadOnlyList<string> ResourceOptions { get; } =
        [ResourceNames.Coal, ResourceNames.IronOre, ResourceNames.CoalCoke, ResourceNames.SteelIngot];

    public NewOfferViewModel(Func<OfferType, string, double, decimal, int, bool> onPost) =>
        _onPost = onPost;

    [RelayCommand]
    private void Post()
    {
        ErrorMessage = string.Empty;

        if (!double.TryParse(QuantityText, NumberStyles.Any, CultureInfo.InvariantCulture, out var qty) || qty <= 0)
            { ErrorMessage = "Invalid quantity."; return; }
        if (!decimal.TryParse(PriceText, NumberStyles.Any, CultureInfo.InvariantCulture, out var price) || price <= 0)
            { ErrorMessage = "Invalid price."; return; }
        if (!int.TryParse(TurnsText, out var turns) || turns <= 0)
            { ErrorMessage = "Invalid duration."; return; }

        var type = SelectedType == "Sell" ? OfferType.Sell : OfferType.Buy;
        if (!_onPost(type, SelectedResource, qty, price, turns))
            ErrorMessage = type == OfferType.Sell ? "Insufficient resources." : "Insufficient funds.";
    }
}
