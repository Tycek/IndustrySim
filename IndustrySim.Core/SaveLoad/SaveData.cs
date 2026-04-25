using IndustrySim.Core.Game;

namespace IndustrySim.Core.SaveLoad;

/// <summary>
/// Envelope written to disk. Wraps <see cref="GameState"/> with metadata so future
/// versions can detect and migrate old saves.
/// </summary>
public class SaveData
{
    public int Version { get; set; } = 1;
    public DateTime SavedAt { get; set; }
    public GameState GameState { get; set; } = new();
}
