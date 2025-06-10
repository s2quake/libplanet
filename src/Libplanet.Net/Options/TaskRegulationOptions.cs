namespace Libplanet.Net.Options;

public sealed record class TaskRegulationOptions
{
    public int MaxTransferBlocksTaskCount { get; set; } = 0;

    public int MaxTransferTxsTaskCount { get; set; } = 0;
}
