using System.ComponentModel.DataAnnotations;

namespace WMS.Application.Common;

public class PagedQuery
{
    public const int MaxPageSize = 100;

    [Range(1, int.MaxValue, ErrorMessage = "La página debe ser al menos 1.")]
    public int Page { get; set; } = 1;

    [Range(1, MaxPageSize, ErrorMessage = "El tamaño de página debe estar entre 1 y 100.")]
    public int PageSize { get; set; } = 10;

    public bool OrderDesc { get; set; } = true;
}
