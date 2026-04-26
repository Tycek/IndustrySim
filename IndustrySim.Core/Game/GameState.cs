using IndustrySim.Core.AiCompanies;
using IndustrySim.Core.Markets;

namespace IndustrySim.Core.Game;

/// <summary>
/// The complete, serializable state of a game session. Everything that must survive
/// a save/load lives here.
/// </summary>
public class GameState
{
    public int TurnNumber { get; set; }
    public Player Player { get; set; } = new();
    public Market Market { get; set; } = new();
    public List<AiCompany> AiCompanies { get; set; } = [];
}
