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
    private readonly Func<string, decimal> _getPrice;

    [ObservableProperty] private string _selectedType     = "Sell";
    [ObservableProperty] private string _selectedResource = ResourceNames.Coal;
    [ObservableProperty] private string _quantityText     = "5";
    [ObservableProperty] private string _priceText        = string.Empty;
    [ObservableProperty] private string _durationText     = "10";
    [ObservableProperty] private string _errorMessage     = string.Empty;

    public IReadOnlyList<string> TypeOptions     { get; } = ["Sell", "Buy"];
    public IReadOnlyList<string> ResourceOptions { get; } =
    [
        ResourceNames.Coal, ResourceNames.IronOre, ResourceNames.CoalCoke, ResourceNames.SteelIngot,
        ResourceNames.SteelWire, ResourceNames.SteelBar, ResourceNames.SteelPlate,
        ResourceNames.CopperOre, ResourceNames.CopperIngot, ResourceNames.CopperWire, ResourceNames.CopperPipe,
        ResourceNames.CrudeOil, ResourceNames.Gas, ResourceNames.Diesel, ResourceNames.Kerosene,
        ResourceNames.Chemicals, ResourceNames.PlasticResin, ResourceNames.Fertiliser,
        ResourceNames.SyntheticRubber, ResourceNames.Plastics,
    ];

    public NewContractViewModel(Action<OfferType, string, double, decimal, int> onPost, Func<string, decimal> getPrice)
    {
        _onPost   = onPost;
        _getPrice = getPrice;
        PriceText = _getPrice(SelectedResource).ToString("F2", CultureInfo.InvariantCulture);
    }

    partial void OnSelectedResourceChanged(string value) =>
        PriceText = _getPrice(value).ToString("F2", CultureInfo.InvariantCulture);

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
