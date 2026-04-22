namespace DjiActionDbMergeTool.Core;

public class MergeProgress
{
    public int Current { get; init; }
    public int Total { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool IsCompleted { get; init; }
    public bool HasError { get; init; }
    public string? ErrorMessage { get; init; }

    public double Percentage => Total > 0 ? (double)Current / Total * 100 : 0;
}
