using Tester.Models;
using Tester.Utils;
using System.Text;
using System.Text.Json;

namespace Tester.Services;

public class Worker
{
    private readonly int _id;
    private readonly OperationRequest[] _operations;
    private readonly int _minDelayMs;
    private readonly int _maxDelayMs;
    private readonly Random _random;
    private bool _isRunning;
    private Task _task = Task.CompletedTask;

    public Worker(int id, OperationRequest[] operations, int minDelayMs, int maxDelayMs)
    {
        _id = id;
        _operations = operations;
        _minDelayMs = minDelayMs;
        _maxDelayMs = maxDelayMs;
        _random = new Random();
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
        using var client = new HttpClient();

        while (_isRunning)
        {
            try
            {
                // Elegir operación al azar
                var operation = _operations[_random.Next(_operations.Length)];

                // Hacer la petición
                var startTime = DateTime.Now;
                var response = await MakeRequest(client, operation);
                var duration = DateTime.Now - startTime;

                Console.WriteLine($"[Worker {_id}] {operation.Method} {operation.Url} -> {response.StatusCode} ({duration.TotalMilliseconds:F0}ms)");

                // Descansar un tiempo aleatorio
                var delay = _random.Next(_minDelayMs, _maxDelayMs);
                await Task.Delay(delay);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[Worker {_id}] HTTP Error: {ex.Message}");
                await Task.Delay(2000); // Esperar más tiempo si hay error de red
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                Console.WriteLine($"[Worker {_id}] Timeout: La petición tardó más de 10 segundos");
                await Task.Delay(1000);
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine($"[Worker {_id}] Timeout: La petición fue cancelada");
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Worker {_id}] Error inesperado: {ex.Message}");
                await Task.Delay(1000);
            }
        }
    }

    private async Task<System.Net.Http.HttpResponseMessage> MakeRequest(System.Net.Http.HttpClient client, OperationRequest operation)
    {
        var request = new System.Net.Http.HttpRequestMessage();
        request.RequestUri = new Uri(operation.Url);

        request.Method = operation.Method.ToUpper() switch
        {
            "GET" => System.Net.Http.HttpMethod.Get,
            "POST" => System.Net.Http.HttpMethod.Post,
            "PUT" => System.Net.Http.HttpMethod.Put,
            "PATCH" => System.Net.Http.HttpMethod.Patch,
            "DELETE" => System.Net.Http.HttpMethod.Delete,
            _ => System.Net.Http.HttpMethod.Get
        };

        // Agregar header de idempotencia para operaciones POST, PUT, PATCH
        if (operation.Method.ToUpper() is "POST" or "PUT" or "PATCH")
        {
            var idempotencyKey = GenerateIdempotencyKey();
            var idempotencyHeaderName = Environment.GetEnvironmentVariable("IDEMPOTENCY_KEY_HEADER") ?? "x-idempotency-key";
            request.Headers.Add(idempotencyHeaderName, idempotencyKey);
        }

        if (operation.Body != null && (operation.Method == "POST" || operation.Method == "PUT" || operation.Method == "PATCH"))
        {
            var json = System.Text.Json.JsonSerializer.Serialize(operation.Body);
            request.Content = new System.Net.Http.StringContent(json, Encoding.UTF8, "application/json");
        }

        if (!string.IsNullOrEmpty(operation.Token))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", operation.Token);
        }

        return await client.SendAsync(request);
    }

    private string GenerateIdempotencyKey()
    {
        // Generar una clave única usando GUID + timestamp para mayor unicidad
        var guid = Guid.NewGuid().ToString("N"); // Sin guiones
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return $"{guid}_{timestamp}";
    }
}

