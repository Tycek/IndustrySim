using IndustrySim.Core.Models;

namespace IndustrySim.Core.Industries;

public class CopperMine : MineBase
{
    public CopperMine() => Capacity = 1000;

    public override string Name => "Copper Mine";
    protected override Resource BaseOutput => new(ResourceNames.CopperOre, 10);
    protected override decimal BaseBuildCost => 650m;
    protected override decimal BaseCost => 65m;
}
