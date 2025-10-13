namespace Duely.Application.Configuration;

public class DuelSettings
{
    public const string SectionName = "DuelSettings";
    public int CheckPairInterval { get; set; }
    public int MaxDurationMinutes { get; set; }
    public int CheckFinishInterval { get; set; }
    public int SsePingIntervalMs { get; set; }
    public int UserIdCookieDays { get; set; }
}
