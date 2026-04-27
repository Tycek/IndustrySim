using IndustrySim.Core.Industries;
using IndustrySim.Core.Markets;
using IndustrySim.Core.Models;

namespace IndustrySim.Core.AiCompanies;

/// <summary>
/// An AI-controlled competitor company. Each turn it runs its industries, executes
/// contracts bilaterally, and actively participates in the shared market.
/// </summary>
public class AiCompany : IMarketParticipant
{
    public string Name { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public List<IIndustry> Industries { get; set; } = [];
    public Dictionary<string, double> Inventory { get; set; } = [];
    public List<Contract> ActiveContracts { get; set; } = [];

    public void AddToInventory(Resource resource) =>
        Inventory[resource.Name] = Inventory.GetValueOrDefault(resource.Name) + resource.Quantity;

    /// <summary>
    /// Advances the company by one turn. All participants are passed in so that bilateral
    /// offer and contract settlement can debit/credit counterparties directly.
    /// Returns the names of mines that depleted this turn.
    /// </summary>
    public List<string> ProcessTurn(
        Market market, Random rng,
        IReadOnlyDictionary<string, IMarketParticipant> participants)
    {
        // Mines produce (no inputs needed).
        var openMines = Industries.OfType<MineBase>().Where(m => m.IsOpen).ToList();
        foreach (var mine in openMines)
            foreach (var resource in mine.Process(Inventory))
                AddToInventory(resource);

        var depletedThisTurn = openMines.Where(m => !m.IsOpen).Select(m => m.Name).ToList();
        Industries.RemoveAll(i => i is MineBase mine && !mine.IsOpen);

        // Processing industries consume inputs then produce.
        foreach (var industry in Industries.Where(i => i is not MineBase))
        {
            var produced = industry.Process(Inventory);
            if (produced.Count == 0) continue;

            foreach (var input in industry.InputsRequired)
                Inventory[input.Name] = Inventory.GetValueOrDefault(input.Name) - input.Quantity;

            foreach (var resource in produced)
                AddToInventory(resource);
        }

        // Execute active contracts (bilateral where source is a named participant).
        foreach (var contract in ActiveContracts.ToList())
        {
            // Mirror contracts are tracked here for display; their execution happens on the other side.
            if (contract.IsCounterpartyView)
            {
                contract.TurnsRemaining--;
                if (contract.TurnsRemaining <= 0)
                    ActiveContracts.Remove(contract);
                continue;
            }

            participants.TryGetValue(contract.Source, out var counterparty);
            bool isBilateral = contract.Source != "Market" && counterparty != null;

            bool fulfilled;
            if (contract.Type == OfferType.Sell) // we pay, receive resources
            {
                var counterpartyHas = !isBilateral ||
                    counterparty!.Inventory.GetValueOrDefault(contract.ResourceName) >= contract.QuantityPerTurn;

                fulfilled = Balance >= contract.TotalPerTurn && counterpartyHas;
                if (fulfilled)
                {
                    Balance -= contract.TotalPerTurn;
                    if (isBilateral)
                    {
                        counterparty!.Balance += contract.TotalPerTurn;
                        counterparty.Inventory[contract.ResourceName] =
                            counterparty.Inventory.GetValueOrDefault(contract.ResourceName) - contract.QuantityPerTurn;
                    }
                    AddToInventory(new Resource(contract.ResourceName, contract.QuantityPerTurn));
                }
            }
            else // Buy — we deliver resources, receive payment
            {
                var available = Inventory.GetValueOrDefault(contract.ResourceName);
                var counterpartyCanPay = !isBilateral ||
                    counterparty!.Balance >= contract.TotalPerTurn;

                fulfilled = available >= contract.QuantityPerTurn && counterpartyCanPay;
                if (fulfilled)
                {
                    Inventory[contract.ResourceName] = available - contract.QuantityPerTurn;
                    if (isBilateral)
                    {
                        counterparty!.AddToInventory(new Resource(contract.ResourceName, contract.QuantityPerTurn));
                        counterparty.Balance -= contract.TotalPerTurn;
                    }
                    Balance += contract.TotalPerTurn;
                }
            }

            if (!fulfilled)
            {
                contract.Strikes++;
                if (contract.Strikes >= 3)
                {
                    Balance -= contract.CancellationPenalty;
                    RemoveCounterpartyMirror(contract, participants);
                    ActiveContracts.Remove(contract);
                    continue;
                }
            }

            contract.TurnsRemaining--;
            if (contract.TurnsRemaining <= 0)
            {
                RemoveCounterpartyMirror(contract, participants);
                ActiveContracts.Remove(contract);
            }
        }

        // Deduct running costs; closed mines cost nothing.
        foreach (var industry in Industries)
        {
            if (industry is MineBase mine && !mine.IsOpen) continue;
            Balance -= industry.RunningCost;
        }

        // Market participation.
        var surplus = ComputeNetSurplus();
        AcceptOffers(market, rng, surplus, participants);
        AcceptContracts(market, rng, surplus, participants);
        PostToMarket(market, rng, surplus);

        return depletedThisTurn;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void RemoveCounterpartyMirror(
        Contract contract, IReadOnlyDictionary<string, IMarketParticipant> participants)
    {
        if (contract.Source == "Market") return;
        if (participants.TryGetValue(contract.Source, out var counterparty))
            counterparty.ActiveContracts.RemoveAll(c => c.OriginalContractId == contract.Id);
    }

    /// <summary>
    /// Net units per turn per resource: production minus internal consumption minus
    /// quantities committed to active Buy contracts (where we must deliver).
    /// Positive = surplus; negative = deficit.
    /// </summary>
    private Dictionary<string, double> ComputeNetSurplus()
    {
        var surplus = new Dictionary<string, double>();

        foreach (var industry in Industries)
        {
            if (industry is MineBase mine && !mine.IsOpen) continue;
            foreach (var output in industry.OutputsProduced)
                surplus[output.Name] = surplus.GetValueOrDefault(output.Name) + output.Quantity;
        }

        foreach (var industry in Industries.Where(i => i is not MineBase))
            foreach (var input in industry.InputsRequired)
                surplus[input.Name] = surplus.GetValueOrDefault(input.Name) - input.Quantity;

        foreach (var contract in ActiveContracts.Where(c => !c.IsCounterpartyView && c.Type == OfferType.Buy))
            surplus[contract.ResourceName] = surplus.GetValueOrDefault(contract.ResourceName) - contract.QuantityPerTurn;

        return surplus;
    }

    // ── Market participation ──────────────────────────────────────────────────

    private void AcceptOffers(
        Market market, Random rng,
        Dictionary<string, double> surplus,
        IReadOnlyDictionary<string, IMarketParticipant> participants)
    {
        // Accept Sell offers (we buy resources to cover a deficit).
        foreach (var offer in market.Offers
            .Where(o => o.Type == OfferType.Sell && o.Source != Name).ToList())
        {
            var net = surplus.GetValueOrDefault(offer.ResourceName);
            if (net >= 0) continue;
            var basePrice = Market.BasePrices.GetValueOrDefault(offer.ResourceName, 10m);
            if (offer.PricePerUnit > basePrice * 1.15m) continue;
            if (Balance < offer.TotalPrice) continue;

            Balance -= offer.TotalPrice;
            AddToInventory(new Resource(offer.ResourceName, offer.Quantity));

            // Credit the poster (their resources were pre-committed; now they receive payment).
            if (offer.Source != "Market" && participants.TryGetValue(offer.Source, out var poster))
                poster.Balance += offer.TotalPrice;

            market.Offers.Remove(offer);
            surplus[offer.ResourceName] = net + offer.Quantity;
        }

        // Accept Buy offers (we sell our surplus resources).
        foreach (var offer in market.Offers
            .Where(o => o.Type == OfferType.Buy && o.Source != Name).ToList())
        {
            var net = surplus.GetValueOrDefault(offer.ResourceName);
            if (net <= 0) continue;
            var basePrice = Market.BasePrices.GetValueOrDefault(offer.ResourceName, 10m);
            if (offer.PricePerUnit < basePrice * 0.85m) continue;
            var inInventory = Inventory.GetValueOrDefault(offer.ResourceName);
            if (inInventory < offer.Quantity) continue;

            Inventory[offer.ResourceName] = inInventory - offer.Quantity;
            Balance += offer.TotalPrice;

            // Credit the poster (their money was pre-committed; now they receive the resources).
            if (offer.Source != "Market" && participants.TryGetValue(offer.Source, out var poster))
                poster.AddToInventory(new Resource(offer.ResourceName, offer.Quantity));

            market.Offers.Remove(offer);
            surplus[offer.ResourceName] = net - offer.Quantity;
        }
    }

    private void AcceptContracts(
        Market market, Random rng,
        Dictionary<string, double> surplus,
        IReadOnlyDictionary<string, IMarketParticipant> participants)
    {
        // Accept Buy contracts (we deliver per turn — requires sustained surplus).
        foreach (var contract in market.AvailableContracts
            .Where(c => c.Type == OfferType.Buy && c.Source != Name).ToList())
        {
            var net = surplus.GetValueOrDefault(contract.ResourceName);
            if (net < contract.QuantityPerTurn) continue;
            var basePrice = Market.BasePrices.GetValueOrDefault(contract.ResourceName, 10m);
            if (contract.PricePerUnit < basePrice * 0.85m) continue;

            // For bilateral contracts verify the poster can pay at least the first turn.
            if (contract.Source != "Market" &&
                (!participants.TryGetValue(contract.Source, out var poster) ||
                 poster.Balance < contract.TotalPerTurn)) continue;

            contract.TurnsRemaining = contract.DurationTurns;
            ActiveContracts.Add(contract);
            market.AvailableContracts.Remove(contract);
            surplus[contract.ResourceName] = net - contract.QuantityPerTurn;

            // Give the poster a mirror so they can track their obligation.
            if (contract.Source != "Market" && participants.TryGetValue(contract.Source, out var mirrorTarget))
                mirrorTarget.ActiveContracts.Add(Contract.CreateMirror(contract, Name));
        }

        // Accept Sell contracts (we receive per turn — requires sustained deficit and funds).
        foreach (var contract in market.AvailableContracts
            .Where(c => c.Type == OfferType.Sell && c.Source != Name).ToList())
        {
            var net = surplus.GetValueOrDefault(contract.ResourceName);
            if (net >= 0) continue;
            var basePrice = Market.BasePrices.GetValueOrDefault(contract.ResourceName, 10m);
            if (contract.PricePerUnit > basePrice * 1.15m) continue;
            if (Balance < contract.TotalPerTurn * contract.DurationTurns) continue;

            // Verify the poster can deliver at least the first turn.
            if (contract.Source != "Market" &&
                (!participants.TryGetValue(contract.Source, out var poster) ||
                 poster.Inventory.GetValueOrDefault(contract.ResourceName) < contract.QuantityPerTurn)) continue;

            contract.TurnsRemaining = contract.DurationTurns;
            ActiveContracts.Add(contract);
            market.AvailableContracts.Remove(contract);
            surplus[contract.ResourceName] = net + contract.QuantityPerTurn;

            if (contract.Source != "Market" && participants.TryGetValue(contract.Source, out var mirrorTarget))
                mirrorTarget.ActiveContracts.Add(Contract.CreateMirror(contract, Name));
        }
    }

    /// <summary>
    /// Posts Sell offers (surplus) and Buy offers (deficit) to the shared market.
    /// Resources/money are pre-committed immediately so the poster cannot spend them twice.
    /// </summary>
    private void PostToMarket(Market market, Random rng, Dictionary<string, double> surplus)
    {
        foreach (var (resource, net) in surplus)
        {
            var basePrice = Market.BasePrices.GetValueOrDefault(resource, 10m);

            if (net > 5 && rng.NextDouble() < 0.30)
            {
                var qty = (int)(Math.Round(Math.Min(net, 50) / 5) * 5);
                var inInventory = Inventory.GetValueOrDefault(resource);
                if (qty >= 5 && inInventory >= qty)
                {
                    Inventory[resource] = inInventory - qty; // pre-commit resources
                    market.Offers.Add(new MarketOffer
                    {
                        Type           = OfferType.Sell,
                        ResourceName   = resource,
                        Quantity       = qty,
                        PricePerUnit   = Math.Round(basePrice * (decimal)(0.90 + rng.NextDouble() * 0.15), 2),
                        TurnsRemaining = rng.Next(3, 7),
                        Source         = Name,
                    });
                }
            }
            else if (net < -2 && rng.NextDouble() < 0.25)
            {
                var qty = (int)(Math.Round(Math.Min(-net, 50) / 5) * 5);
                qty = Math.Max(qty, 5);
                var pricePerUnit = Math.Round(basePrice * (decimal)(1.00 + rng.NextDouble() * 0.15), 2);
                var totalCost    = (decimal)qty * pricePerUnit;
                if (Balance >= totalCost)
                {
                    Balance -= totalCost; // pre-commit money
                    market.Offers.Add(new MarketOffer
                    {
                        Type           = OfferType.Buy,
                        ResourceName   = resource,
                        Quantity       = qty,
                        PricePerUnit   = pricePerUnit,
                        TurnsRemaining = rng.Next(3, 7),
                        Source         = Name,
                    });
                }
            }
        }

        // Small chance to post a contract (no pre-commitment for contracts).
        if (rng.NextDouble() >= 0.10) return;

        foreach (var (resource, net) in surplus)
        {
            var basePrice = Market.BasePrices.GetValueOrDefault(resource, 10m);

            if (net > 10)
            {
                var qty = (int)(Math.Round(Math.Min(net, 20) / 5) * 5);
                if (qty >= 5)
                {
                    market.AvailableContracts.Add(new Contract
                    {
                        Type            = OfferType.Buy,
                        ResourceName    = resource,
                        QuantityPerTurn = qty,
                        PricePerUnit    = Math.Round(basePrice * (decimal)(0.90 + rng.NextDouble() * 0.20), 2),
                        DurationTurns   = rng.Next(5, 16),
                        TurnsAvailable  = rng.Next(3, 7),
                        Source          = Name,
                    });
                    return;
                }
            }
            else if (net < -5)
            {
                var qty = (int)(Math.Round(Math.Min(-net, 20) / 5) * 5);
                qty = Math.Max(qty, 5);
                market.AvailableContracts.Add(new Contract
                {
                    Type            = OfferType.Sell,
                    ResourceName    = resource,
                    QuantityPerTurn = qty,
                    PricePerUnit    = Math.Round(basePrice * (decimal)(1.00 + rng.NextDouble() * 0.20), 2),
                    DurationTurns   = rng.Next(5, 16),
                    TurnsAvailable  = rng.Next(3, 7),
                    Source          = Name,
                });
                return;
            }
        }
    }
}
