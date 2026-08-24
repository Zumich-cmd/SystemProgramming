using System.Text;

namespace SystemProgramming_HW6_Casino;

internal static class Program
{
    private const int SeatCount = 5;
    private const long InitialMoney = 500;

    private static readonly SemaphoreSlim TableSeats = new(SeatCount, SeatCount);
    private static readonly Mutex RouletteMutex = new();
    private static readonly object ConsoleLock = new();

    private static CountdownEvent _allPlayersFinished = null!;
    private static PlayerResult[] _results = [];

    private static int _totalPlayers;
    private static int _nextPlayerId;
    private static int _playersWhoPlayed;
    private static int _dayIsEnding;
    private static int _playersAtTable;
    private static int _maximumPlayersAtTable;

    private static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        _totalPlayers = Random.Shared.Next(20, 101);
        _results = new PlayerResult[_totalPlayers];
        _allPlayersFinished = new CountdownEvent(_totalPlayers);

        WriteLine("=== Казино: початок дня ===");
        WriteLine($"Потенційних гравців сьогодні: {_totalPlayers}");
        WriteLine($"За столом одночасно може бути не більше {SeatCount} гравців.");
        WriteLine($"Початкова сума кожного гравця: {InitialMoney} грн.\n");

        for (var i = 0; i < Math.Min(SeatCount, _totalPlayers); i++)
        {
            StartNextPlayer();
        }

        _allPlayersFinished.Wait();

        var reportPath = Path.Combine(Environment.CurrentDirectory, "casino_report.txt");
        WriteReport(reportPath);

        WriteLine("\n=== Казино: день завершено ===");
        WriteLine($"Зіграли хоча б один раунд: {_playersWhoPlayed} з {_totalPlayers}");
        WriteLine($"Максимальна кількість гравців за столом: {_maximumPlayersAtTable}");
        WriteLine($"Звіт збережено у файлі:\n{reportPath}");

        _allPlayersFinished.Dispose();
        RouletteMutex.Dispose();
        TableSeats.Dispose();
    }

    private static void StartNextPlayer()
    {
        var playerId = Interlocked.Increment(ref _nextPlayerId);

        if (playerId > _totalPlayers)
        {
            return;
        }

        var playerThread = new Thread(() => Play(playerId))
        {
            IsBackground = false,
            Name = $"Гравець {playerId}"
        };

        playerThread.Start();
    }

    private static void Play(int playerId)
    {
        var money = InitialMoney;
        var roundsPlayed = 0;
        var seatTaken = false;

        try
        {
            TableSeats.Wait();
            seatTaken = true;

            var playersAtTable = Interlocked.Increment(ref _playersAtTable);
            UpdateMaximumPlayersAtTable(playersAtTable);

            WriteLine($"Гравець{playerId} сів за стіл. За столом: {playersAtTable}.");

            while (money > 0)
            {
                var stake = Random.Shared.NextInt64(1, money + 1);
                var selectedNumber = Random.Shared.Next(0, 37);
                int rouletteNumber;
                var won = false;

                RouletteMutex.WaitOne();

                try
                {
                    rouletteNumber = Random.Shared.Next(0, 37);
                    won = selectedNumber == rouletteNumber;

                    if (won)
                    {
                        money = money > long.MaxValue / 2 ? long.MaxValue : money * 2;
                    }
                    else
                    {
                        money -= stake;
                    }

                    roundsPlayed++;

                    WriteLine(
                        $"Гравець{playerId}: ставка {stake} грн на {selectedNumber}; " +
                        $"випало {rouletteNumber}; {(won ? "ВИГРАШ" : "програш")}; " +
                        $"баланс {money} грн.");
                }
                finally
                {
                    RouletteMutex.ReleaseMutex();
                }

                if (roundsPlayed == 1)
                {
                    var playersWhoPlayed = Interlocked.Increment(ref _playersWhoPlayed);

                    if (playersWhoPlayed == _totalPlayers)
                    {
                        Volatile.Write(ref _dayIsEnding, 1);
                        WriteLine("\nУсі потенційні гравці зіграли хоча б один раунд. День завершується.");
                    }
                }

                if (money == 0 || Volatile.Read(ref _dayIsEnding) == 1)
                {
                    break;
                }

                Thread.Sleep(Random.Shared.Next(10, 31));
            }
        }
        finally
        {
            _results[playerId - 1] = new PlayerResult(
                playerId,
                InitialMoney,
                money,
                roundsPlayed);

            if (money == 0)
            {
                WriteLine($"Гравець{playerId} програв усі гроші та звільняє місце.");
            }
            else
            {
                WriteLine($"Гравець{playerId} залишає стіл із сумою {money} грн.");
            }

            if (seatTaken)
            {
                Interlocked.Decrement(ref _playersAtTable);
                TableSeats.Release();
            }

            // Після звільнення місця створюється новий потік наступного гравця.
            StartNextPlayer();
            _allPlayersFinished.Signal();
        }
    }

    private static void UpdateMaximumPlayersAtTable(int currentCount)
    {
        var observedMaximum = Volatile.Read(ref _maximumPlayersAtTable);

        while (currentCount > observedMaximum)
        {
            var previousValue = Interlocked.CompareExchange(
                ref _maximumPlayersAtTable,
                currentCount,
                observedMaximum);

            if (previousValue == observedMaximum)
            {
                return;
            }

            observedMaximum = previousValue;
        }
    }

    private static void WriteReport(string reportPath)
    {
        var reportLines = _results
            .OrderBy(result => result.PlayerId)
            .Select(result =>
                $"Гравець{result.PlayerId} [{result.InitialMoney}] [{result.FinalMoney}]");

        File.WriteAllLines(reportPath, reportLines, Encoding.UTF8);
    }

    private static void WriteLine(string message)
    {
        lock (ConsoleLock)
        {
            Console.WriteLine(message);
        }
    }

    private readonly record struct PlayerResult(
        int PlayerId,
        long InitialMoney,
        long FinalMoney,
        int RoundsPlayed);
}
