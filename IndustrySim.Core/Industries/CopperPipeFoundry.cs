using IndustrySim.Core.Models;

namespace IndustrySim.Core.Industries;

public class CopperPipeFoundry : IndustryBase
{
    private static readonly IReadOnlyList<Resource> _inputs =
        [new Resource(ResourceNames.CopperIngot, 1)];

    private static readonly IReadOnlyList<Resource> _outputs =
        [new Resource(ResourceNames.CopperPipe, 1)];

    public override string Name => "Copper Pipe Foundry";
    public override decimal BuildCost => 600m;
    public override decimal RunningCost => 60m;
    public override IReadOnlyList<Resource> InputsRequired => _inputs;
    public override IReadOnlyList<Resource> OutputsProduced => _outputs;
}
