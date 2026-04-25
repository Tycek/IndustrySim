using System.Text.Json;
using IndustrySim.Core.Game;

namespace IndustrySim.Core.SaveLoad;

public class JsonSaveLoadService : ISaveLoadService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public void Save(GameState state, string filePath)
    {
        var data = new SaveData { SavedAt = DateTime.UtcNow, GameState = state };
        File.WriteAllText(filePath, JsonSerializer.Serialize(data, Options));
    }

    public GameState Load(string filePath)
    {
        var data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(filePath))
            ?? throw new InvalidOperationException("Save file could not be deserialized.");
        return data.GameState;
    }

    public bool SaveExists(string filePath) => File.Exists(filePath);
}
