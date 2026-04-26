using IndustrySim.Core.Models;

namespace IndustrySim.Core.Markets;

/// <summary>
/// Common interface for any entity that holds a balance, inventory, and active contracts.
/// Used to apply bilateral offer/contract settlement uniformly across the player and AI companies.
/// </summary>
public interface IMarketParticipant
{
    string Name { get; }
    decimal Balance { get; set; }
    Dictionary<string, double> Inventory { get; }
    List<Contract> ActiveContracts { get; }
    void AddToInventory(Resource resource);
}
