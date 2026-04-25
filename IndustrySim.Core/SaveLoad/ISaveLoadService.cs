using IndustrySim.Core.Game;

namespace IndustrySim.Core.SaveLoad;

public interface ISaveLoadService
{
    void Save(GameState state, string filePath);
    GameState Load(string filePath);
    bool SaveExists(string filePath);
}
