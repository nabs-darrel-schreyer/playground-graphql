using System;
using PlaygroundGraphQL.BFF.Domains.ProductDomain;
using PlaygroundGraphQL.BFF.Models;

namespace PlayGroundGraphQL.UnitTests;

public class ProductDomainQueryUnitTests
{
    private ProductDomainQuery _query = new();

    [Fact]
    public void GetProducts_ReturnsAllProducts()
    {
        // Arrange
        // Act
        var result = _query.GetProducts();

        // Assert
        Assert.NotNull(result);
        var products = result.ToList();
        Assert.Equal(2, products.Count);
    }

    [Fact]
    public void GetProducts_ReturnsIQueryable()
    {
        // Arrange
        // Act
        var result = _query.GetProducts();

        // Assert
        Assert.IsAssignableFrom<IQueryable<Product>>(result);
    }

    [Fact]
    public void GetProducts_ContainsExpectedProducts()
    {
        // Arrange
        // Act
        var result = _query.GetProducts().ToList();

        // Assert
        Assert.Contains(result, p => p.Id == 1 && p.Name == "Sample Product" && p.Price == 9.99m);
        Assert.Contains(result, p => p.Id == 2 && p.Name == "Electricity" && p.Price == 19.99m);
    }

    [Fact]
    public void GetProducts_SupportsLinqOperations()
    {
        // Arrange
        // Act
        var result = _query.GetProducts()
            .Where(p => p.Price > 10m)
            .OrderBy(p => p.Name)
            .ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("Electricity", result[0].Name);
    }

    [Fact]
    public void GetProduct_WithValidId_ReturnsProduct()
    {
        // Arrange
        var expectedId = 1;

        // Act
        var result = _query.GetProduct(expectedId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedId, result.Id);
        Assert.Equal("Sample Product", result.Name);
        Assert.Equal(9.99m, result.Price);
    }

    [Fact]
    public void GetProduct_WithAnotherValidId_ReturnsCorrectProduct()
    {
        // Arrange
        var expectedId = 2;

        // Act
        var result = _query.GetProduct(expectedId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedId, result.Id);
        Assert.Equal("Electricity", result.Name);
        Assert.Equal(19.99m, result.Price);
    }

    [Fact]
    public void GetProduct_WithInvalidId_ReturnsNull()
    {
        // Arrange
        var invalidId = 999;

        // Act
        var result = _query.GetProduct(invalidId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetProduct_WithZeroId_ReturnsNull()
    {
        // Arrange
        // Act
        var result = _query.GetProduct(0);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetProduct_WithNegativeId_ReturnsNull()
    {
        // Arrange
        // Act
        var result = _query.GetProduct(-1);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetProducts_AllProductsHaveCreatedAt()
    {
        // Arrange
        // Act
        var result = _query.GetProducts().ToList();

        // Assert
        Assert.All(result, product =>
        {
            Assert.NotEqual(default(DateTime), product.CreatedAt);
            Assert.True(product.CreatedAt <= DateTime.UtcNow);
        });
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(100, false)]
    [InlineData(-5, false)]
    public void GetProduct_VariousIds_ReturnsExpectedResult(int id, bool shouldExist)
    {
        // Arrange
        // Act
        var result = _query.GetProduct(id);

        // Assert
        if (shouldExist)
        {
            Assert.NotNull(result);
            Assert.Equal(id, result.Id);
        }
        else
        {
            Assert.Null(result);
        }
    }
}
