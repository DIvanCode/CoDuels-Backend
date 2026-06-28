using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Duely.Infrastructure.Gateway.Client.Abstracts;

public sealed class DuelStartedMessage : Message
{
    public DuelStartedMessage(int duelId)
    {
        DuelId = duelId;
    }
    
    [JsonPropertyName("duel_id")]
    public int DuelId { get; init; }
    
    // ReSharper disable once UnusedMember.Local
#pragma warning disable CS8618, CS9264
    /// <summary>
    /// EF constructor. Do not use explicitly!
    /// </summary>
    [Obsolete(message: "For EF. Do not use explicitly!", error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    private DuelStartedMessage()
    {
    }
#pragma warning restore CS8618, CS9264
}
