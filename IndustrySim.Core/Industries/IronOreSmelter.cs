using IndustrySim.Core.Models;

namespace IndustrySim.Core.Industries;

public class IronOreSmelter : IndustryBase
{
    private static readonly IReadOnlyList<Resource> _inputs =
        [new Resource(ResourceNames.CoalCoke, 1), new Resource(ResourceNames.IronOre, 2)];

    private static readonly IReadOnlyList<Resource> _outputs =
        [new Resource(ResourceNames.SteelIngot, 1)];

    public override string Name => "Iron Ore Smelter";
    public override decimal BuildCost => 800m;
    public override decimal RunningCost => 80m;
    public override IReadOnlyList<Resource> InputsRequired => _inputs;
    public override IReadOnlyList<Resource> OutputsProduced => _outputs;
}
