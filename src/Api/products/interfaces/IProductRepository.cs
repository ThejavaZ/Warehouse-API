namespace Api.Products;

public interface IProductRepository
{
    Task<Product?> GetProductByIdAsync(int id, CancellationToken cancellationToken);
    Task<Product?> GetProductBySkuAsync(string sku, CancellationToken cancellationToken);
    Task<(IEnumerable<Product> Products, int TotalItems)> GetAllProductsAsync(ProductQuery query, CancellationToken cancellationToken);
    Task AddProductAsync(Product product, CancellationToken cancellationToken);
    Task UpdateProductAsync(Product product, CancellationToken cancellationToken);
    Task DeleteProductAsync(int id, CancellationToken cancellationToken);
}
