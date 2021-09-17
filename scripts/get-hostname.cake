#load "./utility/cake-context-accessor.cake"

using System.Runtime.InteropServices;
using System.Diagnostics;

/// <summary>
/// Sets up the current hostname for use from various tasks
/// </summary>
public static class Hostname
{
    public static string Current => LazyHostname.Value;

    public static string LocalBranchName => Current.ToLower();

    public static string GetHostnameTrackingFilePath() => HostnameTrackingFilePath.Value;

    // Updates and returns path to file used to track changes in hostname
    private readonly static Lazy<string> HostnameTrackingFilePath = new Lazy<string>(() =>
    {
        // Using lockfile helper method so it's stored in temp directory with locks
        var hostnameFilePath = GetLockFilePath("track-hostname.txt");
        var previousHostname = System.IO.File.Exists(hostnameFilePath) ? System.IO.File.ReadAllText(hostnameFilePath) : null;
        var currentHostname = Current;
        if (previousHostname != currentHostname)
        {
            CakeContextAccessor.Context.Verbose($"Updating hostname tracker file. Previous hostname: `{previousHostname}`, current hostname: `{currentHostname}`");
            System.IO.File.WriteAllText(hostnameFilePath, currentHostname);
        }
        return hostnameFilePath;
    });

    private readonly static Lazy<string> LazyHostname = new Lazy<string>(() =>
    {
        var context = CakeContextAccessor.Context;
        var hostname_override = System.Environment.GetEnvironmentVariable("MARVEL_HOSTNAME");
        if (!string.IsNullOrWhiteSpace(hostname_override))
        {
            context.Log.Information($"MARVEL_HOSTNAME detected. Using overridden hostname: {hostname_override}");
            return hostname_override;
        }

        string result = System.Environment.MachineName;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var proc = context.ProcessRunner.Start("scutil", new ProcessSettings { Arguments = "--get LocalHostName", RedirectStandardOutput = true });
            proc.WaitForExit();
            if (proc.GetExitCode() != 0)
            {
                throw new Exception("Unable to determine hostname!");
            }
            var standardOutput = proc.GetStandardOutput().ToList();
            context.Log.Debug($"stdout: \n{String.Join("\n", standardOutput)}");
            result = standardOutput.LastOrDefault();
        }
        context.Log.Debug($"Hostname = {result}");
        return result;
    });
}

Task("get-hostname")
    .Does(() =>
    {
        Information($"Hostname was detected as {Hostname.Current}");
    });