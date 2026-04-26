using IndustrySim.Core.AiCompanies;
using IndustrySim.Core.Game;
using IndustrySim.Core.Markets;
using IndustrySim.Core.Models;

namespace IndustrySim.Tests;

/// <summary>
/// Tests for bilateral contract acceptance and per-turn execution.
/// ProcessTurn tests use an AI company with no industries so its own
/// turn is a no-op and cannot disturb the assertions.
/// </summary>
public class BilateralContractTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static (GameLoop loop, AiCompany ai) Setup(
        decimal playerBalance = 10_000m, decimal aiBalance = 5_000m)
    {
        var state = new GameState
        {
            Player = new Player { Name = "Player", Balance = playerBalance }
        };
        var loop = new GameLoop(state);
        var ai   = new AiCompany { Name = "Acme", Balance = aiBalance };
        loop.State.AiCompanies.Add(ai);
        return (loop, ai);
    }

    /// <summary>
    /// Contract posted by AI where AI wants to BUY coal (player will be the executor,
    /// delivering coal each turn and receiving money).
    /// </summary>
    private static Contract AiBuyContract(string source = "Acme",
        double qtyPerTurn = 10, decimal price = 5m, int duration = 10)
        => new()
        {
            Type            = OfferType.Buy,
            ResourceName    = ResourceNames.Coal,
            QuantityPerTurn = qtyPerTurn,
            PricePerUnit    = price,
            DurationTurns   = duration,
            TurnsAvailable  = 5,
            Source          = source,
        };

    /// <summary>
    /// Contract posted by AI where AI wants to SELL coal (player will be the executor,
    /// paying each turn and receiving coal).
    /// </summary>
    private static Contract AiSellContract(string source = "Acme",
        double qtyPerTurn = 10, decimal price = 5m, int duration = 10)
        => new()
        {
            Type            = OfferType.Sell,
            ResourceName    = ResourceNames.Coal,
            QuantityPerTurn = qtyPerTurn,
            PricePerUnit    = price,
            DurationTurns   = duration,
            TurnsAvailable  = 5,
            Source          = source,
        };

    // ── TryAcceptContract — acceptance side-effects ───────────────────────────

    [Fact]
    public void AcceptAiBuyContract_ContractAppearsInPlayerActiveContracts()
    {
        var (loop, _) = Setup();
        var contract = AiBuyContract();
        loop.State.Market.AvailableContracts.Add(contract);

        loop.TryAcceptContract(contract);

        Assert.Contains(contract, loop.State.Player.ActiveContracts);
    }

    [Fact]
    public void AcceptAiBuyContract_ContractRemovedFromAvailableList()
    {
        var (loop, _) = Setup();
        var contract = AiBuyContract();
        loop.State.Market.AvailableContracts.Add(contract);

        loop.TryAcceptContract(contract);

        Assert.DoesNotContain(contract, loop.State.Market.AvailableContracts);
    }

    [Fact]
    public void AcceptAiBuyContract_TurnsRemainingSetToDuration()
    {
        var (loop, _) = Setup();
        var contract = AiBuyContract(duration: 8);
        loop.State.Market.AvailableContracts.Add(contract);

        loop.TryAcceptContract(contract);

        Assert.Equal(8, contract.TurnsRemaining);
    }

    [Fact]
    public void AcceptAiBuyContract_MirrorCreatedInAiActiveContracts()
    {
        var (loop, ai) = Setup();
        var contract = AiBuyContract();
        loop.State.Market.AvailableContracts.Add(contract);

        loop.TryAcceptContract(contract);

        Assert.Single(ai.ActiveContracts);
    }

    [Fact]
    public void AcceptAiBuyContract_MirrorIsMarkedAsCounterpartyView()
    {
        var (loop, ai) = Setup();
        var contract = AiBuyContract();
        loop.State.Market.AvailableContracts.Add(contract);

        loop.TryAcceptContract(contract);

        Assert.True(ai.ActiveContracts[0].IsCounterpartyView);
    }

    [Fact]
    public void AcceptAiBuyContract_MirrorLinksBackToOriginalId()
    {
        var (loop, ai) = Setup();
        var contract = AiBuyContract();
        loop.State.Market.AvailableContracts.Add(contract);

        loop.TryAcceptContract(contract);

        Assert.Equal(contract.Id, ai.ActiveContracts[0].OriginalContractId);
    }

    [Fact]
    public void AcceptAiBuyContract_MirrorHasOppositeType()
    {
        // AI posted Buy; from AI's perspective the mirror should appear as Sell (they deliver).
        var (loop, ai) = Setup();
        var contract = AiBuyContract();
        loop.State.Market.AvailableContracts.Add(contract);

        loop.TryAcceptContract(contract);

        Assert.Equal(OfferType.Sell, ai.ActiveContracts[0].Type);
    }

    [Fact]
    public void AcceptAiSellContract_MirrorCreatedInAiActiveContracts()
    {
        var (loop, ai) = Setup();
        // Player accepts the Sell contract → player pays, receives coal; AI delivers
        ai.Inventory[ResourceNames.Coal] = 50; // poster can deliver first turn
        var contract = AiSellContract();
        loop.State.Market.AvailableContracts.Add(contract);

        loop.TryAcceptContract(contract);

        Assert.Single(ai.ActiveContracts);
        Assert.True(ai.ActiveContracts[0].IsCounterpartyView);
        Assert.Equal(contract.Id, ai.ActiveContracts[0].OriginalContractId);
    }

    [Fact]
    public void AcceptAiBuyContract_ReturnsFalseWhenAlreadyAccepted()
    {
        var (loop, _) = Setup();
        var contract = AiBuyContract();
        loop.State.Market.AvailableContracts.Add(contract);

        loop.TryAcceptContract(contract);          // first accept
        var second = loop.TryAcceptContract(contract); // no longer in market

        Assert.False(second);
    }

    // ── Per-turn bilateral contract execution (ProcessTurn) ───────────────────

    [Fact]
    public void BuyContract_PlayerDeliversCoal_AiPays()
    {
        // Player is executor: delivers coal, receives money.
        // AI is poster (has a mirror, just ticks).
        var (loop, ai) = Setup(playerBalance: 10_000m, aiBalance: 5_000m);
        loop.State.Player.Inventory[ResourceNames.Coal] = 50;

        var contract = AiBuyContract(qtyPerTurn: 10, price: 5m); // TotalPerTurn = $50
        contract.TurnsRemaining = 5;
        loop.State.Player.ActiveContracts.Add(contract);

        // Add the mirror on AI's side.
        ai.ActiveContracts.Add(Contract.CreateMirror(contract, "Player"));

        loop.ProcessTurn();

        Assert.Equal(40,       loop.State.Player.Inventory[ResourceNames.Coal]); // -10
        Assert.Equal(10_050m,  loop.State.Player.Balance);                       // +$50
        Assert.Equal(10,       ai.Inventory.GetValueOrDefault(ResourceNames.Coal)); // +10
        Assert.Equal(4_950m,   ai.Balance);                                       // -$50
    }

    [Fact]
    public void SellContract_PlayerPays_AiDeliversCoal()
    {
        // Player is executor on a Sell contract: player pays, receives coal from AI.
        var (loop, ai) = Setup(playerBalance: 10_000m, aiBalance: 5_000m);
        ai.Inventory[ResourceNames.Coal] = 50;

        var contract = AiSellContract(qtyPerTurn: 10, price: 5m); // TotalPerTurn = $50
        contract.TurnsRemaining = 5;
        loop.State.Player.ActiveContracts.Add(contract);

        ai.ActiveContracts.Add(Contract.CreateMirror(contract, "Player"));

        loop.ProcessTurn();

        Assert.Equal(9_950m, loop.State.Player.Balance);                         // -$50
        Assert.Equal(10,     loop.State.Player.Inventory.GetValueOrDefault(ResourceNames.Coal)); // +10
        Assert.Equal(40,     ai.Inventory[ResourceNames.Coal]);                  // -10
        Assert.Equal(5_050m, ai.Balance);                                        // +$50
    }

    [Fact]
    public void BuyContract_AiCannotPay_EarnsStrikeNotDelivery()
    {
        // AI has no money → bilateral check fails → strike, no transfer.
        var (loop, ai) = Setup(playerBalance: 10_000m, aiBalance: 0m);
        loop.State.Player.Inventory[ResourceNames.Coal] = 50;

        var contract = AiBuyContract(qtyPerTurn: 10, price: 5m);
        contract.TurnsRemaining = 5;
        loop.State.Player.ActiveContracts.Add(contract);
        ai.ActiveContracts.Add(Contract.CreateMirror(contract, "Player"));

        loop.ProcessTurn();

        Assert.Equal(1, contract.Strikes);
        Assert.Equal(50, loop.State.Player.Inventory[ResourceNames.Coal]); // no coal delivered
        Assert.Equal(0m, ai.Balance);                                       // AI still broke
        Assert.Equal(10_000m, loop.State.Player.Balance);                  // player not paid
    }

    [Fact]
    public void SellContract_PlayerCannotPay_EarnsStrike()
    {
        var (loop, ai) = Setup(playerBalance: 0m, aiBalance: 5_000m);
        ai.Inventory[ResourceNames.Coal] = 50;

        var contract = AiSellContract(qtyPerTurn: 10, price: 5m);
        contract.TurnsRemaining = 5;
        loop.State.Player.ActiveContracts.Add(contract);
        ai.ActiveContracts.Add(Contract.CreateMirror(contract, "Player"));

        loop.ProcessTurn();

        Assert.Equal(1, contract.Strikes);
        Assert.Equal(50,    ai.Inventory[ResourceNames.Coal]); // coal not delivered
        Assert.Equal(0m,    loop.State.Player.Balance);        // player still broke
    }

    [Fact]
    public void ThreeStrikes_ContractCancelledAndPenaltyDeducted()
    {
        // Player can never deliver (no coal) → 3 strikes → cancellation.
        var (loop, ai) = Setup(playerBalance: 10_000m, aiBalance: 5_000m);
        // No coal in inventory → player always fails to deliver.

        var contract = AiBuyContract(qtyPerTurn: 10, price: 5m); // penalty = $50*3 = $150
        contract.TurnsRemaining = 10;
        loop.State.Player.ActiveContracts.Add(contract);
        ai.ActiveContracts.Add(Contract.CreateMirror(contract, "Player"));

        loop.ProcessTurn();
        loop.ProcessTurn();
        loop.ProcessTurn();

        Assert.Empty(loop.State.Player.ActiveContracts);
        Assert.Equal(9_850m, loop.State.Player.Balance); // $10,000 − $150 penalty
    }

    [Fact]
    public void ThreeStrikes_MirrorRemovedFromAi()
    {
        var (loop, ai) = Setup();

        var contract = AiBuyContract();
        contract.TurnsRemaining = 10;
        loop.State.Player.ActiveContracts.Add(contract);
        ai.ActiveContracts.Add(Contract.CreateMirror(contract, "Player"));

        loop.ProcessTurn();
        loop.ProcessTurn();
        loop.ProcessTurn();

        Assert.Empty(ai.ActiveContracts);
    }

    [Fact]
    public void ContractExpiry_MirrorRemovedFromAiWhenDurationReached()
    {
        // Player fulfils every turn; when TurnsRemaining hits 0 the mirror must be cleaned up.
        var (loop, ai) = Setup(playerBalance: 10_000m, aiBalance: 5_000m);
        loop.State.Player.Inventory[ResourceNames.Coal] = 200;

        var contract = AiBuyContract(qtyPerTurn: 10, price: 5m);
        contract.TurnsRemaining = 2;
        loop.State.Player.ActiveContracts.Add(contract);
        ai.ActiveContracts.Add(Contract.CreateMirror(contract, "Player"));

        loop.ProcessTurn(); // TurnsRemaining → 1 (mirror also ticked by AI's turn → 1)
        loop.ProcessTurn(); // TurnsRemaining → 0 → contract removed; mirror also removed

        Assert.Empty(loop.State.Player.ActiveContracts);
        Assert.Empty(ai.ActiveContracts);
    }

    [Fact]
    public void MarketContract_NoBilateralTransfer()
    {
        // Market-sourced contracts: player transacts with the abstract market only.
        var (loop, _) = Setup(playerBalance: 10_000m);
        loop.State.Player.Inventory[ResourceNames.Coal] = 50;

        var contract = AiBuyContract(source: "Market", qtyPerTurn: 10, price: 5m);
        contract.TurnsRemaining = 5;
        loop.State.Player.ActiveContracts.Add(contract);

        loop.ProcessTurn();

        // Coal delivered, money received — same as before bilateral settlement.
        Assert.Equal(40,      loop.State.Player.Inventory[ResourceNames.Coal]);
        Assert.Equal(10_050m, loop.State.Player.Balance);
    }
}
