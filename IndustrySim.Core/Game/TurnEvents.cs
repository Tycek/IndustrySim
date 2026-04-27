namespace IndustrySim.Core.Game;

/// <summary>Notable events that occurred during a turn, for the UI to present to the player.</summary>
public record TurnEvents(
    IReadOnlyList<string> DepletedMines,
    IReadOnlyList<string> CancelledContracts,
    IReadOnlyList<string> NewAiCompanies,
    IReadOnlyList<string> BankruptAiCompanies);
