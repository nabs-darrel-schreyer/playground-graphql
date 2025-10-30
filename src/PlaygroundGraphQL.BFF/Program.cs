using PlaygroundGraphQL.BFF.Domains.ProductDomain;
using PlaygroundGraphQL.BFF.Models;

var builder = WebApplication.CreateBuilder(args);


builder.Services
    .AddGraphQLServer()
    .AddQueryType<ProductDomainQuery>()
    .AddType<Product>()
    .AddFiltering()
    .AddSorting()
    .AddProjections();

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.MapGraphQL();

app.Run();
