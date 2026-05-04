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

    [ObservableProperty] private string _playerName      = string.Empty;
    [ObservableProperty] private string _balanceText     = string.Empty;
    [ObservableProperty] private string _turnText        = string.Empty;
    [ObservableProperty] private string _notification    = string.Empty;
    [ObservableProperty] private bool   _hasNotification = false;

    // Market filters
    [ObservableProperty] private string _marketResourceFilter = "All";
    [ObservableProperty] private string _marketTypeFilter     = "All";
    [ObservableProperty] private string _marketSourceFilter   = "All";

    public IReadOnlyList<string> MarketResourceOptions { get; } =
        ["All", ResourceNames.Coal, ResourceNames.IronOre, ResourceNames.CoalCoke, ResourceNames.SteelIngot,
         ResourceNames.SteelWire, ResourceNames.SteelBar, ResourceNames.SteelPlate,
         ResourceNames.CopperOre, ResourceNames.CopperIngot, ResourceNames.CopperWire, ResourceNames.CopperPipe,
         ResourceNames.CrudeOil, ResourceNames.Gas, ResourceNames.Diesel, ResourceNames.Kerosene,
         ResourceNames.Chemicals, ResourceNames.PlasticResin, ResourceNames.Fertiliser,
         ResourceNames.SyntheticRubber, ResourceNames.Plastics];

    public IReadOnlyList<string> MarketTypeOptions   { get; } = ["All", "Buy", "Sell"];
    public IReadOnlyList<string> MarketSourceOptions { get; } = ["All", "Market", "AI Company", "My Offers"];

    public ObservableCollection<OwnedIndustryViewModel>      PlayerIndustries   { get; } = [];
    public ObservableCollection<MarketOfferViewModel>        MarketOffers       { get; } = [];
    public ObservableCollection<StockpileEntryViewModel>     Stockpile          { get; } = [];
    public ObservableCollection<AvailableContractViewModel>  AvailableContracts { get; } = [];
    public ObservableCollection<ActiveContractViewModel>     ActiveContracts    { get; } = [];
    public ObservableCollection<AiCompanyViewModel>          AiCompanies        { get; } = [];
    public IReadOnlyList<CatalogIndustryViewModel>           Catalog            { get; }
    public SummaryViewModel                                  Summary            { get; } = new();
    public SimulationViewModel                               Simulation         { get; } = new();
    public NewOfferViewModel                                 NewOffer           { get; }
    public NewContractViewModel                              NewContract        { get; }

    public MainWindowViewModel()
    {
        _loop = GameLoop.StartNew("Player 1");

        NewOffer    = new NewOfferViewModel(PostOffer);
        NewContract = new NewContractViewModel(PostContract);

        Catalog =
        [
            new("Coal Mine",          500m, 50m, () => new CoalMine(),          Build),
            new("Iron Ore Mine",      600m, 60m, () => new IronOreMine(),       Build),
            new("Coke Oven",          300m, 30m, () => new CokeOven(),          Build),
            new("Iron Ore Smelter",   800m, 80m, () => new IronOreSmelter(),    Build),
            new("Steel Wire Drawer",   600m, 60m, () => new SteelWireDrawer(),   Build),
            new("Steel Bar Roller",    500m, 50m, () => new SteelBarRoller(),    Build),
            new("Steel Plate Mill",    900m, 90m, () => new SteelPlateMill(),    Build),
            new("Copper Mine",         650m,  65m,  () => new CopperMine(),        Build),
            new("Copper Smelter",      750m,  75m,  () => new CopperSmelter(),     Build),
            new("Copper Wire Drawer",  550m,  55m,  () => new CopperWireDrawer(),  Build),
            new("Copper Pipe Foundry", 600m,  60m,  () => new CopperPipeFoundry(), Build),
            new("Oil Well",            700m,  70m,  () => new OilWell(),           Build),
            new("Oil Refinery",        1500m, 150m, () => new OilRefinery(),       Build),
            new("Chemical Plant",      1000m, 100m, () => new ChemicalPlant(),     Build),
            new("Plastic Plant",       800m,  80m,  () => new PlasticPlant(),      Build),
        ];

        RefreshState();
    }

    partial void OnMarketResourceFilterChanged(string value) => ApplyMarketFilter();
    partial void OnMarketTypeFilterChanged(string value)     => ApplyMarketFilter();
    partial void OnMarketSourceFilterChanged(string value)   => ApplyMarketFilter();

    private void ApplyMarketFilter()
    {
        var playerName = _loop.State.Player.Name;
        IEnumerable<MarketOfferViewModel> view = _rawMarketOffers;

        if (MarketResourceFilter != "All")
            view = view.Where(o => o.Resource == MarketResourceFilter);

        if (MarketTypeFilter != "All")
            view = view.Where(o => o.Type == MarketTypeFilter);

        view = MarketSourceFilter switch
        {
            "Market"     => view.Where(o => o.Source == "Market"),
            "AI Company" => view.Where(o => o.Source != "Market" && o.Source != playerName),
            "My Offers"  => view.Where(o => o.Source == playerName),
            _            => view,
        };

        MarketOffers.Clear();
        foreach (var offer in view)
            MarketOffers.Add(offer);
    }

    // ── Player actions ───────────────────────────────────────────────────────

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

    private void CancelOffer(MarketOffer offer)
    {
        _loop.TryCancelOffer(offer);
        RefreshState();
    }

    private bool PostOffer(OfferType type, string resource, double qty, decimal price, int turns)
    {
        var ok = _loop.TryPostOffer(type, resource, qty, price, turns);
        RefreshState();
        return ok;
    }

    private void AcceptContract(Contract contract)
    {
        _loop.TryAcceptContract(contract);
        RefreshState();
    }

    private void CancelContract(Contract contract)
    {
        _loop.TryCancelContract(contract);
        RefreshState();
    }

    private void PostContract(OfferType type, string resource, double qty, decimal price, int duration)
    {
        _loop.PostContract(type, resource, qty, price, duration);
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
        var events = _loop.ProcessTurn();
        RefreshState();

        var lines = new List<string>();

        foreach (var mine in events.DepletedMines)
            lines.Add($"{mine} has been depleted.");

        foreach (var resource in events.CancelledContracts)
            lines.Add($"{resource} contract cancelled after 3 strikes. Penalty deducted.");

        foreach (var company in events.NewAiCompanies)
            lines.Add($"{company} has entered the market as a new competitor.");

        foreach (var company in events.BankruptAiCompanies)
            lines.Add($"{company} has gone bankrupt and left the market.");

        if (lines.Count > 0)
        {
            Notification    = string.Join('\n', lines);
            HasNotification = true;
        }
        else
        {
            DismissNotification();
        }
    }

    private void RefreshState()
    {
        var player     = _loop.State.Player;
        var playerName = player.Name;

        PlayerName  = playerName;
        BalanceText = $"${player.Balance:N0}";
        TurnText    = $"Turn {_loop.State.TurnNumber}";

        PlayerIndustries.Clear();
        foreach (var industry in player.Industries)
            PlayerIndustries.Add(new OwnedIndustryViewModel(industry, CloseIndustry));

        _rawMarketOffers.Clear();
        foreach (var offer in _loop.State.Market.Offers)
        {
            var canFulfill = offer.Type == OfferType.Buy
                ? player.Inventory.GetValueOrDefault(offer.ResourceName) >= offer.Quantity
                : player.Balance >= offer.TotalPrice;
            var isPlayerOwned = offer.Source == playerName;
            var fairPrice     = _loop.State.Market.PriceIndex.GetValueOrDefault(offer.ResourceName, 0m);
            _rawMarketOffers.Add(new MarketOfferViewModel(offer, canFulfill, isPlayerOwned, fairPrice, AcceptOffer, CancelOffer));
        }
        ApplyMarketFilter();

        Stockpile.Clear();
        foreach (var (resource, qty) in player.Inventory.OrderBy(kv => kv.Key).Where(kv => kv.Value > 0))
            Stockpile.Add(new StockpileEntryViewModel(resource, qty));

        AvailableContracts.Clear();
        foreach (var contract in _loop.State.Market.AvailableContracts)
        {
            var canFulfill = contract.Type == OfferType.Buy
                ? player.Inventory.GetValueOrDefault(contract.ResourceName) >= contract.QuantityPerTurn
                : player.Balance >= contract.TotalPerTurn;
            var isPlayerOwned = contract.Source == playerName;
            AvailableContracts.Add(new AvailableContractViewModel(contract, canFulfill, isPlayerOwned, AcceptContract, CancelContract));
        }

        ActiveContracts.Clear();
        foreach (var contract in player.ActiveContracts)
        {
            var canFulfill = contract.Type == OfferType.Buy
                ? player.Inventory.GetValueOrDefault(contract.ResourceName) >= contract.QuantityPerTurn
                : player.Balance >= contract.TotalPerTurn;
            ActiveContracts.Add(new ActiveContractViewModel(contract, canFulfill));
        }

        AiCompanies.Clear();
        foreach (var company in _loop.State.AiCompanies)
            AiCompanies.Add(new AiCompanyViewModel(company));

        Summary.Refresh(player, _loop.State.Market);
    }
}
