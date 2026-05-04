using IndustrySim.Core.Models;

namespace IndustrySim.Core.Industries;

public class CopperWireDrawer : IndustryBase
{
    private static readonly IReadOnlyList<Resource> _inputs =
        [new Resource(ResourceNames.CopperIngot, 1)];

    private static readonly IReadOnlyList<Resource> _outputs =
        [new Resource(ResourceNames.CopperWire, 2)];

    public override string Name => "Copper Wire Drawer";
    public override decimal BuildCost => 550m;
    public override decimal RunningCost => 55m;
    public override IReadOnlyList<Resource> InputsRequired => _inputs;
    public override IReadOnlyList<Resource> OutputsProduced => _outputs;
}
