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
    public static GameLoop StartNew(string playerName, decimal startingBalance = 10_000m) => new(new GameState
    {
        Player = new Player { Name = playerName, Balance = startingBalance }
    });

    /// <summary>Removes a depleted or unwanted industry from the player's list.</summary>
    public void RemoveIndustry(IIndustry industry) =>
        State.Player.Industries.Remove(industry);

    /// <summary>
    /// Accepts a contract offer. Moves it from the market's available list to the player's
    /// active contracts and resets <see cref="Contract.TurnsRemaining"/> to the full duration.
    /// Returns false if the contract is no longer available.
    /// </summary>
    public bool TryAcceptContract(Contract contract)
    {
        if (!State.Market.AvailableContracts.Remove(contract))
            return false;

        contract.TurnsRemaining = contract.DurationTurns;
        State.Player.ActiveContracts.Add(contract);
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

    /// <summary>
    /// Accepts a market offer on behalf of the player.
    /// Sell offers (market sells): player pays and receives the resource.
    /// Buy offers (market buys): player loses the resource and receives payment.
    /// Returns false if the offer no longer exists or the player cannot fulfil it.
    /// </summary>
    public bool TryAcceptOffer(MarketOffer offer)
    {
        if (!State.Market.Offers.Remove(offer))
            return false;

        if (offer.Type == OfferType.Sell)
        {
            if (State.Player.Balance < offer.TotalPrice)
            {
                State.Market.Offers.Add(offer); // put it back
                return false;
            }

            State.Player.Balance -= offer.TotalPrice;
            State.Player.AddToInventory(new Resource(offer.ResourceName, offer.Quantity));
        }
        else // Buy — market buys from player
        {
            var available = State.Player.Inventory.GetValueOrDefault(offer.ResourceName);
            if (available < offer.Quantity)
            {
                State.Market.Offers.Add(offer); // put it back
                return false;
            }

            State.Player.Inventory[offer.ResourceName] = available - offer.Quantity;
            State.Player.Balance += offer.TotalPrice;
        }

        return true;
    }

    /// <summary>
    /// Advances the game by one turn.
    /// Returns the names of any mines that became depleted this turn.
    /// </summary>
    public IReadOnlyList<string> ProcessTurn()
    {
        State.TurnNumber++;

        // Expire stale offers and add new ones for this turn.
        State.Market.GenerateOffers(_rng);

        // TODO: process AI company turns

        // Mines produce first (no inputs, so order doesn't matter between them).
        // Track which mines were open before production so we can detect new depletions.
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

        // Execute active contracts. Skip (no penalty) if the player cannot fulfil this turn.
        foreach (var contract in State.Player.ActiveContracts.ToList())
        {
            if (contract.Type == OfferType.Sell) // market sells to player — player pays
            {
                if (State.Player.Balance >= contract.TotalPerTurn)
                {
                    State.Player.Balance -= contract.TotalPerTurn;
                    State.Player.AddToInventory(new Resource(contract.ResourceName, contract.QuantityPerTurn));
                }
            }
            else // Buy — market buys from player — player delivers resources
            {
                var available = State.Player.Inventory.GetValueOrDefault(contract.ResourceName);
                if (available >= contract.QuantityPerTurn)
                {
                    State.Player.Inventory[contract.ResourceName] = available - contract.QuantityPerTurn;
                    State.Player.Balance += contract.TotalPerTurn;
                }
            }

            contract.TurnsRemaining--;
        }

        State.Player.ActiveContracts.RemoveAll(c => c.TurnsRemaining <= 0);

        // Deduct running costs. Closed mines (Capacity = 0) are excluded.
        foreach (var industry in State.Player.Industries)
        {
            if (industry is MineBase mine && !mine.IsOpen)
                continue;
            State.Player.Balance -= industry.RunningCost;
        }

        return depletedThisTurn;
    }
}
