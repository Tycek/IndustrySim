using IndustrySim.Core.Models;

namespace IndustrySim.Core.Industries;

public class SteelPlateMill : IndustryBase
{
    private static readonly IReadOnlyList<Resource> _inputs =
        [new Resource(ResourceNames.SteelIngot, 2)];

    private static readonly IReadOnlyList<Resource> _outputs =
        [new Resource(ResourceNames.SteelPlate, 1)];

    public override string Name => "Steel Plate Mill";
    public override decimal BuildCost => 900m;
    public override decimal RunningCost => 90m;
    public override IReadOnlyList<Resource> InputsRequired => _inputs;
    public override IReadOnlyList<Resource> OutputsProduced => _outputs;
}
