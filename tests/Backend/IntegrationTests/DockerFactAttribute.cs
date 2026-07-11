using System.Diagnostics;
using Xunit;

namespace Threadia.IntegrationTests;

/// <summary>Docker デーモンが利用できない環境ではテストをスキップする。</summary>
public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        if (!DockerAvailability.IsAvailable)
        {
            Skip = "Docker が利用できないためスキップします。";
        }
    }
}

public static class DockerAvailability
{
    public static readonly bool IsAvailable = Check();

    private static bool Check()
    {
        try
        {
            var startInfo = new ProcessStartInfo("docker", "info")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            using var process = Process.Start(startInfo);
            return process is not null && process.WaitForExit(15_000) && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
