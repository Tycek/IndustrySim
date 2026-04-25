using IndustrySim.Core.Models;

namespace IndustrySim.Core.Industries;

public class IronOreMine : MineBase
{
    public IronOreMine() => Capacity = 1000;

    public override string Name => "Iron Ore Mine";
    protected override Resource BaseOutput => new(ResourceNames.IronOre, 10);
    protected override decimal BaseBuildCost => 600m;
    protected override decimal BaseCost => 60m;
}
