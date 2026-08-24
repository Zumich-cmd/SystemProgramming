namespace SystemProgramming_PrimesThreads;

internal static class PrimeGenerator
{
    public static bool IsPrime(long number)
    {
        if (number < 2)
        {
            return false;
        }

        if (number == 2)
        {
            return true;
        }

        if (number % 2 == 0)
        {
            return false;
        }

        // number / divisor замість divisor * divisor запобігає переповненню.
        for (long divisor = 3; divisor <= number / divisor; divisor += 2)
        {
            if (number % divisor == 0)
            {
                return false;
            }
        }

        return true;
    }
}
