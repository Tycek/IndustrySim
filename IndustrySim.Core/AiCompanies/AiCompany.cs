using IndustrySim.Core.Game;
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
    /// Only viable processing industries (all inputs covered) contribute outputs — this
    /// mirrors the constraint in ComputeNetBalance and prevents phantom surpluses from
    /// industries that cannot actually run.
    /// </summary>
    private Dictionary<string, double> ComputeNetSurplus()
    {
        var surplus = new Dictionary<string, double>();

        // Mines: always viable; use capacity-adjusted output.
        foreach (var mine in Industries.OfType<MineBase>().Where(m => m.IsOpen))
            foreach (var output in mine.OutputsProduced)
                surplus[output.Name] = surplus.GetValueOrDefault(output.Name)
                    + Math.Min(output.Quantity, mine.Capacity);

        // Processing industries: run the viability-gated fixed-point to add outputs only
        // when all inputs are covered by production. Industries that never become viable
        // remain in `pending` and have their inputs subtracted below to signal demand.
        var pending = Industries.Where(i => i is not MineBase).ToList();
        bool progress;
        do
        {
            progress = false;
            foreach (var industry in pending.ToList())
            {
                if (!AiCompanyGenerator.IsViable(industry, surplus)) continue;

                foreach (var input in industry.InputsRequired)
                    surplus[input.Name] = surplus.GetValueOrDefault(input.Name) - input.Quantity;
                foreach (var output in industry.OutputsProduced)
                    surplus[output.Name] = surplus.GetValueOrDefault(output.Name) + output.Quantity;

                pending.Remove(industry);
                progress = true;
            }
        }
        while (progress);

        // Non-viable industries cannot contribute outputs (no phantom surplus), but they
        // still represent real ongoing demand for their inputs — they consume from inventory
        // each turn until it runs out.
        foreach (var industry in pending)
            foreach (var input in industry.InputsRequired)
                surplus[input.Name] = surplus.GetValueOrDefault(input.Name) - input.Quantity;

        // Buy contracts: AI must deliver resources each turn — outflow.
        foreach (var contract in ActiveContracts.Where(c => !c.IsCounterpartyView && c.Type == OfferType.Buy))
            surplus[contract.ResourceName] = surplus.GetValueOrDefault(contract.ResourceName) - contract.QuantityPerTurn;

        // Sell contracts: AI receives resources each turn — inflow.
        foreach (var contract in ActiveContracts.Where(c => !c.IsCounterpartyView && c.Type == OfferType.Sell))
            surplus[contract.ResourceName] = surplus.GetValueOrDefault(contract.ResourceName) + contract.QuantityPerTurn;

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
            if (StockpileCoversDeficit(offer.ResourceName, net)) continue;
            var basePrice = market.PriceIndex.GetValueOrDefault(offer.ResourceName, 10m);
            if (offer.PricePerUnit > basePrice * 1.15m) continue;
            if (Balance < offer.TotalPrice) continue;

            Balance -= offer.TotalPrice;
            AddToInventory(new Resource(offer.ResourceName, offer.Quantity));

            // Credit the poster (their resources were pre-committed; now they receive payment).
            if (offer.Source != "Market" && participants.TryGetValue(offer.Source, out var poster))
                poster.Balance += offer.TotalPrice;

            market.Offers.Remove(offer);
            // Cap at 0: a one-time purchase fills inventory but doesn't create a production surplus.
            // Without this, buying 10 coal coke to cover a -1/turn deficit would show net=+9
            // and PostToMarket would immediately re-sell the purchased stock.
            surplus[offer.ResourceName] = Math.Min(0.0, net + offer.Quantity);
        }

        // Accept Buy offers (we sell our surplus resources).
        foreach (var offer in market.Offers
            .Where(o => o.Type == OfferType.Buy && o.Source != Name).ToList())
        {
            var net         = surplus.GetValueOrDefault(offer.ResourceName);
            var inInventory = Inventory.GetValueOrDefault(offer.ResourceName);
            // Also sell stranded outputs: inventory of a resource that is not an input to any
            // of our own industries (e.g. SteelIngots when the smelter has lost its supply).
            var isNeededAsInput = Industries.Any(i => i.InputsRequired.Any(r => r.Name == offer.ResourceName));
            if (net <= 0 && (inInventory <= 0 || isNeededAsInput)) continue;

            var basePrice = market.PriceIndex.GetValueOrDefault(offer.ResourceName, 10m);
            if (offer.PricePerUnit < basePrice * 0.85m) continue;

            double amountToSell;
            if (offer.Source == "Market")
            {
                // Market buy offers support partial fulfillment.
                amountToSell = Math.Min(inInventory, offer.Quantity);
                if (amountToSell <= 0) continue;
            }
            else
            {
                if (inInventory < offer.Quantity) continue;
                amountToSell = offer.Quantity;
            }

            Inventory[offer.ResourceName] = inInventory - amountToSell;
            Balance += (decimal)amountToSell * offer.PricePerUnit;

            if (offer.Source == "Market")
            {
                market.RecordSaleToMarket(offer.ResourceName, amountToSell);
                // Re-add the offer with reduced quantity so other companies can still sell
                // to the same gap in the same turn (same pattern as player-side TryAcceptOffer).
                market.Offers.Remove(offer);
                offer.Quantity -= amountToSell;
                if (offer.Quantity > 0)
                    market.Offers.Add(offer);
            }
            else
            {
                market.Offers.Remove(offer);
                if (participants.TryGetValue(offer.Source, out var poster))
                    poster.AddToInventory(new Resource(offer.ResourceName, amountToSell));
            }

            // Cap at 0: selling from inventory doesn't create a sustained flow deficit.
            // Without this, selling 10 units when net=1 would set surplus=-9 and cause
            // PostToMarket to immediately post a buy offer for the same resource.
            surplus[offer.ResourceName] = Math.Max(0.0, net - amountToSell);
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
            var basePrice = market.PriceIndex.GetValueOrDefault(contract.ResourceName, 10m);
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
            if (StockpileCoversDeficit(contract.ResourceName, net)) continue;
            var basePrice = market.PriceIndex.GetValueOrDefault(contract.ResourceName, 10m);
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
            var basePrice = market.PriceIndex.GetValueOrDefault(resource, 10m);

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
            else if (net < 0 && !StockpileCoversDeficit(resource, net) && rng.NextDouble() < 0.60)
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
            var basePrice = market.PriceIndex.GetValueOrDefault(resource, 10m);

            if (net > 10)
            {
                // Surplus: AI sells its excess — acceptor pays and receives the resource.
                var qty = (int)(Math.Round(Math.Min(net, 20) / 5) * 5);
                if (qty >= 5)
                {
                    market.AvailableContracts.Add(new Contract
                    {
                        Type            = OfferType.Sell,
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
            else if (net < -5 && !StockpileCoversDeficit(resource, net))
            {
                // Deficit: AI buys what it needs — acceptor delivers and receives payment.
                var qty = (int)(Math.Round(Math.Min(-net, 20) / 5) * 5);
                qty = Math.Max(qty, 5);
                market.AvailableContracts.Add(new Contract
                {
                    Type            = OfferType.Buy,
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

    private const double StockpileBufferTurns = 50.0;

    /// <summary>True when the current inventory covers the per-turn deficit for 50+ turns.</summary>
    private bool StockpileCoversDeficit(string resource, double net) =>
        Inventory.GetValueOrDefault(resource) / (-net) >= StockpileBufferTurns;

    // ── Mid-game industry building ────────────────────────────────────────────

    private const decimal ShortageThreshold  = 1.40m; // price must be 40 % above base to trigger
    private const double  BuildProbability   = 0.10;  // 10 % chance per turn when threshold is met
    private const decimal BuildCostSafety    = 3.0m;  // must hold 3× build cost before committing

    /// <summary>
    /// Considers building a new industry when at least one resource is trading above its
    /// shortage threshold. Uses the same opportunity-scoring logic as the generator so the
    /// decision reflects what the supply chain actually needs, not just what is expensive.
    /// </summary>
    public void ConsiderBuildingIndustry(
        IReadOnlyDictionary<string, decimal> priceIndex,
        IReadOnlyList<AiCompany> allCompanies,
        Player player,
        Random rng)
    {
        var hasShortage = priceIndex.Any(kv =>
            Market.BasePrices.TryGetValue(kv.Key, out var basePrice) && kv.Value >= basePrice * ShortageThreshold);
        if (!hasShortage) return;

        if (rng.NextDouble() >= BuildProbability) return;

        var netBalance = AiCompanyGenerator.ComputeNetBalance(allCompanies, player);

        // Only consider industries whose output revenue exceeds their operating cost at
        // current prices. Input opportunity costs are excluded because AI companies build
        // vertically integrated chains and produce their own inputs.
        var candidates = AiCompanyGenerator.AllFactories
            .Select(f => (Factory: f, Score: AiCompanyGenerator.Score(f, netBalance)))
            .Where(c => ComputeExpectedMargin(c.Factory(), priceIndex) > 0)
            .ToList();

        if (candidates.Count == 0) return;

        var chosen   = AiCompanyGenerator.WeightedPick(candidates, rng);
        var industry = chosen.Factory();

        if (Balance < industry.BuildCost * BuildCostSafety) return;

        Balance -= industry.BuildCost;
        Industries.Add(industry);
    }

    // Revenue from outputs minus running cost only. Input opportunity costs are excluded
    // because AI companies source inputs internally from their own supply chain.
    private static decimal ComputeExpectedMargin(IIndustry industry, IReadOnlyDictionary<string, decimal> priceIndex)
    {
        var revenue = industry.OutputsProduced.Sum(o =>
            (decimal)o.Quantity * priceIndex.GetValueOrDefault(o.Name, 0m));
        return revenue - industry.RunningCost;
    }
}
