using Api.Common;

namespace Api.Products;

public class ProductService : IProductService
{
    private readonly IProductRepository _repository;

    public ProductService(IProductRepository repository)
    {
        _repository = repository;
    }

    public async Task<ProductViewModel> GetProductByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await _repository.GetProductByIdAsync(id, cancellationToken);

        if (product == null || product.SoftDelete != null)
            throw new KeyNotFoundException($"Product with ID {id} not found.");

        return MapToViewModel(product);
    }

    public async Task<PagedResult<ProductListViewModel>> GetAllProductsAsync(
        ProductQuery query,
        CancellationToken cancellationToken = default)
    {
        SanitizeQuery(query);

        var (products, totalItems) = await _repository.GetAllProductsAsync(query, cancellationToken);

        var viewModels = products.Select(product => new ProductListViewModel
        {
            Id = product.Id,
            UUID = product.UUID,
            SKU = product.SKU,
            Name = product.Name,
            Price = product.Price,
            IsActive = product.IsActive
        })
            .ToList();

        int totalPages = (int)Math.Ceiling(totalItems / (double)query.PageSize);

        return new PagedResult<ProductListViewModel>
        {
            Page = query.Page,
            PageSize = query.PageSize,
            TotalPages = totalPages,
            TotalItems = totalItems,
            Items = viewModels
        };
    }

    public async Task<ProductViewModel> CreateProductAsync(
        ProductCreateDTO dto,
        CancellationToken cancellationToken = default)
    {
        // Mismo tratamiento que el correo de Users: comprobación en el Service
        // más índice único filtrado en BD como respaldo ante carreras.
        var existingProduct = await _repository.GetProductBySkuAsync(dto.SKU, cancellationToken);

        if (existingProduct != null && existingProduct.SoftDelete == null)
            throw new ArgumentException("A product with this SKU already exists.", nameof(dto.SKU));

        var product = new Product
        {
            UUID = Guid.NewGuid(),
            SKU = dto.SKU,
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repository.AddProductAsync(product, cancellationToken);

        return MapToViewModel(product);
    }

    public async Task<ProductViewModel> UpdateProductAsync(
        int id,
        ProductUpdateDTO dto,
        CancellationToken cancellationToken = default)
    {
        var product = await _repository.GetProductByIdAsync(id, cancellationToken);

        if (product == null || product.SoftDelete != null)
            throw new KeyNotFoundException($"Product with ID {id} not found.");

        var skuOwner = await _repository.GetProductBySkuAsync(dto.SKU, cancellationToken);

        if (skuOwner != null && skuOwner.Id != id && skuOwner.SoftDelete == null)
            throw new ArgumentException("A product with this SKU already exists.", nameof(dto.SKU));

        product.SKU = dto.SKU;
        product.Name = dto.Name;
        product.Description = dto.Description;
        product.Price = dto.Price;
        product.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateProductAsync(product, cancellationToken);

        return MapToViewModel(product);
    }

    public async Task<bool> DeleteProductAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await _repository.GetProductByIdAsync(id, cancellationToken);

        if (product == null || product.SoftDelete != null)
            return false;

        await _repository.DeleteProductAsync(id, cancellationToken);

        return true;
    }

    private static void SanitizeQuery(ProductQuery query)
    {
        query.Page = Math.Max(1, query.Page);
        query.PageSize = Math.Clamp(query.PageSize, 1, PagedQuery.MaxPageSize);
    }

    private static ProductViewModel MapToViewModel(Product product)
    {
        return new ProductViewModel
        {
            Id = product.Id,
            UUID = product.UUID,
            SKU = product.SKU,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            IsActive = product.IsActive,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt
        };
    }
}
