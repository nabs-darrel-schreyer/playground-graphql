using HotChocolate.Resolvers;
using PlaygroundGraphQL.BFF.Models;

namespace PlaygroundGraphQL.BFF.Domains.ProductDomain;

public class ProductDomainQuery
{
    private static List<Product> _cache =
        [
            new()
            {
                Id = 1,
                Name = "Sample Product",
                Price = 9.99m,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = 2,
                Name = "Electricity",
                Price = 19.99m,
                CreatedAt = DateTime.UtcNow
            }
        ];

    [UsePaging]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Product> GetProducts(IResolverContext context = default!)
    {
        return _cache.AsQueryable();
    }

    public Product? GetProduct(int id, IResolverContext context = default!)
    {
        return _cache.Find(x => x.Id == id);
    }
}
