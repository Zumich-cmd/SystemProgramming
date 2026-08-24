using System.Collections.Concurrent;
using System.Text;

namespace SystemProgramming_SIEM;

internal static class Program
{
    private static readonly object ConsoleLock = new();

    private static int _generatedCount;
    private static int _processedCount;
    private static int _generationCompleted;

    private static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        var events = new ConcurrentQueue<string>();

        using var queueSignal = new AutoResetEvent(initialState: false);

        // Початковий count = 1 є службовим. Він не дозволяє CountdownEvent
        // завершитися, поки потік-обробник ще може додавати нові задачі.
        using var allTasksCompleted = new CountdownEvent(initialCount: 1);

        var processorThread = new Thread(
            () => ProcessQueue(events, queueSignal, allTasksCompleted))
        {
            IsBackground = true,
            Name = "SIEM queue processor"
        };

        processorThread.Start();

        var timerSynchronization = new object();
        var generationAllowed = true;

        using var eventGenerator = new Timer(
            _ =>
            {
                lock (timerSynchronization)
                {
                    if (!generationAllowed)
                    {
                        return;
                    }

                    var eventNumber = Interlocked.Increment(ref _generatedCount);
                    var severity = GetRandomSeverity();
                    var securityEvent = $"Event #{eventNumber} [{severity}]";

                    events.Enqueue(securityEvent);
                    queueSignal.Set();

                    WriteLine($"[Generator] Створено: {securityEvent}");
                }
            },
            state: null,
            dueTime: TimeSpan.Zero,
            period: TimeSpan.FromMilliseconds(600));

        WriteLine("SIEM запущено. Події генеруються кожні 600 мс.\n");

        Thread.Sleep(TimeSpan.FromSeconds(5));

        // Зупиняємо Timer під тим самим lock, який використовує callback.
        // Після виходу з lock нові події вже не можуть потрапити до черги.
        lock (timerSynchronization)
        {
            generationAllowed = false;
            eventGenerator.Change(Timeout.Infinite, Timeout.Infinite);
        }

        Volatile.Write(ref _generationCompleted, 1);
        queueSignal.Set();

        WriteLine("\n[System] Генератор зупинено через 5 секунд.");
        WriteLine("[System] Очікування обробки подій, що залишилися...");

        allTasksCompleted.Wait();
        processorThread.Join();

        WriteLine($"\n📊 Всього оброблено подій: {_processedCount}");
        WriteLine($"Згенеровано подій: {_generatedCount}");
    }

    private static void ProcessQueue(
        ConcurrentQueue<string> events,
        AutoResetEvent queueSignal,
        CountdownEvent allTasksCompleted)
    {
        while (Volatile.Read(ref _generationCompleted) == 0 || !events.IsEmpty)
        {
            if (!events.TryDequeue(out var securityEvent))
            {
                queueSignal.WaitOne(millisecondsTimeout: 100);
                continue;
            }

            allTasksCompleted.AddCount();

            var workItemQueued = ThreadPool.QueueUserWorkItem(
                _ =>
                {
                    try
                    {
                        WriteLine($"[Alert] Обробляю: {securityEvent}");
                        Thread.Sleep(400);
                        Interlocked.Increment(ref _processedCount);
                        WriteLine($"[Ready] Оброблено: {securityEvent}");
                    }
                    finally
                    {
                        allTasksCompleted.Signal();
                    }
                });

            if (!workItemQueued)
            {
                allTasksCompleted.Signal();
                WriteLine($"[Error] Не вдалося поставити в чергу: {securityEvent}");
            }
        }

        // Потік більше не викличе AddCount: генератор зупинений, черга порожня.
        allTasksCompleted.Signal();
    }

    private static string GetRandomSeverity()
    {
        string[] severities = ["LOW", "MEDIUM", "HIGH", "CRITICAL"];
        return severities[Random.Shared.Next(severities.Length)];
    }

    private static void WriteLine(string message)
    {
        lock (ConsoleLock)
        {
            Console.WriteLine(message);
        }
    }
}
