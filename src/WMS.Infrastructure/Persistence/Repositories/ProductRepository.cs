using Api.Common;
using Api.Products;
using Microsoft.EntityFrameworkCore;
using WMS.Infrastructure.Persistence;

namespace WMS.Infrastructure.Persistence.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly AppDbContext _context;

    public ProductRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Product?> GetProductByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<Product?> GetProductBySkuAsync(string sku, CancellationToken cancellationToken)
    {
        return await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.SKU == sku, cancellationToken);
    }

    public async Task<(IEnumerable<Product> Products, int TotalItems)> GetAllProductsAsync(
        ProductQuery query,
        CancellationToken cancellationToken)
    {
        // 1. Consulta base: AsNoTracking porque es de solo lectura.
        IQueryable<Product> dbQuery = _context.Products
            .AsNoTracking()
            .Where(p => p.SoftDelete == null);

        // 2. Búsqueda por Name o SKU (case-insensitive; IQueryable compuesto progresivamente).
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var searchTerm = query.Search.Trim().ToLower();
            dbQuery = dbQuery.Where(p =>
                (p.Name != null && p.Name.ToLower().Contains(searchTerm)) ||
                (p.SKU != null && p.SKU.ToLower().Contains(searchTerm)));
        }

        // 3. Filtro por estado (solo cuando el cliente envía el valor).
        if (query.IsActive.HasValue)
        {
            dbQuery = dbQuery.Where(p => p.IsActive == query.IsActive.Value);
        }

        // 4. El conteo se ejecuta con los filtros ya aplicados y ANTES del Skip/Take.
        int totalItems = await dbQuery.CountAsync(cancellationToken);

        // 5. Ordenamiento estable por Id antes de paginar.
        dbQuery = query.OrderDesc
            ? dbQuery.OrderByDescending(p => p.Id)
            : dbQuery.OrderBy(p => p.Id);

        // 6. Paginación (los límites ya los validó PagedQuery; se sanitiza además aquí).
        int page = Math.Max(1, query.Page);
        int pageSize = Math.Clamp(query.PageSize, 1, PagedQuery.MaxPageSize);

        var products = await dbQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (products, totalItems);
    }

    public async Task AddProductAsync(Product product, CancellationToken cancellationToken)
    {
        await _context.Products.AddAsync(product, cancellationToken);

        // Un único SaveChangesAsync es atómico (transacción implícita de EF Core).
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateProductAsync(Product product, CancellationToken cancellationToken)
    {
        _context.Products.Update(product);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteProductAsync(int id, CancellationToken cancellationToken)
    {
        var product = await _context.Products.FindAsync([id], cancellationToken);

        if (product == null)
            return;

        product.IsActive = false;
        product.SoftDelete = DateTime.UtcNow;
        product.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
