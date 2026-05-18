using RhMcp.Integration.Tests.Harness;

namespace RhMcp.Integration.Tests;

// Exercises spawn_slot end-to-end: the router must launch a real Rhino,
// receive its listener announcement, and return the slot metadata. Marked
// [Explicit] because it requires a working Rhino install + freshly-built
// plugin. Opt in with --filter "Category=RequiresRhino".
[TestFixture]
[Explicit("Spawns a real Rhino; opt in with --filter \"Category=RequiresRhino\".")]
[Category("RequiresRhino")]
internal sealed class SpawnSlotTests : SharedRouterFixture
{

    [TestCase("8")]
    [TestCase("9")] // TODO : Fails
    [TestCase("WIP")]
    public async Task spawn_slot_returns_slot_metadata(string version)
    {
        string spawnJson = await _router.CallToolTextAsync("spawn_slot", Args.Of(("version", version)));

        Assert.That(spawnJson, Json.HasProperty("slotId", Is.Not.Empty));
        Assert.That(spawnJson, Json.HasProperty("version", Is.EqualTo(version)));
        Assert.That(spawnJson, Json.HasProperty("adopted", Is.False));
    }

    [Test]
    public async Task spawn_three_slots_returns_distinct_metadata()
    {
        string json_1 = await _router.CallToolTextAsync("spawn_slot", Args.Of(("version", "8")));
        string json_2 = await _router.CallToolTextAsync("spawn_slot", Args.Of(("version", "8")));
        string json_3 = await _router.CallToolTextAsync("spawn_slot", Args.Of(("version", "8")));

        foreach (string json in new[] { json_1, json_2, json_3 })
        {
            Assert.That(json, Json.HasProperty("slotId", Is.Not.Empty));
            Assert.That(json, Json.HasProperty("version", Is.EqualTo("8")));
            Assert.That(json, Json.HasProperty("adopted", Is.False));
        }
    }

    // Round-trip: spawn produces a slotId that close_slot will accept.
    [Test]
    public async Task spawn_then_close_slot_round_trip()
    {
        string openJson = await _router.CallToolTextAsync("spawn_slot", Args.Of(("version", "8")));

        Assert.That(openJson, Json.HasProperty("slotId", Is.Not.Empty));
        Assert.That(openJson, Json.HasProperty("version", Is.EqualTo("8")));
        Assert.That(openJson, Json.HasProperty("adopted", Is.False));

        string slotId = JsonAssert.Parse(openJson).GetProperty("slotId").GetString()!;

        string closeJson = await _router.CallToolTextAsync("close_slot", Args.Of(("slot", slotId)));
        Assert.That(closeJson, Json.HasProperty("closed", Is.True));
    }
}
