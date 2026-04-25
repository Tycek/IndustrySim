using IndustrySim.Core.Industries;

namespace IndustrySim.Core.Game;

/// <summary>
/// Drives the turn-based game loop. Each call to <see cref="ProcessTurn"/> advances
/// the simulation by one turn: industries produce, the market updates, AI acts.
/// </summary>
public class GameLoop
{
    public GameState State { get; }

    public GameLoop(GameState state) => State = state;

    /// <summary>Creates a new game session with a fresh <see cref="GameState"/>.</summary>
    public static GameLoop StartNew(string playerName, decimal startingBalance = 10_000m) => new(new GameState
    {
        Player = new Player { Name = playerName, Balance = startingBalance }
    });

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
    /// Advances the game by one turn.
    /// </summary>
    public void ProcessTurn()
    {
        State.TurnNumber++;

        // TODO: resolve market offers and contracts
        // TODO: process AI company turns

        // Mines produce each turn until their reserves are exhausted.
        foreach (var mine in State.Player.Industries.OfType<MineBase>().Where(m => m.IsOpen))
        {
            foreach (var resource in mine.Process(State.Player.Inventory))
                State.Player.AddToInventory(resource);
        }

        // Deduct running costs. Closed mines (Capacity = 0) are excluded.
        foreach (var industry in State.Player.Industries)
        {
            if (industry is MineBase mine && !mine.IsOpen)
                continue;
            State.Player.Balance -= industry.RunningCost;
        }
    }
}
