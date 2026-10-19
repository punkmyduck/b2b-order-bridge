using Shared.Domain;

namespace OrderBridge.Integrations.Domain.Exceptions;

public class DomainInvalidOperationException : DomainException
{
    public DomainInvalidOperationException(string code, string message) : base(code, message)
    {
    }
}
