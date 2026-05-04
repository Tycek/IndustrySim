using IndustrySim.Core.Models;

namespace IndustrySim.Core.Industries;

public class ChemicalPlant : IndustryBase
{
    private static readonly IReadOnlyList<Resource> _inputs =
        [new Resource(ResourceNames.Chemicals, 2)];

    private static readonly IReadOnlyList<Resource> _outputs =
    [
        new Resource(ResourceNames.Fertiliser,      1),
        new Resource(ResourceNames.SyntheticRubber, 1),
    ];

    public override string Name => "Chemical Plant";
    public override decimal BuildCost => 1000m;
    public override decimal RunningCost => 100m;
    public override IReadOnlyList<Resource> InputsRequired => _inputs;
    public override IReadOnlyList<Resource> OutputsProduced => _outputs;
}
