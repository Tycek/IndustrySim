using IndustrySim.Core.AiCompanies;
using IndustrySim.Core.Industries;
using IndustrySim.Core.Markets;
using IndustrySim.Core.Models;

namespace IndustrySim.Core.Game;

/// <summary>
/// Drives the turn-based game loop. Each call to <see cref="ProcessTurn"/> advances
/// the simulation by one turn: industries produce, the market updates, AI acts.
/// </summary>
public class GameLoop
{
    private readonly Random _rng = Random.Shared;

    public GameState State { get; }

    public GameLoop(GameState state) => State = state;

    /// <summary>Creates a new game session with a fresh <see cref="GameState"/>.</summary>
    public static GameLoop StartNew(string playerName, decimal startingBalance = 10_000m)
    {
        var state = new GameState
        {
            Player = new Player { Name = playerName, Balance = startingBalance }
        };

        var initialCount = Random.Shared.Next(2, 7); // 2–4 companies at game start
        state.AiCompanies.AddRange(
            AiCompanyGenerator.GenerateInitial(initialCount, Random.Shared, state.Player));

        return new GameLoop(state);
    }

    /// <summary>Removes a depleted or unwanted industry from the player's list.</summary>
    public void RemoveIndustry(IIndustry industry) =>
        State.Player.Industries.Remove(industry);

    // ── Player market actions ────────────────────────────────────────────────

    /// <summary>
    /// Posts a one-time offer on behalf of the player.
    /// Sell offers pre-commit the resources; Buy offers pre-commit the money.
    /// Returns false if the player lacks the resources or funds.
    /// </summary>
    public bool TryPostOffer(OfferType type, string resourceName, double quantity, decimal pricePerUnit, int turnsRemaining)
    {
        var totalPrice = (decimal)quantity * pricePerUnit;

        if (type == OfferType.Sell)
        {
            var available = State.Player.Inventory.GetValueOrDefault(resourceName);
            if (available < quantity) return false;
            State.Player.Inventory[resourceName] = available - quantity;
        }
        else
        {
            if (State.Player.Balance < totalPrice) return false;
            State.Player.Balance -= totalPrice;
        }

        State.Market.Offers.Add(new MarketOffer
        {
            Type           = type,
            ResourceName   = resourceName,
            Quantity       = quantity,
            PricePerUnit   = pricePerUnit,
            TurnsRemaining = turnsRemaining,
            Source         = State.Player.Name,
        });
        return true;
    }

    /// <summary>
    /// Cancels a player-owned offer and refunds its pre-committed resources or money.
    /// Returns false if the offer is not currently on the market.
    /// </summary>
    public bool TryCancelOffer(MarketOffer offer)
    {
        if (!State.Market.Offers.Remove(offer)) return false;

        if (offer.Source == State.Player.Name)
        {
            if (offer.Type == OfferType.Sell)
                State.Player.AddToInventory(new Resource(offer.ResourceName, offer.Quantity));
            else
                State.Player.Balance += offer.TotalPrice;
        }
        return true;
    }

    /// <summary>
    /// Posts a contract offer on behalf of the player. No pre-commitment — the contract
    /// is merely listed on the market until accepted or expired.
    /// </summary>
    public void PostContract(OfferType type, string resourceName, double quantityPerTurn,
        decimal pricePerUnit, int durationTurns, int turnsAvailable = 5)
    {
        State.Market.AvailableContracts.Add(new Contract
        {
            Type            = type,
            ResourceName    = resourceName,
            QuantityPerTurn = quantityPerTurn,
            PricePerUnit    = pricePerUnit,
            DurationTurns   = durationTurns,
            TurnsAvailable  = turnsAvailable,
            Source          = State.Player.Name,
        });
    }

    /// <summary>
    /// Withdraws a player-posted contract from the available list.
    /// Returns false if it is no longer listed (already accepted or expired).
    /// </summary>
    public bool TryCancelContract(Contract contract) =>
        State.Market.AvailableContracts.Remove(contract);

    /// <summary>
    /// Accepts a contract offer. Moves it from the market's available list to the player's
    /// active contracts and resets <see cref="Contract.TurnsRemaining"/> to the full duration.
    /// For bilateral contracts (posted by a named participant) the poster receives a mirror
    /// contract so they can track their obligation.
    /// Returns false if the contract is no longer available or the poster cannot cover the first turn.
    /// </summary>
    public bool TryAcceptContract(Contract contract)
    {
        if (!State.Market.AvailableContracts.Remove(contract))
            return false;

        var participants = BuildParticipantsLookup();

        // Verify the poster can actually fulfil the first turn before committing.
        if (contract.Source != "Market" && participants.TryGetValue(contract.Source, out var poster))
        {
            if (contract.Type == OfferType.Sell &&
                poster.Inventory.GetValueOrDefault(contract.ResourceName) < contract.QuantityPerTurn)
            {
                State.Market.AvailableContracts.Add(contract);
                return false;
            }
            if (contract.Type == OfferType.Buy && poster.Balance < contract.TotalPerTurn)
            {
                State.Market.AvailableContracts.Add(contract);
                return false;
            }

            poster.ActiveContracts.Add(Contract.CreateMirror(contract, State.Player.Name));
        }

        contract.TurnsRemaining = contract.DurationTurns;
        State.Player.ActiveContracts.Add(contract);
        return true;
    }

    /// <summary>
    /// Accepts a market offer on behalf of the player.
    /// Sell offers (poster/market sells): player pays and receives the resource.
    /// Buy offers (poster/market buys): player loses the resource and receives payment.
    /// For bilateral offers, credits the poster's balance or inventory accordingly.
    /// Returns false if the offer no longer exists or the player cannot fulfil it.
    /// </summary>
    public bool TryAcceptOffer(MarketOffer offer)
    {
        if (!State.Market.Offers.Remove(offer))
            return false;

        var participants = BuildParticipantsLookup();
        var isBilateral  = offer.Source != "Market" && participants.TryGetValue(offer.Source, out var poster);

        if (offer.Type == OfferType.Sell) // poster sells, player buys
        {
            if (State.Player.Balance < offer.TotalPrice)
            {
                State.Market.Offers.Add(offer);
                return false;
            }
            State.Player.Balance -= offer.TotalPrice;
            State.Player.AddToInventory(new Resource(offer.ResourceName, offer.Quantity));
            // Resources were pre-committed at post time; poster now receives the payment.
            if (isBilateral) participants[offer.Source].Balance += offer.TotalPrice;
        }
        else // Buy — poster buys from player
        {
            var available = State.Player.Inventory.GetValueOrDefault(offer.ResourceName);
            if (available < offer.Quantity)
            {
                State.Market.Offers.Add(offer);
                return false;
            }
            State.Player.Inventory[offer.ResourceName] = available - offer.Quantity;
            State.Player.Balance += offer.TotalPrice;
            // Money was pre-committed at post time; poster now receives the resources.
            if (isBilateral) participants[offer.Source].AddToInventory(new Resource(offer.ResourceName, offer.Quantity));
        }

        return true;
    }

    /// <summary>
    /// Builds an industry for the player. Deducts <see cref="IIndustry.BuildCost"/> from
    /// the player's balance. Returns false if the player cannot afford it.
    /// </summary>
    public bool TryBuildIndustry(IIndustry industry)
    {
        if (State.Player.Balance < industry.BuildCost)
            return false;

        State.Player.Balance -= industry.BuildCost;
        State.Player.Industries.Add(industry);
        return true;
    }

    // ── Turn processing ──────────────────────────────────────────────────────

    /// <summary>
    /// Advances the game by one turn.
    /// Returns events that occurred (depleted mines, cancelled contracts).
    /// </summary>
    public TurnEvents ProcessTurn()
    {
        State.TurnNumber++;

        var participants = BuildParticipantsLookup();

        // Each AI company runs its industries, executes contracts, and participates in the market.
        var aiDepletedMines = new List<string>();
        foreach (var company in State.AiCompanies)
            aiDepletedMines.AddRange(company.ProcessTurn(State.Market, _rng, participants));

        // AI companies consider building new industries based on current price signals.
        foreach (var company in State.AiCompanies)
            company.ConsiderBuildingIndustry(State.Market.PriceIndex, State.AiCompanies, State.Player, _rng);

        // Every 10 turns there is a 30 % chance a new competitor enters the market.
        var newAiCompanies = new List<string>();
        if (State.TurnNumber % 10 == 0 && _rng.NextDouble() < 0.30)
        {
            var newCompany = AiCompanyGenerator.TryGenerateDynamic(State.AiCompanies, State.Player, _rng);
            if (newCompany != null)
            {
                State.AiCompanies.Add(newCompany);
                newAiCompanies.Add(newCompany.Name);
            }
        }

        // Mines produce first (no inputs needed).
        var openMines = State.Player.Industries.OfType<MineBase>().Where(m => m.IsOpen).ToList();
        foreach (var mine in openMines)
        {
            foreach (var resource in mine.Process(State.Player.Inventory))
                State.Player.AddToInventory(resource);
        }

        var depletedThisTurn = openMines.Where(m => !m.IsOpen).Select(m => m.Name).ToList();

        // Processing industries consume from inventory then add their outputs.
        foreach (var industry in State.Player.Industries.Where(i => i is not MineBase))
        {
            var produced = industry.Process(State.Player.Inventory);
            if (produced.Count == 0) continue;

            foreach (var input in industry.InputsRequired)
                State.Player.Inventory[input.Name] =
                    State.Player.Inventory.GetValueOrDefault(input.Name) - input.Quantity;

            foreach (var output in produced)
                State.Player.AddToInventory(output);
        }

        // Execute active contracts with full bilateral settlement.
        var cancelledContracts = new List<string>();
        foreach (var contract in State.Player.ActiveContracts.ToList())
        {
            // Mirror contracts are display-only; just tick them down.
            if (contract.IsCounterpartyView)
            {
                contract.TurnsRemaining--;
                if (contract.TurnsRemaining <= 0)
                    State.Player.ActiveContracts.Remove(contract);
                continue;
            }

            participants.TryGetValue(contract.Source, out var counterparty);
            bool isBilateral = contract.Source != "Market" && counterparty != null;

            bool fulfilled;
            if (contract.Type == OfferType.Sell) // poster sells to player — player pays, receives resources
            {
                var counterpartyHas = !isBilateral ||
                    counterparty!.Inventory.GetValueOrDefault(contract.ResourceName) >= contract.QuantityPerTurn;

                fulfilled = State.Player.Balance >= contract.TotalPerTurn && counterpartyHas;
                if (fulfilled)
                {
                    State.Player.Balance -= contract.TotalPerTurn;
                    if (isBilateral)
                    {
                        counterparty!.Balance += contract.TotalPerTurn;
                        counterparty.Inventory[contract.ResourceName] =
                            counterparty.Inventory.GetValueOrDefault(contract.ResourceName) - contract.QuantityPerTurn;
                    }
                    State.Player.AddToInventory(new Resource(contract.ResourceName, contract.QuantityPerTurn));
                }
            }
            else // Buy — player delivers resources, receives payment
            {
                var available = State.Player.Inventory.GetValueOrDefault(contract.ResourceName);
                var counterpartyCanPay = !isBilateral || counterparty!.Balance >= contract.TotalPerTurn;

                fulfilled = available >= contract.QuantityPerTurn && counterpartyCanPay;
                if (fulfilled)
                {
                    State.Player.Inventory[contract.ResourceName] = available - contract.QuantityPerTurn;
                    if (isBilateral)
                    {
                        counterparty!.AddToInventory(new Resource(contract.ResourceName, contract.QuantityPerTurn));
                        counterparty.Balance -= contract.TotalPerTurn;
                    }
                    State.Player.Balance += contract.TotalPerTurn;
                }
            }

            if (!fulfilled)
            {
                contract.Strikes++;
                if (contract.Strikes >= 3)
                {
                    State.Player.Balance -= contract.CancellationPenalty;
                    RemoveCounterpartyMirror(contract, participants);
                    State.Player.ActiveContracts.Remove(contract);
                    cancelledContracts.Add(contract.ResourceName);
                    continue;
                }
            }

            contract.TurnsRemaining--;
            if (contract.TurnsRemaining <= 0)
            {
                RemoveCounterpartyMirror(contract, participants);
                State.Player.ActiveContracts.Remove(contract);
            }
        }

        // Deduct running costs. Closed mines cost nothing.
        foreach (var industry in State.Player.Industries)
        {
            if (industry is MineBase mine && !mine.IsOpen)
                continue;
            State.Player.Balance -= industry.RunningCost;
        }

        // Compute offer/contract pressure and drift the price index.
        var (sellPressure, buyPressure) = ComputeMarketPressure(participants);
        State.Market.AdjustPrices(sellPressure, buyPressure);

        // Expire stale offers, refund pre-committed funds, then generate new offers using
        // the freshly adjusted price index.
        var expiredOffers = State.Market.GenerateOffers(_rng);
        RefundExpiredOffers(expiredOffers, participants);

        var bankruptCompanies = DissolveInsolventAiCompanies(participants);

        return new TurnEvents(depletedThisTurn, cancelledContracts, newAiCompanies, bankruptCompanies);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private IReadOnlyDictionary<string, IMarketParticipant> BuildParticipantsLookup()
    {
        var dict = new Dictionary<string, IMarketParticipant>
        {
            [State.Player.Name] = State.Player
        };
        foreach (var company in State.AiCompanies)
            dict[company.Name] = company;
        return dict;
    }

    private static void RefundExpiredOffers(
        IReadOnlyList<MarketOffer> expired,
        IReadOnlyDictionary<string, IMarketParticipant> participants)
    {
        foreach (var offer in expired)
        {
            if (!participants.TryGetValue(offer.Source, out var poster)) continue;

            if (offer.Type == OfferType.Sell)
                poster.AddToInventory(new Resource(offer.ResourceName, offer.Quantity));
            else
                poster.Balance += offer.TotalPrice;
        }
    }

    private static void RemoveCounterpartyMirror(
        Contract contract, IReadOnlyDictionary<string, IMarketParticipant> participants)
    {
        if (contract.Source == "Market") return;
        if (participants.TryGetValue(contract.Source, out var counterparty))
            counterparty.ActiveContracts.RemoveAll(c => c.OriginalContractId == contract.Id);
    }

    /// <summary>
    /// Dissolves AI companies whose balance has fallen below the insolvency threshold
    /// (the greater of 3× monthly running costs or $500). Cancels all their market
    /// presence and removes any active contracts they hold with other participants.
    /// No penalty is charged to the player or counterparties — the company is simply gone.
    /// </summary>
    /// <summary>
    /// Sums current one-time offer quantities and active Buy-contract quantities per resource
    /// to produce the sell and buy pressure signals used by <see cref="Market.AdjustPrices"/>.
    /// </summary>
    private (Dictionary<string, double> sell, Dictionary<string, double> buy) ComputeMarketPressure(
        IReadOnlyDictionary<string, IMarketParticipant> participants)
    {
        var sell = new Dictionary<string, double>();
        var buy  = new Dictionary<string, double>();

        foreach (var offer in State.Market.Offers)
        {
            if (offer.Type == OfferType.Sell)
                sell[offer.ResourceName] = sell.GetValueOrDefault(offer.ResourceName) + offer.Quantity;
            else
                buy[offer.ResourceName] = buy.GetValueOrDefault(offer.ResourceName) + offer.Quantity;
        }

        foreach (var participant in participants.Values)
            foreach (var contract in participant.ActiveContracts.Where(c => !c.IsCounterpartyView && c.Type == OfferType.Buy))
                buy[contract.ResourceName] = buy.GetValueOrDefault(contract.ResourceName) + contract.QuantityPerTurn;

        return (sell, buy);
    }

    private List<string> DissolveInsolventAiCompanies(
        IReadOnlyDictionary<string, IMarketParticipant> participants)
    {
        var dissolved = new List<string>();

        foreach (var ai in State.AiCompanies.ToList())
        {
            var runningCosts = ai.Industries
                .Where(i => i is not MineBase mine || mine.IsOpen)
                .Sum(i => i.RunningCost);
            var threshold = -Math.Max(runningCosts * 3, 500m);

            if (ai.Balance > threshold) continue;

            // Remove all market offers posted by this AI (pre-committed resources/money forfeited).
            State.Market.Offers.RemoveAll(o => o.Source == ai.Name);

            // Remove all available contracts posted by this AI.
            State.Market.AvailableContracts.RemoveAll(c => c.Source == ai.Name);

            // Remove all active contracts referencing this AI from every other participant.
            // This covers: (a) contracts the AI posted that others accepted (Source == ai.Name),
            // and (b) mirrors the AI holds that belong to the other participant's original contract.
            foreach (var (name, participant) in participants)
            {
                if (name == ai.Name) continue;
                participant.ActiveContracts.RemoveAll(c => c.Source == ai.Name);
            }

            State.AiCompanies.Remove(ai);
            dissolved.Add(ai.Name);
        }

        return dissolved;
    }
}
