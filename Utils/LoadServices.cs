using System.Text.Json;
using Tester.Models;

namespace Tester.Utils;

public static class OperationLoader
{
    public static OperationRequest[]? LoadOperationsFromJson(string path, string fallbackToken)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"ℹ️  No se encontró {path}. Usando operaciones por defecto.");
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);

            var opts = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var ops = JsonSerializer.Deserialize<OperationRequest[]>(json, opts);

            if (ops is null || ops.Length == 0)
            {
                Console.WriteLine("⚠️  Interfaz.json vacío o mal formado. Usando operaciones por defecto.");
                return null;
            }

            // Si alguna operación no trae token, aplicar el de .env
            foreach (var op in ops)
            {
                if (string.IsNullOrWhiteSpace(op.Token))
                    op.Token = fallbackToken;
            }

            Console.WriteLine($"✅ Cargadas {ops.Length} operaciones desde {path}");
            return ops;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error leyendo {path}: {ex.Message}");
            return null;
        }
    }

    public static OperationRequest[] GetDefaultOps(string token) => new[]
    {
        new OperationRequest { Url = "tu-url", Method = "GET", Token = token }
    };
}
