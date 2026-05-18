using System.Text.Json;
using RhMcp.Integration.Tests.Harness;

namespace RhMcp.Integration.Tests;

// Pins down the Mac-specific shared-process contract:
//   On macOS, Rhino is single-instance per bundle id, so multiple slots for the
//   same version share one OS process and each get a private listener port.
//   The first slot launches Rhino.app; subsequent slots ask the existing
//   listener to spawn another doc + port via _router_spawn_listener.
//
// Marked [Explicit] because it requires a real Rhino install. [Platform(MacOSX)]
// keeps the case from running on Windows where the contract is the opposite.
[TestFixture]
[Explicit("Spawns real Rhino on macOS; opt in with --filter \"Category=RequiresRhino\".")]
[Category("RequiresRhino")]
[Platform("MacOSX")]
public sealed class MacSharedProcessTests : RouterFixture
{
    [Test]
    public async Task two_slots_same_version_share_pid_and_use_distinct_ports()
    {
        _ = await _router.CallToolTextAsync("spawn_slot", Args.Of(("version", "8")));
        _ = await _router.CallToolTextAsync("spawn_slot", Args.Of(("version", "8")));

        string json = await _router.CallToolTextAsync("list_slots");
        Assert.That(json, Json.IsArrayOfLength(2));

        List<JsonElement> slots = JsonAssert.Parse(json).EnumerateArray().ToList();
        HashSet<int> pids = slots.Select(s => s.GetProperty("pid").GetInt32()).ToHashSet();
        HashSet<int> ports = slots.Select(s => s.GetProperty("port").GetInt32()).ToHashSet();

        Assert.That(pids, Has.Count.EqualTo(1), "Mac slots for the same version must share one Rhino process.");
        Assert.That(ports, Has.Count.EqualTo(2), "Each Mac sibling slot must have its own listener port.");
    }

    [Test]
    public async Task closing_first_of_two_mac_siblings_keeps_rhino_alive()
    {
        string spawnA = await _router.CallToolTextAsync("spawn_slot", Args.Of(("version", "8")));
        string spawnB = await _router.CallToolTextAsync("spawn_slot", Args.Of(("version", "8")));
        string slotA = JsonAssert.Parse(spawnA).GetProperty("slotId").GetString()!;
        string slotB = JsonAssert.Parse(spawnB).GetProperty("slotId").GetString()!;
        int sharedPid = JsonAssert.Parse(spawnA).GetProperty("pid").GetInt32();

        string closeJson = await _router.CallToolTextAsync("close_slot", Args.Of(("slot", slotA)));
        Assert.That(closeJson, Json.HasProperty("closed", Is.True));

        // Slot B must still be in the registry and its pid (which was shared
        // with A) must still be alive — the listener for A was closed via the
        // control channel, but the Rhino process keeps running for B.
        string listJson = await _router.CallToolTextAsync("list_slots");
        Assert.That(listJson, Json.IsArrayOfLength(1));

        JsonElement remaining = JsonAssert.Parse(listJson)[0];
        Assert.That(remaining, Json.HasProperty("slotId", Is.EqualTo(slotB)));
        Assert.That(remaining, Json.HasProperty("pid", Is.EqualTo(sharedPid)));
        Assert.That(IsProcessAlive(sharedPid), Is.True, "Shared Rhino process must outlive close_slot on a sibling.");
    }

    [Test]
    public async Task closing_last_mac_sibling_terminates_rhino()
    {
        string spawnA = await _router.CallToolTextAsync("spawn_slot", Args.Of(("version", "8")));
        string spawnB = await _router.CallToolTextAsync("spawn_slot", Args.Of(("version", "8")));
        string slotA = JsonAssert.Parse(spawnA).GetProperty("slotId").GetString()!;
        string slotB = JsonAssert.Parse(spawnB).GetProperty("slotId").GetString()!;
        int sharedPid = JsonAssert.Parse(spawnA).GetProperty("pid").GetInt32();

        _ = await _router.CallToolTextAsync("close_slot", Args.Of(("slot", slotA)));
        _ = await _router.CallToolTextAsync("close_slot", Args.Of(("slot", slotB)));

        Assert.That(IsProcessAlive(sharedPid), Is.False, "Closing the last sibling must terminate the shared Rhino.");

        string listJson = await _router.CallToolTextAsync("list_slots");
        Assert.That(listJson, Json.IsArrayOfLength(0));
    }

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            using System.Diagnostics.Process p = System.Diagnostics.Process.GetProcessById(pid);
            return !p.HasExited;
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
    }
}
