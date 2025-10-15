using System.Diagnostics;
using System.IO;
using Libplanet.Commands.IO;
using Libplanet.Serialization;
using Libplanet.Serialization.Json;
using Libplanet.Serialization.Yaml;
using Libplanet.Types;

namespace Libplanet.Commands;

public static class OutputUtility
{
    private static readonly bool IsJQSupported = SupportsJQ();
    private static readonly bool IsYQSupported = SupportsYQ();

    public static void Write(TextWriter textWriter, object value, OutputType outputType)
    {
        var options = new ModelOptions
        {
            TypeInfoEmission = TypeInfoEmission.Never,
            Purpose = SerializationPurpose.Inspection,
        };

        switch (outputType)
        {
            case OutputType.Json:
                textWriter.Write(ToColorizedJsonString(ModelJsonSerializer.Serialize(value, options)));
                break;
            case OutputType.Yaml:
                textWriter.Write(ToColorizedYamlString(ModelYamlSerializer.Serialize(value, options)));
                break;
            case OutputType.Hex:
                textWriter.Write(ByteUtility.Hex(ModelSerializer.Serialize(value)));
                break;
            default:
                throw new NotSupportedException($"Unsupported output type: {outputType}");
        }
    }

    public static void WriteLine(TextWriter textWriter, object value, OutputType outputType)
    {
        var options = new ModelOptions
        {
            TypeInfoEmission = TypeInfoEmission.Never,
            Purpose = SerializationPurpose.Inspection,
        };

        switch (outputType)
        {
            case OutputType.Json:
                var json = ModelJsonSerializer.Serialize(value, options);
                textWriter.WriteLine(ToColorizedJsonString(json));
                break;
            case OutputType.Yaml:
                var yaml = ModelYamlSerializer.Serialize(value, options);
                textWriter.WriteLine(ToColorizedYamlString(yaml));
                break;
            case OutputType.Hex:
                var bytes = ModelSerializer.Serialize(value, options);
                textWriter.WriteLine(ByteUtility.Hex(bytes));
                break;
            default:
                throw new NotSupportedException($"Unsupported output type: {outputType}");
        }
    }

    public static void WriteLine(TextWriter textWriter, string value, OutputType outputType)
    {
        var options = new ModelOptions
        {
            TypeInfoEmission = TypeInfoEmission.Never,
            Purpose = SerializationPurpose.Inspection,
        };
        switch (outputType)
        {
            case OutputType.Json:
                var json = ModelJsonSerializer.Serialize(value, options);
                textWriter.WriteLine(ToColorizedJsonString(json));
                break;
            case OutputType.Yaml:
                var yaml = ModelYamlSerializer.Serialize(value, options);
                textWriter.WriteLine(ToColorizedYamlString(yaml));
                break;
            case OutputType.Hex:
                var bytes = ModelSerializer.Serialize(value, options);
                textWriter.WriteLine(ByteUtility.Hex(bytes));
                break;
            default:
                throw new NotSupportedException($"Unsupported output type: {outputType}");
        }
    }

    public static string ToColorizedJsonString(string json)
    {
        if (IsJQSupported)
        {
            using var tempFile = TempFile.WriteAllText(json);
            var process = new Process();
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.FileName = $"jq";
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.Arguments = $". -C \"{tempFile.FileName}\"";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.Start();
            var s = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return s;
        }

        return json;
    }

    public static string ToColorizedYamlString(string yaml)
    {
        if (IsYQSupported)
        {
            using var tempFile = TempFile.WriteAllText(yaml);
            var process = new Process();
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.FileName = $"yq";
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.Arguments = $"-e --colors \"{tempFile.FileName}\"";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.Start();
            var s = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return s;
        }

        return yaml;
    }

    private static bool SupportsJQ()
    {
        try
        {
            if (OperatingSystem.IsMacOS() && !Console.IsOutputRedirected)
            {
                var process = new Process();
                process.StartInfo.FileName = $"which";
                process.StartInfo.Arguments = "jq";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.OutputDataReceived += (s, e) => { };
                process.ErrorDataReceived += (s, e) => { };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                return process.ExitCode == 0;
            }
        }
        catch
        {
            // ignored
        }

        return false;
    }

    private static bool SupportsYQ()
    {
        try
        {
            if (OperatingSystem.IsMacOS() && !Console.IsOutputRedirected)
            {
                var process = new Process();
                process.StartInfo.FileName = "which";
                process.StartInfo.Arguments = "yq";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.OutputDataReceived += (s, e) => { };
                process.ErrorDataReceived += (s, e) => { };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                return process.ExitCode == 0;
            }
        }
        catch
        {
            // ignored
        }

        return false;
    }
}
