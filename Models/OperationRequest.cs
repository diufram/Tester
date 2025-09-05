namespace Tester.Models;

public sealed record OperationRequest(
    OperationType Operation,
    string Url,
    object? Body = null
)
{
    public Guid Id { get; init; } = Guid.NewGuid();
}

