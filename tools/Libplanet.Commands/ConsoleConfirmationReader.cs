namespace Libplanet.Commands;

public static class ConsoleConfirmationReader
{
    public static bool Read(string prompt)
    {
        while (true)
        {
            Console.Write($"{prompt} [y/n]: ");
            var input = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (input == null || input == string.Empty || input == "n" || input == "no")
            {
                return false;
            }

            if (input == "y" || input == "yes")
            {
                return true;
            }
        }
    }
}
