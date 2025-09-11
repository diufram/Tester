using Tester.Models;
using Tester.Utils;
using System.Text;
using System.Text.Json;
using System.Net;

namespace Tester.Services;

public class Worker
{
    private readonly int _id;
    private readonly WorkerManager _manager;
    private readonly int _minDelayMs;
    private readonly int _maxDelayMs;
    private readonly Random _random;
    private bool _isRunning;
    private Task _task = Task.CompletedTask;

    public Worker(int id, int minDelayMs, int maxDelayMs, WorkerManager manager)
    {
        _id = id;
        _minDelayMs = Math.Max(0, minDelayMs);
        _maxDelayMs = Math.Max(_minDelayMs + 1, maxDelayMs);
        _random = new Random(unchecked(id * 397) ^ Environment.TickCount); // menos colisiones entre workers
        _manager = manager;
    }

    public void Start()
    {
        if (_isRunning) return;

        _isRunning = true;
        _task = Task.Run(async () => await WorkLoop());
        Console.WriteLine($"Worker {_id} iniciado");
    }

    public void Stop()
    {
        _isRunning = false;
        Console.WriteLine($"Worker {_id} detenido");
    }

    private async Task WorkLoop()
    {
        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10) 
        };

        while (_isRunning)
        {
            try
            {
                var operation = _manager.GetNextOperation(_random);
                if (operation == null)
                {
                    // No hay operaciones disponibles, esperar un poco más
                    Console.WriteLine($"[Worker {_id}] Sin operaciones disponibles, esperando...");
                    await Task.Delay(5000); // Esperar 5 segundos antes de intentar de nuevo
                    continue;
                }


                var startTime = DateTime.Now;
                var response = await MakeRequest2(client, operation);
                var duration = DateTime.Now - startTime;

                // Determinar si fue exitoso (códigos 2xx)
                bool isSuccess = IsSuccessStatusCode(response.StatusCode);
                string statusEmoji = isSuccess ? "✅" : "❌";

                Console.WriteLine($"[Worker {_id}] {operation.Method} {operation.Url} -> {(int)response.StatusCode} {response.ReasonPhrase} {statusEmoji} ({duration.TotalMilliseconds:F0}ms)");
                
                // Reportar la ejecución del endpoint al manager (con indicador de éxito/error)
                _manager.RecordEndpointExecution(operation.Method ?? "GET", operation.Url, isSuccess);

                // descanso aleatorio
                var delay = _random.Next(_minDelayMs, _maxDelayMs);
                await Task.Delay(delay);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[Worker {_id}] HTTP Error: {ex.Message}");
                await Task.Delay(2000);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                Console.WriteLine($"[Worker {_id}] Timeout: La petición tardó más de 10 segundos");
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Worker {_id}] Error inesperado: {ex.Message}");
                await Task.Delay(1000);
            }
        }
    }

    private static bool IsSuccessStatusCode(HttpStatusCode statusCode)
    {
        return ((int)statusCode >= 200) && ((int)statusCode <= 299);
    }

    private async Task<HttpResponseMessage> MakeRequest(HttpClient client, OperationRequest operation)
    {
        var method = operation.Method?.ToUpperInvariant() ?? "GET";
        var request = new HttpRequestMessage
        {
            RequestUri = new Uri(operation.Url),
            Method = method switch
            {
                "GET"    => HttpMethod.Get,
                "POST"   => HttpMethod.Post,
                "PUT"    => HttpMethod.Put,
                "PATCH"  => HttpMethod.Patch,
                "DELETE" => HttpMethod.Delete,
                _        => HttpMethod.Get
            }
        };

        // ✅ Headers personalizados por operación
        if (operation.Headers is not null)
        {
            foreach (var kv in operation.Headers)
            {
                // Evitar colisión con Authorization que seteamos abajo
                if (!kv.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                    request.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            }
        }

        // ✅ Body robusto (respeta JsonElement/string ya formateados)
        if (operation.Body is not null && (method is "POST" or "PUT" or "PATCH" or "DELETE"))
        {
            request.Content = ToJsonContent(operation.Body);
        }

        // ✅ Bearer opcional
        if (!string.IsNullOrWhiteSpace(operation.Token))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", operation.Token);
        }

        // Aceptar JSON por defecto (si el backend lo usa)
        request.Headers.TryAddWithoutValidation("Accept", "application/json");

        return await client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> MakeRequest2(HttpClient client, OperationRequest operation)
    {
        var method = operation.Method?.ToUpperInvariant() ?? "GET";
        var ip = Environment.GetEnvironmentVariable("IP") ?? throw new InvalidOperationException("Variable IP no encontrada en .env");
        var finalUrl = $"{ip}{operation.Url}";
        var request = new HttpRequestMessage
        {
            RequestUri = new Uri(finalUrl),
            Method = method switch
            {
                "GET"    => HttpMethod.Get,
                "POST"   => HttpMethod.Post,
                "PUT"    => HttpMethod.Put,
                "PATCH"  => HttpMethod.Patch,
                "DELETE" => HttpMethod.Delete,
                _        => HttpMethod.Get
            }
        };
        //Headers personalizados por operación
        if (operation.Headers is not null)
        {
            foreach (var kv in operation.Headers)
            {
                // Evitar colisión con Authorization que seteamos abajo
                if (!kv.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                    request.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            }
        }
        if (operation.Body is not null && (method is "POST" or "PUT" or "PATCH" or "DELETE"))
        {
            request.Content = ToJsonContent(operation.Body);
        }
        if (!string.IsNullOrWhiteSpace(operation.Token))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", operation.Token);
        }
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        return await client.SendAsync(request);
    }

    private static StringContent ToJsonContent(object body)
    {
        // Si viene como string, asumimos que ya es JSON (o texto) y lo enviamos tal cual.
        if (body is string s)
            return new StringContent(s, Encoding.UTF8, "application/json");

        // Si viene como JsonElement (típico al deserializar desde Interfaz.json), escribirlo “crudo”.
        if (body is JsonElement el)
            return new StringContent(el.GetRawText(), Encoding.UTF8, "application/json");

        // Si viene como objeto arbitrario (anónimo, diccionario, POCO), serializar.
        var json = JsonSerializer.Serialize(body, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

}
