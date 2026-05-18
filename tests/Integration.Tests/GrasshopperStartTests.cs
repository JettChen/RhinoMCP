using System.Text.Json;
using RhMcp.Integration.Tests.Harness;

namespace RhMcp.Integration.Tests;

// Exercises g1_start / g2_start: starting Grasshopper inside a spawned Rhino
// should produce its own slot entry alongside the parent Rhino slot. Marked
// [Explicit] because it requires a real Rhino install.
[TestFixture]
[Explicit("Spawns a real Rhino + Grasshopper; opt in with --filter \"Category=RequiresRhino\".")]
[Category("RequiresRhino")]
public sealed class GrasshopperStartTests : SharedRouterFixture
{

    [Test]
    public async Task g2_start_in_rhino_8_produces_distinct_slot()
    {
        _ = await _router.CallToolTextAsync("spawn_slot", Args.Of(("version", "8")));

        string gh2Json = await _router.CallToolTextAsync("g2_start");
        Assert.That(gh2Json, Does.Contain("Opened G2").IgnoreCase);

        string slotJson = await _router.CallToolTextAsync("list_slots");
        Assert.That(slotJson, Json.IsArrayOfLength(2));

        List<JsonElement> slots = JsonAssert.Parse(slotJson).EnumerateArray().ToList();
        foreach (string property in new[] { "slotId", "port", "endpoint" })
        {
            HashSet<string> set = slots.Select(j => j.GetProperty(property).ToString().ToLowerInvariant()).ToHashSet();
            Assert.That(set, Has.Count.EqualTo(2));
        }
    }

    // g2_start with no Rhino already spawned should auto-spawn a host. Today
    // that path picks Rhino WIP — the assertion mirrors that contract.
    [Test]
    public async Task g2_start_with_no_host_spawns_one_slot()
    {
        string gh2Json = await _router.CallToolTextAsync("g2_start");
        Assert.That(gh2Json, Does.Contain("Opened G2").IgnoreCase);

        string slotJson = await _router.CallToolTextAsync("list_slots");
        Assert.That(slotJson, Json.IsArrayOfLength(1));

        // TODO : Assert that current slot is Rhino WIP
    }

    [TestCase("8")]
    [TestCase("WIP")]
    public async Task g1_start_inside_rhino_opens_grasshopper(string version)
    {
        _ = await _router.CallToolTextAsync("spawn_slot", Args.Of(("version", version)));

        string gh1Json = await _router.CallToolTextAsync("g1_start");
        Assert.That(gh1Json, Does.Contain("Opened Grasshopper").IgnoreCase);
    }
}
