using System;

namespace PlaygroundGraphQL.BFF.Models;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public decimal Price { get; set; }
    public DateTime CreatedAt { get; set; }
}