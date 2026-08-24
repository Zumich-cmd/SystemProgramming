using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace SystemProgramming_HW4;

internal static class Program
{
    private const string ChildModeArgument = "--child";

    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        if (args.Length > 0 && args[0] == ChildModeArgument)
        {
            return await RunChildModeAsync(args.Skip(1).ToArray());
        }

        return await RunParentModeAsync(args);
    }

    private static async Task<int> RunParentModeAsync(string[] args)
    {
        Console.WriteLine("Батьківський процес");
        Console.WriteLine($"PID: {Environment.ProcessId}\n");

        var filePath = args.Length >= 1
            ? args[0]
            : ReadFilePath();

        var searchWord = args.Length >= 2
            ? args[1]
            : ReadSearchWord();

        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"Файл не знайдено: {filePath}");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(searchWord))
        {
            Console.Error.WriteLine("Слово для пошуку не може бути порожнім.");
            return 1;
        }

        var startInfo = CreateChildStartInfo(filePath, searchWord);

        Console.WriteLine("Запуск дочірнього процесу...");
        Console.WriteLine($"Файл: {Path.GetFullPath(filePath)}");
        Console.WriteLine($"Слово: {searchWord}\n");

        using var childProcess = Process.Start(startInfo);

        if (childProcess is null)
        {
            Console.Error.WriteLine("Не вдалося запустити дочірній процес.");
            return 2;
        }

        Console.WriteLine($"Дочірній PID: {childProcess.Id}\n");
        await childProcess.WaitForExitAsync();

        Console.WriteLine(
            $"\nДочірній процес завершився з кодом {childProcess.ExitCode}.");

        return childProcess.ExitCode;
    }

    private static async Task<int> RunChildModeAsync(string[] args)
    {
        Console.WriteLine("Дочірній процес");
        Console.WriteLine($"PID: {Environment.ProcessId}\n");

        if (args.Length != 2)
        {
            Console.Error.WriteLine(
                "Дочірній процес очікує два аргументи: " +
                "<шлях_до_файлу> <слово>.");
            return 1;
        }

        var filePath = args[0];
        var searchWord = args[1];

        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"Файл не знайдено: {filePath}");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(searchWord))
        {
            Console.Error.WriteLine("Слово для пошуку не може бути порожнім.");
            return 1;
        }

        try
        {
            var text = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
            var count = CountWholeWordOccurrences(text, searchWord);

            Console.WriteLine($"Файл: {Path.GetFullPath(filePath)}");
            Console.WriteLine($"Слово: {searchWord}");
            Console.WriteLine($"Кількість входжень: {count}");

            return 0;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(
                $"Не вдалося прочитати файл: {exception.Message}");
            return 3;
        }
    }

    private static ProcessStartInfo CreateChildStartInfo(
        string filePath,
        string searchWord)
    {
        var processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException(
                "Не вдалося визначити шлях до поточного процесу.");

        var startInfo = new ProcessStartInfo
        {
            UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory
        };

        // Під час dotnet run поточним процесом є dotnet.exe, тому спочатку
        // передаємо шлях до DLL. Після публікації запускаємо EXE напряму.
        if (string.Equals(
                Path.GetFileNameWithoutExtension(processPath),
                "dotnet",
                StringComparison.OrdinalIgnoreCase))
        {
            startInfo.FileName = processPath;
            startInfo.ArgumentList.Add(Environment.GetCommandLineArgs()[0]);
        }
        else
        {
            startInfo.FileName = processPath;
        }

        startInfo.ArgumentList.Add(ChildModeArgument);
        startInfo.ArgumentList.Add(Path.GetFullPath(filePath));
        startInfo.ArgumentList.Add(searchWord);

        return startInfo;
    }

    private static int CountWholeWordOccurrences(
        string text,
        string searchWord)
    {
        // Межі визначаються через Unicode-букви/цифри, тому метод працює
        // і для англійських, і для українських слів.
        var pattern =
            $@"(?<![\p{{L}}\p{{N}}_]){Regex.Escape(searchWord)}" +
            @"(?![\p{L}\p{N}_])";

        return Regex.Matches(
            text,
            pattern,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Count;
    }

    private static string ReadFilePath()
    {
        var defaultPath = Path.Combine(AppContext.BaseDirectory, "sample.txt");

        Console.WriteLine("Введіть шлях до файлу.");
        Console.WriteLine($"Enter — використати тестовий файл: {defaultPath}");
        Console.Write("> ");

        var input = Console.ReadLine();
        return string.IsNullOrWhiteSpace(input) ? defaultPath : input.Trim(' ', '"');
    }

    private static string ReadSearchWord()
    {
        Console.Write("Введіть слово для пошуку (Enter — bicycle): ");
        var input = Console.ReadLine();

        return string.IsNullOrWhiteSpace(input) ? "bicycle" : input.Trim();
    }

}
