using System.Text.Json;
using RhMcp.Integration.Tests.Harness;

namespace RhMcp.Integration.Tests;

// Pins down the Windows-specific contract: every slot gets its own OS process,
// regardless of version. This is the inverse of MacSharedProcessTests and is
// the simpler invariant to verify — if either test ever fails, the shared-vs-
// isolated branch in RhinoManager has drifted.
//
// Marked [Explicit] because it requires a real Rhino install. [Platform(Win)]
// excludes the case from macOS where the contract is the opposite.
[TestFixture]
[Explicit("Spawns real Rhino on Windows; opt in with --filter \"Category=RequiresRhino\".")]
[Category("RequiresRhino")]
[Platform("Win")]
public sealed class WindowsProcessIsolationTests : RouterFixture
{
    [Test]
    public async Task two_slots_same_version_have_distinct_pids_and_ports()
    {
        _ = await _router.CallToolTextAsync("spawn_slot", Args.Of(("version", "8")));
        _ = await _router.CallToolTextAsync("spawn_slot", Args.Of(("version", "8")));

        string json = await _router.CallToolTextAsync("list_slots");
        Assert.That(json, Json.IsArrayOfLength(2));

        List<JsonElement> slots = JsonAssert.Parse(json).EnumerateArray().ToList();
        HashSet<int> pids = slots.Select(s => s.GetProperty("pid").GetInt32()).ToHashSet();
        HashSet<int> ports = slots.Select(s => s.GetProperty("port").GetInt32()).ToHashSet();

        Assert.That(pids, Has.Count.EqualTo(2), "Windows slots must be backed by distinct Rhino processes.");
        Assert.That(ports, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task closing_one_slot_does_not_kill_the_other()
    {
        string spawnA = await _router.CallToolTextAsync("spawn_slot", Args.Of(("version", "8")));
        string spawnB = await _router.CallToolTextAsync("spawn_slot", Args.Of(("version", "8")));
        string slotA = JsonAssert.Parse(spawnA).GetProperty("slotId").GetString()!;
        string slotB = JsonAssert.Parse(spawnB).GetProperty("slotId").GetString()!;
        int pidB = JsonAssert.Parse(spawnB).GetProperty("pid").GetInt32();

        string closeJson = await _router.CallToolTextAsync("close_slot", Args.Of(("slot", slotA)));
        Assert.That(closeJson, Json.HasProperty("closed", Is.True));

        string listJson = await _router.CallToolTextAsync("list_slots");
        Assert.That(listJson, Json.IsArrayOfLength(1));

        JsonElement remaining = JsonAssert.Parse(listJson)[0];
        Assert.That(remaining, Json.HasProperty("slotId", Is.EqualTo(slotB)));
        Assert.That(IsProcessAlive(pidB), Is.True, "Sibling slot's Rhino must outlive close_slot on a peer.");
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
