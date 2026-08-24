using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SystemProgramming_HW3;

internal static class Program
{
    private const uint MbOk = 0x00000000;
    private const uint MbIconInformation = 0x00000040;
    private const uint MbIconWarning = 0x00000030;

    [SupportedOSPlatform("windows")]
    private static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        if (!OperatingSystem.IsWindows())
        {
            Console.WriteLine("Ця програма працює тільки у Windows.");
            return;
        }

        Console.WriteLine("Використання успадкованого коду Windows API");
        Console.WriteLine("Функції: Beep і MessageBeep\n");

        Console.WriteLine("1. Системний інформаційний сигнал MessageBeep");
        PlayMessageBeep(MbIconInformation);
        Thread.Sleep(500);

        Console.WriteLine("2. Висхідна послідовність Beep");
        PlaySequence(
            new[]
            {
                new Tone(440, 250),
                new Tone(523, 250),
                new Tone(659, 250),
                new Tone(784, 400)
            },
            pauseMilliseconds: 150);

        Thread.Sleep(700);

        Console.WriteLine("3. Низхідна послідовність Beep");
        PlaySequence(
            new[]
            {
                new Tone(784, 250),
                new Tone(659, 250),
                new Tone(523, 250),
                new Tone(440, 400)
            },
            pauseMilliseconds: 150);

        Thread.Sleep(700);

        Console.WriteLine("4. Попереджувальний сигнал MessageBeep");
        PlayMessageBeep(MbIconWarning);
        Thread.Sleep(500);

        Console.WriteLine("5. Завершальний звуковий сигнал");
        PlaySequence(
            new[]
            {
                new Tone(523, 180),
                new Tone(659, 180),
                new Tone(784, 180),
                new Tone(1047, 500)
            },
            pauseMilliseconds: 100);

        PlayMessageBeep(MbOk);
        Console.WriteLine("\nУсі звукові сигнали згенеровано.");
    }

    [SupportedOSPlatform("windows")]
    private static void PlaySequence(
        IEnumerable<Tone> tones,
        int pauseMilliseconds)
    {
        foreach (var tone in tones)
        {
            Console.WriteLine(
                $"Beep: частота {tone.Frequency} Гц, " +
                $"тривалість {tone.Duration} мс");

            if (!NativeMethods.Beep(tone.Frequency, tone.Duration))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Не вдалося виконати функцію Beep.");
            }

            Thread.Sleep(pauseMilliseconds);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void PlayMessageBeep(uint type)
    {
        if (!NativeMethods.MessageBeep(type))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Не вдалося виконати функцію MessageBeep.");
        }
    }

    private readonly record struct Tone(uint Frequency, uint Duration);
}

internal static class NativeMethods
{
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Beep(uint frequency, uint duration);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool MessageBeep(uint type);
}
