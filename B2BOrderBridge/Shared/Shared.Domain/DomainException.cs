namespace Shared.Domain;

public class DomainException : Exception
{
    public DomainException(string code, string message) : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    public string Code { get; }
}
