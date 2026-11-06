namespace WMS.Domain.Common;

public class DomainException : Exception
{
    public string Code { get; }
    public string? Field { get; }
    public int StatusCode { get; }

    public DomainException(
        string code,
        string message,
        int statusCode = 400,
        string? field = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
        StatusCode = statusCode;
        Field = field;
    }

    public static DomainException NotFound(string entity, object id)
        => new("not_found", $"{entity} with id {id} was not found.", 404);

    public static DomainException Validation(string message, string? field = null)
        => new("validation_error", message, 400, field);

    public static DomainException Conflict(string message, string? field = null)
        => new("conflict", message, 409, field);
}
