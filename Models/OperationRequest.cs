namespace Tester.Models;

// Modelos simples
public class OperationRequest
{
    public required string Url { get; set; }
    public required string Method { get; set; }
    public object? Body { get; set; }
    public string? Token { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
}

