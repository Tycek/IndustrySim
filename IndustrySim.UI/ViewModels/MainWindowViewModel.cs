using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IndustrySim.Core.Game;
using IndustrySim.Core.Industries;
using IndustrySim.Core.Markets;
using IndustrySim.Core.Models;

namespace IndustrySim.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly GameLoop _loop;
    private readonly List<MarketOfferViewModel> _rawMarketOffers = [];

    [ObservableProperty] private string _playerName       = string.Empty;
    [ObservableProperty] private string _balanceText      = string.Empty;
    [ObservableProperty] private string _turnText         = string.Empty;
    [ObservableProperty] private string _notification     = string.Empty;
    [ObservableProperty] private bool   _hasNotification  = false;

    // Market filters
    [ObservableProperty] private string _marketResourceFilter = "All";
    [ObservableProperty] private string _marketTypeFilter     = "All";

    public IReadOnlyList<string> MarketResourceOptions { get; } =
        ["All", ResourceNames.Coal, ResourceNames.IronOre, ResourceNames.CoalCoke, ResourceNames.SteelIngot];

    public IReadOnlyList<string> MarketTypeOptions { get; } = ["All", "Buy", "Sell"];

    public ObservableCollection<OwnedIndustryViewModel>      PlayerIndustries    { get; } = [];
    public ObservableCollection<MarketOfferViewModel>        MarketOffers        { get; } = [];
    public ObservableCollection<StockpileEntryViewModel>     Stockpile           { get; } = [];
    public ObservableCollection<AvailableContractViewModel>  AvailableContracts  { get; } = [];
    public ObservableCollection<ActiveContractViewModel>     ActiveContracts     { get; } = [];
    public IReadOnlyList<CatalogIndustryViewModel>           Catalog             { get; }
    public SummaryViewModel                                  Summary             { get; } = new();
    public SimulationViewModel                               Simulation          { get; } = new();

    public MainWindowViewModel()
    {
        _loop = GameLoop.StartNew("Player 1");

        Catalog =
        [
            new("Coal Mine",        500m, 50m, () => new CoalMine(),        Build),
            new("Iron Ore Mine",    600m, 60m, () => new IronOreMine(),     Build),
            new("Coke Oven",        300m, 30m, () => new CokeOven(),        Build),
            new("Iron Ore Smelter", 800m, 80m, () => new IronOreSmelter(), Build),
        ];

        RefreshState();
    }

    partial void OnMarketResourceFilterChanged(string value) => ApplyMarketFilter();
    partial void OnMarketTypeFilterChanged(string value)     => ApplyMarketFilter();

    private void ApplyMarketFilter()
    {
        IEnumerable<MarketOfferViewModel> view = _rawMarketOffers;

        if (MarketResourceFilter != "All")
            view = view.Where(o => o.Resource == MarketResourceFilter);

        if (MarketTypeFilter != "All")
            view = view.Where(o => o.Type == MarketTypeFilter);

        MarketOffers.Clear();
        foreach (var offer in view)
            MarketOffers.Add(offer);
    }

    private void Build(IIndustry industry)
    {
        _loop.TryBuildIndustry(industry);
        RefreshState();
    }

    private void CloseIndustry(IIndustry industry)
    {
        _loop.RemoveIndustry(industry);
        RefreshState();
    }

    private void AcceptOffer(MarketOffer offer)
    {
        _loop.TryAcceptOffer(offer);
        RefreshState();
    }

    private void AcceptContract(Contract contract)
    {
        _loop.TryAcceptContract(contract);
        RefreshState();
    }

    [RelayCommand]
    private void DismissNotification()
    {
        Notification    = string.Empty;
        HasNotification = false;
    }

    [RelayCommand]
    private void EndTurn()
    {
        var depleted = _loop.ProcessTurn();
        RefreshState();

        if (depleted.Count > 0)
        {
            Notification    = depleted.Count == 1
                ? $"{depleted[0]} has been depleted."
                : string.Join('\n', depleted.Select(n => $"{n} has been depleted."));
            HasNotification = true;
        }
        else
        {
            DismissNotification();
        }
    }

    private void RefreshState()
    {
        PlayerName  = _loop.State.Player.Name;
        BalanceText = $"${_loop.State.Player.Balance:N0}";
        TurnText    = $"Turn {_loop.State.TurnNumber}";

        PlayerIndustries.Clear();
        foreach (var industry in _loop.State.Player.Industries)
            PlayerIndustries.Add(new OwnedIndustryViewModel(industry, CloseIndustry));

        _rawMarketOffers.Clear();
        var player = _loop.State.Player;
        foreach (var offer in _loop.State.Market.Offers)
        {
            var canFulfill = offer.Type == OfferType.Buy
                ? player.Inventory.GetValueOrDefault(offer.ResourceName) >= offer.Quantity
                : player.Balance >= offer.TotalPrice;
            _rawMarketOffers.Add(new MarketOfferViewModel(offer, canFulfill, AcceptOffer));
        }
        ApplyMarketFilter();

        Stockpile.Clear();
        foreach (var (resource, qty) in _loop.State.Player.Inventory.OrderBy(kv => kv.Key).Where(kv => kv.Value > 0))
            Stockpile.Add(new StockpileEntryViewModel(resource, qty));

        AvailableContracts.Clear();
        foreach (var contract in _loop.State.Market.AvailableContracts)
        {
            var canFulfill = contract.Type == OfferType.Buy
                ? player.Inventory.GetValueOrDefault(contract.ResourceName) >= contract.QuantityPerTurn
                : player.Balance >= contract.TotalPerTurn;
            AvailableContracts.Add(new AvailableContractViewModel(contract, canFulfill, AcceptContract));
        }

        ActiveContracts.Clear();
        foreach (var contract in _loop.State.Player.ActiveContracts)
        {
            var canFulfill = contract.Type == OfferType.Buy
                ? player.Inventory.GetValueOrDefault(contract.ResourceName) >= contract.QuantityPerTurn
                : player.Balance >= contract.TotalPerTurn;
            ActiveContracts.Add(new ActiveContractViewModel(contract, canFulfill));
        }

        Summary.Refresh(player);
    }
}
