namespace SeedField.Catalog;

public sealed class CatalogException : Exception
{
    public CatalogException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}
