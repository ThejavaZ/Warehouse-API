using WMS.Application.Common;

namespace Api.Products;

public interface IProductService
{
    Task<ProductViewModel> GetProductByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<PagedResult<ProductListViewModel>> GetAllProductsAsync(ProductQuery query, CancellationToken cancellationToken = default);
    Task<ProductViewModel> CreateProductAsync(ProductCreateDTO dto, CancellationToken cancellationToken = default);
    Task<ProductViewModel> UpdateProductAsync(int id, ProductUpdateDTO dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteProductAsync(int id, CancellationToken cancellationToken = default);
}
