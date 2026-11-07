using WMS.Application.Products;
using WMS.Domain.Entities;

namespace WMS.Application.Test.Fakes;

/// <summary>
/// Implementación en memoria de IProductRepository para pruebas unitarias del servicio.
/// Reproduce el comportamiento de filtrado del repositorio real sin depender de EF Core.
/// </summary>
public class FakeProductRepository : IProductRepository
{
    public List<Product> Products { get; } = new();

    public Task<Product?> GetProductByIdAsync(int id, CancellationToken cancellationToken)
        => Task.FromResult(Products.SingleOrDefault(p => p.Id == id));

    public Task<Product?> GetProductBySkuAsync(string sku, CancellationToken cancellationToken)
        => Task.FromResult(Products.SingleOrDefault(p => p.SKU == sku));

    public Task<(IEnumerable<Product> Products, int TotalItems)> GetAllProductsAsync(
        ProductQuery query,
        CancellationToken cancellationToken)
    {
        var dbQuery = Products.AsEnumerable().Where(p => p.SoftDelete == null);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            dbQuery = dbQuery.Where(p =>
                (p.Name != null && p.Name.ToLower().Contains(term)) ||
                (p.SKU != null && p.SKU.ToLower().Contains(term)));
        }

        if (query.IsActive.HasValue)
        {
            dbQuery = dbQuery.Where(p => p.IsActive == query.IsActive.Value);
        }

        var totalItems = dbQuery.Count();

        dbQuery = query.OrderDesc
            ? dbQuery.OrderByDescending(p => p.Id)
            : dbQuery.OrderBy(p => p.Id);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var products = dbQuery.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Task.FromResult<(IEnumerable<Product>, int)>((products, totalItems));
    }

    public Task AddProductAsync(Product product, CancellationToken cancellationToken)
    {
        product.Id = Products.Count == 0 ? 1 : Products.Max(p => p.Id) + 1;
        Products.Add(product);

        return Task.CompletedTask;
    }

    public Task UpdateProductAsync(Product product, CancellationToken cancellationToken)
    {
        var index = Products.FindIndex(p => p.Id == product.Id);

        if (index >= 0)
        {
            Products[index] = product;
        }

        return Task.CompletedTask;
    }

    public Task DeleteProductAsync(int id, CancellationToken cancellationToken)
    {
        var product = Products.SingleOrDefault(p => p.Id == id);

        if (product != null)
        {
            product.IsActive = false;
            product.SoftDelete = DateTime.UtcNow;
            product.UpdatedAt = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }
}
