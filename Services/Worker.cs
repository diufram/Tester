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
        _random = new Random(unchecked(id * 397) ^ Environment.TickCount);
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
        var cant = 0;
        //while (_isRunning)
        while (cant < 1)
        {
            OperationRequest? operation = null;
            cant++;
            try
            {
                operation = _manager.GetNextOperation(_random);
                if (operation == null)
                {
                    Console.WriteLine($"[Worker {_id}] Sin operaciones disponibles, esperando...");
                    await Task.Delay(5000);
                    continue;
                }

                // CONTAR LA PETICIÓN INMEDIATAMENTE AL ENVIARLA
                _manager.RecordRequestSent(operation.Method ?? "GET", operation.Url);

                var startTime = DateTime.Now;
                HttpResponseMessage? response = null;
                bool requestCompleted = false;
                
                try
                {
                    response = await MakeRequest(client, operation);
                    requestCompleted = true;
                    var duration = DateTime.Now - startTime;

                    // Determinar si fue exitoso
                    bool isSuccess = IsSuccessStatusCode(response.StatusCode);
                    string statusEmoji = isSuccess ? "✅" : "❌";

                    Console.WriteLine($"[Worker {_id}] {operation.Method} {operation.Url} -> {(int)response.StatusCode} {response.ReasonPhrase} {statusEmoji} ({duration.TotalMilliseconds:F0}ms)");
                    
                    // Registrar el resultado de la respuesta
                    if (isSuccess)
                    {
                        _manager.RecordRequestSuccess(operation.Method ?? "GET", operation.Url);
                    }
                    else
                    {
                        _manager.RecordRequestError(operation.Method ?? "GET", operation.Url);
                    }
                }
                catch (TaskCanceledException) when (!_isRunning)
                {
                    // Worker detenido, salir sin registrar timeout
                    break;
                }
                catch (TaskCanceledException)
                {
                    // Timeout - la petición ya fue contada como enviada
                    Console.WriteLine($"[Worker {_id}] Timeout: La petición tardó más de 10 segundos");
                    _manager.RecordRequestTimeout(operation.Method ?? "GET", operation.Url);
                }
                catch (HttpRequestException ex)
                {
                    // Error de red - la petición ya fue contada como enviada
                    Console.WriteLine($"[Worker {_id}] HTTP Error: {ex.Message}");
                    _manager.RecordRequestError(operation.Method ?? "GET", operation.Url);
                }
                finally
                {
                    response?.Dispose();
                }

                // Descanso aleatorio
                var delay = _random.Next(_minDelayMs, _maxDelayMs);
                await Task.Delay(delay);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Worker {_id}] Error inesperado: {ex.Message}");
                // Si hubo una operación pero falló antes del envío, no contamos nada
                // porque RecordRequestSent solo se llama justo antes de MakeRequest
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

        // Headers personalizados por operación
        if (operation.Headers is not null)
        {
            foreach (var kv in operation.Headers)
            {
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
        if (body is string s)
            return new StringContent(s, Encoding.UTF8, "application/json");

        if (body is JsonElement el)
            return new StringContent(el.GetRawText(), Encoding.UTF8, "application/json");

        var json = JsonSerializer.Serialize(body, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        return new StringContent(json, Encoding.UTF8, "application/json");
    }
}