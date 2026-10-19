using Shared.Domain;

namespace OrderBridge.Orders.Domain.Exceptions;

public class DomainValidationException : DomainException
{
    public DomainValidationException(string code, string message) : base(code, message)
    {
    }
}
