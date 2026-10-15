namespace Arclight.Core.Interfaces;

/// <summary>A running process as seen by the playtime tracker. Only names and image paths are read.</summary>
public sealed record RunningProcess(int ProcessId, string Name, string? ImagePath);

public interface IProcessSnapshotProvider
{
    IReadOnlyList<RunningProcess> GetRunningProcesses();
}
