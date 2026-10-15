using System.Diagnostics;
using System.Runtime.InteropServices;
using Arclight.Core.Interfaces;

namespace Arclight.Platforms.Windows;

/// <summary>
/// Lists running processes with their image paths using PROCESS_QUERY_LIMITED_INFORMATION, which
/// works without administrator rights for normal user processes. Protected processes are skipped.
/// </summary>
public sealed class WindowsProcessSnapshotProvider : IProcessSnapshotProvider
{
    private const uint ProcessQueryLimitedInformation = 0x1000;

    public IReadOnlyList<RunningProcess> GetRunningProcesses()
    {
        var result = new List<RunningProcess>(256);
        var buffer = new char[1024];
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (process.Id is 0 or 4)
                {
                    continue;
                }

                result.Add(new RunningProcess(process.Id, process.ProcessName, TryGetImagePath(process.Id, buffer)));
            }
        }

        return result;
    }

    internal static string? TryGetImagePath(int processId, char[] buffer)
    {
        var handle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var size = buffer.Length;
            return QueryFullProcessImageName(handle, 0, buffer, ref size) ? new string(buffer, 0, size) : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "QueryFullProcessImageNameW")]
    private static extern bool QueryFullProcessImageName(IntPtr process, int flags, [Out] char[] exeName, ref int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
