namespace IndustrySim.Core.Models;

/// <summary>
/// Represents a named good or material with a quantity.
/// </summary>
public record Resource(string Name, double Quantity)
{
    public Resource WithQuantity(double quantity) => this with { Quantity = quantity };
    public Resource Add(double amount) => this with { Quantity = Quantity + amount };
    public Resource Scale(double factor) => this with { Quantity = Quantity * factor };
}
