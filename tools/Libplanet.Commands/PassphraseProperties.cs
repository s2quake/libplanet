using System.IO;
using JSSoft.Commands;

namespace Libplanet.Commands;

public static class PassphraseProperties
{
    [CommandProperty("passphrase", 'p')]
    [CommandSummary("Take passphrase through this option instead of prompt")]
    [CommandDescription("Take passphrase through this option instead of prompt.  " +
        "Mutually exclusive with --passphrase-file option.")]
    [CommandPropertyExclusion(nameof(PassphraseFile))]
    public static string Passphrase { get; set; } = string.Empty;

    [CommandProperty("passphrase-file")]
    [CommandSummary("Read passphrase from the specified file instead of taking it through prompt")]
    [CommandDescription("Read passphrase from the specified file instead of taking it " +
        "through prompt.  Mutually exclusive with -p/--passphrase option.  " +
        "For standard input, use a hyphen (`-').  For an actual file named a hyphen, " +
        "prepend `./', i.e., `./-'.  Note that the trailing CR/LF is trimmed.")]
    [CommandPropertyExclusion(nameof(Passphrase))]
    public static string PassphraseFile { get; set; } = string.Empty;

    public static string GetPassphraseFromFile()
    {
        if (PassphraseFile == string.Empty)
        {
            throw new InvalidOperationException("Passphrase file path is not set.");
        }

        if (Passphrase != string.Empty)
        {
            throw new InvalidOperationException(
                $"-p/--passphrase and --passphrase-file options are mutually exclusive.");
        }

        if (PassphraseFile == "-")
        {
            if (File.Exists("-"))
            {
                Console.Error.WriteLine(
                    "Note: Passphrase is read from standard input (`-').  If you want " +
                    "to read from a file, prepend `./', i.e.: --passphrase-file=./-");
            }

            return Console.In.ReadToEnd().TrimEnd('\r', '\n');
        }

        return File.ReadAllText(PassphraseFile).TrimEnd('\r', '\n');
    }

    public static string GetPassphraseFromPrompt(string prompt1, string prompt2 = "")
    {
        if (Passphrase != string.Empty)
        {
            throw new InvalidOperationException("Passphrase is not set.");
        }

        if (PassphraseFile != string.Empty)
        {
            throw new InvalidOperationException(
                $"-p/--passphrase and --passphrase-file options are mutually exclusive.");
        }

        var passphrase1 = ConsolePasswordReader.Read(prompt1);
        if (prompt2 != string.Empty && passphrase1 != ConsolePasswordReader.Read(prompt2))
        {
            throw new InvalidOperationException("Passphrases do not match.");
        }

        return passphrase1;
    }

    public static string GetPassphrase()
    {
        if (Passphrase == string.Empty && PassphraseFile == string.Empty)
        {
            return GetPassphraseFromPrompt("Passphrase: ", "Retype passphrase: ");
        }
        else if (PassphraseFile != string.Empty && Passphrase == string.Empty)
        {
            return GetPassphraseFromFile();
        }
        else if (Passphrase != string.Empty && PassphraseFile == string.Empty)
        {
            return Passphrase;
        }
        else
        {
            throw new InvalidOperationException(
                $"-p/--passphrase and --passphrase-file options are mutually exclusive.");
        }
    }
}
