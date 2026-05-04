using IndustrySim.Core.Models;

namespace IndustrySim.Core.Industries;

public class CopperSmelter : IndustryBase
{
    private static readonly IReadOnlyList<Resource> _inputs =
        [new Resource(ResourceNames.CopperOre, 2)];

    private static readonly IReadOnlyList<Resource> _outputs =
        [new Resource(ResourceNames.CopperIngot, 1)];

    public override string Name => "Copper Smelter";
    public override decimal BuildCost => 750m;
    public override decimal RunningCost => 75m;
    public override IReadOnlyList<Resource> InputsRequired => _inputs;
    public override IReadOnlyList<Resource> OutputsProduced => _outputs;
}
