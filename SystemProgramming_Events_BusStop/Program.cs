using System.Text;

namespace SystemProgramming_Events_BusStop;

internal static class Program
{
    private const int RouteNumber = 175;
    private const int BusCount = 12;
    private const int BusCapacity = 30;

    // lock створює критичну секцію для спільного стану зупинки.
    private static readonly object StopCriticalSection = new();
    private static readonly object ConsoleLock = new();

    // Диспетчер повідомляє про автобус, а потік посадки — про завершення посадки.
    private static readonly AutoResetEvent BusArrived = new(initialState: false);
    private static readonly AutoResetEvent BoardingFinished = new(initialState: false);

    private static readonly BusTripResult[] Trips = new BusTripResult[BusCount];

    private static int _waitingPassengers;
    private static int _totalArrivedPassengers;
    private static int _totalTransportedPassengers;
    private static int _arrivingBusIndex;
    private static int _dayEnded;

    private static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        var initialPassengers = Random.Shared.Next(15, 51);

        lock (StopCriticalSection)
        {
            _waitingPassengers = initialPassengers;
            _totalArrivedPassengers = initialPassengers;
        }

        WriteLine("=== Автобусна кінцева зупинка: початок дня ===");
        WriteLine($"Маршрут: №{RouteNumber}");
        WriteLine($"Автобусів за день: {BusCount}");
        WriteLine($"Місткість кожного автобуса: {BusCapacity}");
        WriteLine($"На початку дня на зупинці: {initialPassengers} пасажирів.\n");

        var passengerGeneratorThread = new Thread(GeneratePassengers)
        {
            Name = "Генератор пасажирів",
            IsBackground = false
        };

        var boardingThread = new Thread(BoardPassengers)
        {
            Name = "Посадка пасажирів",
            IsBackground = false
        };

        var busDispatcherThread = new Thread(DispatchBuses)
        {
            Name = "Диспетчер автобусів",
            IsBackground = false
        };

        passengerGeneratorThread.Start();
        boardingThread.Start();
        busDispatcherThread.Start();

        busDispatcherThread.Join();
        passengerGeneratorThread.Join();
        boardingThread.Join();

        int totalArrived;
        int totalTransported;
        int passengersLeft;

        lock (StopCriticalSection)
        {
            totalArrived = _totalArrivedPassengers;
            totalTransported = _totalTransportedPassengers;
            passengersLeft = _waitingPassengers;
        }

        if (totalArrived != totalTransported + passengersLeft)
        {
            throw new InvalidOperationException("Порушено баланс пасажирів.");
        }

        var reportPath = Path.Combine(Environment.CurrentDirectory, "bus_stop_report.txt");
        WriteReport(reportPath, totalArrived, totalTransported, passengersLeft);

        WriteLine("\n=== Робочий день завершено ===");
        WriteLine($"Усього прийшло пасажирів: {totalArrived}");
        WriteLine($"Перевезено пасажирів: {totalTransported}");
        WriteLine($"Залишилося на зупинці: {passengersLeft}");
        WriteLine($"Перевірка балансу: {totalArrived} = {totalTransported} + {passengersLeft}");
        WriteLine($"Звіт збережено у файлі:\n{reportPath}");

        BusArrived.Dispose();
        BoardingFinished.Dispose();
    }

    private static void GeneratePassengers()
    {
        while (Volatile.Read(ref _dayEnded) == 0)
        {
            Thread.Sleep(Random.Shared.Next(40, 91));

            if (Volatile.Read(ref _dayEnded) == 1)
            {
                break;
            }

            var newPassengers = Random.Shared.Next(1, 16);
            int passengersAtStop;

            lock (StopCriticalSection)
            {
                _waitingPassengers += newPassengers;
                _totalArrivedPassengers += newPassengers;
                passengersAtStop = _waitingPassengers;
            }

            WriteLine(
                $"[Зупинка] Прийшло: {newPassengers}; " +
                $"тепер очікує: {passengersAtStop}.");
        }
    }

    private static void DispatchBuses()
    {
        for (var busIndex = 1; busIndex <= BusCount; busIndex++)
        {
            Thread.Sleep(200);

            lock (StopCriticalSection)
            {
                _arrivingBusIndex = busIndex;
            }

            WriteLine($"\n[Диспетчер] Прибув автобус №{RouteNumber}, рейс {busIndex}.");

            BusArrived.Set();
            BoardingFinished.WaitOne();
        }

        Volatile.Write(ref _dayEnded, 1);
    }

    private static void BoardPassengers()
    {
        for (var completedTrips = 0; completedTrips < BusCount; completedTrips++)
        {
            BusArrived.WaitOne();

            int busIndex;
            int waitingBeforeBoarding;
            int boardedPassengers;
            int waitingAfterBoarding;

            lock (StopCriticalSection)
            {
                busIndex = _arrivingBusIndex;
                waitingBeforeBoarding = _waitingPassengers;
                boardedPassengers = Math.Min(_waitingPassengers, BusCapacity);

                _waitingPassengers -= boardedPassengers;
                _totalTransportedPassengers += boardedPassengers;
                waitingAfterBoarding = _waitingPassengers;

                Trips[busIndex - 1] = new BusTripResult(
                    busIndex,
                    waitingBeforeBoarding,
                    boardedPassengers,
                    waitingAfterBoarding);
            }

            WriteLine(
                $"[Посадка] Рейс {busIndex}: зайшло {boardedPassengers} із " +
                $"{waitingBeforeBoarding}; залишилося {waitingAfterBoarding}.");

            BoardingFinished.Set();
        }
    }

    private static void WriteReport(
        string reportPath,
        int totalArrived,
        int totalTransported,
        int passengersLeft)
    {
        var reportLines = new List<string>
        {
            "ЗВІТ ПРО РОБОТУ АВТОБУСНОЇ КІНЦЕВОЇ ЗУПИНКИ",
            $"Маршрут: №{RouteNumber}",
            $"Кількість автобусів: {BusCount}",
            $"Місткість автобуса: {BusCapacity}",
            string.Empty
        };

        reportLines.AddRange(
            Trips.Select(trip =>
                $"Рейс {trip.TripNumber}: очікувало [{trip.WaitingBefore}], " +
                $"поїхало [{trip.Boarded}], залишилося [{trip.WaitingAfter}]")
        );

        reportLines.Add(string.Empty);
        reportLines.Add($"Усього прийшло пасажирів: {totalArrived}");
        reportLines.Add($"Усього перевезено: {totalTransported}");
        reportLines.Add($"Залишилося на зупинці: {passengersLeft}");
        reportLines.Add($"Баланс: {totalArrived} = {totalTransported} + {passengersLeft}");

        File.WriteAllLines(reportPath, reportLines, Encoding.UTF8);
    }

    private static void WriteLine(string message)
    {
        lock (ConsoleLock)
        {
            Console.WriteLine(message);
        }
    }

    private readonly record struct BusTripResult(
        int TripNumber,
        int WaitingBefore,
        int Boarded,
        int WaitingAfter);
}
