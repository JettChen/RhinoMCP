using System.Text.Json;
using RhMcp.Integration.Tests.Harness;

namespace RhMcp.Integration.Tests;

// Pins down the JSON shape list_slots returns post-spawn / post-close. The
// existing ListSlotsTests fixture only asserts the empty case; this one
// asserts the populated case and the close-removes path. Requires a real
// Rhino install.
[TestFixture]
[Explicit("Spawns a real Rhino; opt in with --filter \"Category=RequiresRhino\".")]
[Category("RequiresRhino")]
public sealed class ListSlotsShapeTests : RouterFixture
{
    [Test]
    public async Task list_slots_after_spawn_contains_expected_fields()
    {
        string spawnJson = await _router.CallToolTextAsync("spawn_slot", Args.Of(("version", "8")));
        JsonElement spawn = JsonAssert.Parse(spawnJson);
        string slotId = spawn.GetProperty("slotId").GetString()!;
        int port = spawn.GetProperty("port").GetInt32();

        string listJson = await _router.CallToolTextAsync("list_slots");
        Assert.That(listJson, Json.IsArrayOfLength(1));

        JsonElement slot = JsonAssert.Parse(listJson)[0];
        Assert.That(slot, Json.HasProperty("slotId", Is.EqualTo(slotId)));
        Assert.That(slot, Json.HasProperty("port", Is.EqualTo(port)));
        Assert.That(slot, Json.HasProperty("version", Is.EqualTo("8")));
        Assert.That(slot, Json.HasProperty("adopted", Is.False));
        Assert.That(slot, Json.HasProperty("pid", Is.GreaterThan(0)));
        Assert.That(slot, Json.HasProperty("endpoint", Is.EqualTo($"http://localhost:{port}")));
    }

    [Test]
    public async Task list_slots_after_close_does_not_include_closed_slot()
    {
        string spawnJson = await _router.CallToolTextAsync("spawn_slot", Args.Of(("version", "8")));
        string slotId = JsonAssert.Parse(spawnJson).GetProperty("slotId").GetString()!;

        string closeJson = await _router.CallToolTextAsync("close_slot", Args.Of(("slot", slotId)));
        Assert.That(closeJson, Json.HasProperty("closed", Is.True));

        string listJson = await _router.CallToolTextAsync("list_slots");
        Assert.That(listJson, Json.IsArrayOfLength(0));
    }

    [Test]
    public async Task close_slot_twice_returns_slot_not_found_on_second_call()
    {
        string spawnJson = await _router.CallToolTextAsync("spawn_slot", Args.Of(("version", "8")));
        string slotId = JsonAssert.Parse(spawnJson).GetProperty("slotId").GetString()!;

        _ = await _router.CallToolTextAsync("close_slot", Args.Of(("slot", slotId)));

        string secondClose = await _router.CallToolTextAsync("close_slot", Args.Of(("slot", slotId)));
        Assert.That(secondClose, Json.HasProperty("closed", Is.False));
        Assert.That(secondClose, Json.HasProperty("error", Is.EqualTo("slot_not_found")));
    }
}
