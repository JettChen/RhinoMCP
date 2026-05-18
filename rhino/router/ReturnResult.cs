using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace RhMcp.Router;

// Unified envelope every router tool call returns. The MCP SDK delivers a
// single string to the agent per call, so we serialize this record into that
// string.
public sealed record ReturnResult(
    [property: JsonPropertyName("payload")] JsonNode? Payload,
    [property: JsonPropertyName("error")] string? Error = null,
    [property: JsonPropertyName("autoSpawnedSlot")] SlotInfo? AutoSpawnedSlot = null)
{
    public string AsJson => JsonSerializer.Serialize(this, RouterJsonContext.Default.ReturnResult);
}

// Side-effect descriptor for an auto-spawn the router performed on the agent's
// behalf. `reason` is a sentence the agent can surface to the user verbatim
// (e.g. "Auto-spawned Rhino WIP because 'g2_search_components' requires it.").
public sealed record SlotInfo(
    [property: JsonPropertyName("slotId")] string SlotId,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("reason")] string Reason);
