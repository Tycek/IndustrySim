using IndustrySim.Core.Industries;
using IndustrySim.Core.Models;

namespace IndustrySim.Core.Game;

public class Player
{
    public string Name { get; set; } = string.Empty;
    public decimal Balance { get; set; }

    // Note: System.Text.Json requires polymorphic configuration to round-trip IIndustry.
    // Wire up JsonDerivedType attributes or a custom converter before enabling save/load.
    public List<IIndustry> Industries { get; set; } = [];

    public Dictionary<string, double> Inventory { get; set; } = [];

    public void AddToInventory(Resource resource) =>
        Inventory[resource.Name] = Inventory.GetValueOrDefault(resource.Name) + resource.Quantity;
}
