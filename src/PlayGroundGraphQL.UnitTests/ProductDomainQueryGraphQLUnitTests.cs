using HotChocolate;
using HotChocolate.Execution;
using Microsoft.Extensions.DependencyInjection;
using PlaygroundGraphQL.BFF.Domains.ProductDomain;
using PlaygroundGraphQL.BFF.Models;

namespace PlayGroundGraphQL.UnitTests;

public class ProductDomainQueryGraphQLUnitTests
{

    static CancellationToken ct = TestContext.Current.CancellationToken;

    private async Task<IRequestExecutor> CreateExecutorAsync()
    {
        return await new ServiceCollection()
            .AddGraphQL()
            .AddQueryType<ProductDomainQuery>()
            .AddFiltering()
            .AddSorting()
            .BuildRequestExecutorAsync();
    }

    [Fact]
    public async Task GetProducts_ReturnsAllProducts()
    {
        // Arrange
        var executor = await CreateExecutorAsync();
        var (query, _) = GraphQLQueryBuilder
            .Query()
            .Collection<Product>(
                "products",
                p => new { p.Id, p.Name, p.Price })
            .Build();

        // Act
        var result = await executor.ExecuteAsync(query, ct);

        // Assert
        var queryResult = result.ExpectOperationResult();
        Assert.Empty(queryResult.Errors ?? []);
        Assert.NotNull(queryResult.Data);

        var json = result.ToJson();
        Assert.Contains("Sample Product", json);
        Assert.Contains("Electricity", json);
    }

    [Fact]
    public async Task GetProducts_WithFiltering_ReturnsFilteredProducts()
    {
        // Arrange
        var executor = await CreateExecutorAsync();
        var (query, _) = GraphQLQueryBuilder
            .Query()
            .Collection<Product>(
                "products",
                p => new { p.Id, p.Name, p.Price },
                where: p => p.Price > 10)
            .Build();

        // Act
        var result = await executor.ExecuteAsync(query, ct);

        // Assert
        var queryResult = result.ExpectOperationResult();
        Assert.Empty(queryResult.Errors ?? []);
        var json = result.ToJson();
        Assert.Contains("Electricity", json);
        Assert.DoesNotContain("Sample Product", json);
    }

    [Fact]
    public async Task GetProducts_WithSorting_ReturnsSortedProducts()
    {
        // Arrange
        var executor = await CreateExecutorAsync();
        var (query, _) = GraphQLQueryBuilder
            .Query()
            .Collection<Product>(
                "products",
                p => new { p.Id, p.Name },
                order: p => new { p.Name }, descending: true)
            .Build();

        // Act
        var result = await executor.ExecuteAsync(query, ct);

        // Assert
        var queryResult = result.ExpectOperationResult();
        Assert.Empty(queryResult.Errors ?? []);
        var json = result.ToJson();

        // Verify "Sample Product" comes before "Electricity" (DESC order)
        var sampleIndex = json.IndexOf("Sample Product");
        var electricityIndex = json.IndexOf("Electricity");
        Assert.True(sampleIndex < electricityIndex);
    }

    [Fact]
    public async Task GetProduct_WithValidId_ReturnsProduct()
    {
        // Arrange
        var executor = await CreateExecutorAsync();
        var (query, v) = GraphQLQueryBuilder
            .Query()
            .Entity<Product>(
                "product",
                p => new { p.Id, p.Name, p.Price },
                where: p => p.Id == 1)
            .Build();

        // Act
        var result = await executor.ExecuteAsync(query, ct);

        // Assert
        var queryResult = result.ExpectOperationResult();
        Assert.Empty(queryResult.Errors ?? []);
        var json = result.ToJson();
        Assert.Contains("Sample Product", json);
        Assert.Contains("9.99", json);
    }

    [Fact]
    public async Task GetProduct_WithInvalidId_ReturnsNull()
    {
        // Arrange
        var executor = await CreateExecutorAsync();
        var (query, v) = GraphQLQueryBuilder
            .Query()
            .Entity<Product>(
                "product",
                p => new { p.Id, p.Name },
                where: p => p.Id == 999)
            .Build();

        // Act
        var result = await executor.ExecuteAsync(query, ct);

        // Assert
        var queryResult = result.ExpectOperationResult();
        Assert.Empty(queryResult.Errors ?? []);
        var json = result.ToJson();
        Assert.Contains("null", json);
    }

    [Fact]
    public async Task GetProducts_WithComplexQuery_WorksCorrectly()
    {
        // Arrange
        var executor = await CreateExecutorAsync();
        var (query, v) = GraphQLQueryBuilder
            .Query()
            .Collection<Product>(
                "products",
                p => new { p.Id, p.Name, p.Price },
                where: p => p.Price >= 5,
                order: p => new { p.Price })
            .Build();

        // Act
        var result = await executor.ExecuteAsync(query, ct);

        // Assert
        var queryResult = result.ExpectOperationResult();
        Assert.Empty(queryResult.Errors ?? []);
        var json = result.ToJson();

        // Verify order: Sample Product (9.99) before Electricity (19.99)
        var sampleIndex = json.IndexOf("Sample Product");
        var electricityIndex = json.IndexOf("Electricity");
        Assert.True(sampleIndex < electricityIndex);
    }

    [Fact]
    public async Task Schema_IsValid()
    {
        // Arrange & Act
        var executor = await CreateExecutorAsync();
        var schema = executor.Schema;

        // Assert
        Assert.NotNull(schema);
        Assert.NotNull(schema.QueryType);

        var productsField = schema.QueryType.Fields["products"];
        Assert.NotNull(productsField);
    }
}
