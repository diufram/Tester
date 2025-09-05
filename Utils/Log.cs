using System.Collections.Concurrent;

namespace Tester.Utils;

public static class Log
{
    private static readonly BlockingCollection<string> _queue = new(new ConcurrentQueue<string>());

    static Log()
    {
        var t = new Thread(() =>
        {
            foreach (var line in _queue.GetConsumingEnumerable())
                Console.WriteLine(line);
        })
        { IsBackground = true, Name = "LogWriter" };
        t.Start();
    }

    public static void Write(string line) => _queue.Add(line);

    public static void Stop() => _queue.CompleteAdding();
}
