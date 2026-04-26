using System;
using System.Collections.Generic;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IndustrySim.Core.Markets;
using IndustrySim.Core.Models;

namespace IndustrySim.UI.ViewModels;

public partial class NewContractViewModel : ViewModelBase
{
    private readonly Action<OfferType, string, double, decimal, int> _onPost;

    [ObservableProperty] private string _selectedType     = "Sell";
    [ObservableProperty] private string _selectedResource = ResourceNames.Coal;
    [ObservableProperty] private string _quantityText     = "5";
    [ObservableProperty] private string _priceText        = "5.00";
    [ObservableProperty] private string _durationText     = "10";
    [ObservableProperty] private string _errorMessage     = string.Empty;

    public IReadOnlyList<string> TypeOptions     { get; } = ["Sell", "Buy"];
    public IReadOnlyList<string> ResourceOptions { get; } =
        [ResourceNames.Coal, ResourceNames.IronOre, ResourceNames.CoalCoke, ResourceNames.SteelIngot];

    public NewContractViewModel(Action<OfferType, string, double, decimal, int> onPost) =>
        _onPost = onPost;

    [RelayCommand]
    private void Post()
    {
        ErrorMessage = string.Empty;

        if (!double.TryParse(QuantityText, NumberStyles.Any, CultureInfo.InvariantCulture, out var qty) || qty <= 0)
            { ErrorMessage = "Invalid quantity per turn."; return; }
        if (!decimal.TryParse(PriceText, NumberStyles.Any, CultureInfo.InvariantCulture, out var price) || price <= 0)
            { ErrorMessage = "Invalid price."; return; }
        if (!int.TryParse(DurationText, out var duration) || duration <= 0)
            { ErrorMessage = "Invalid duration."; return; }

        var type = SelectedType == "Sell" ? OfferType.Sell : OfferType.Buy;
        _onPost(type, SelectedResource, qty, price, duration);
    }
}
