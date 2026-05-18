using RhMcp.Integration.Tests.Harness;

namespace RhMcp.Integration.Tests;

// Pins down slot-routing semantics for plugin-side tool calls:
//   - An explicit `slot` arg must route to that exact Rhino.
//   - An unknown `slot` arg must produce a structured slot_not_found payload
//     (not a hang, and not a generic MCP error).
//
// Requires a real Rhino install.
[TestFixture]
[Explicit("Spawns real Rhinos; opt in with --filter \"Category=RequiresRhino\".")]
[Category("RequiresRhino")]
public sealed class ToolDispatchBySlotTests : RouterFixture
{
    [Test]
    public async Task explicit_slot_routes_to_correct_rhino()
    {
        string spawnA = await _router.CallToolTextAsync("spawn_slot", Args.Of(("version", "8")));
        string spawnB = await _router.CallToolTextAsync("spawn_slot", Args.Of(("version", "8")));
        string slotA = JsonAssert.Parse(spawnA).GetProperty("slotId").GetString()!;
        string slotB = JsonAssert.Parse(spawnB).GetProperty("slotId").GetString()!;

        // Drop three lines into slot A; leave slot B untouched.
        _ = await _router.CallToolTextAsync("run_python", Args.Of(
            ("slot", (object?)slotA),
            ("script", """
                from Rhino.Geometry import Point3d, Line
                doc = __rhino_doc__
                for i in range(3):
                    doc.Objects.AddLine(Line(Point3d(i, 0, 0), Point3d(i, 1, 0)))
                """)));

        string listA = await _router.CallToolTextAsync("list_objects", Args.Of(("slot", slotA)));
        string listB = await _router.CallToolTextAsync("list_objects", Args.Of(("slot", slotB)));

        Assert.Multiple((Action)(() =>
        {
            Assert.That(listA, Json.HasProperty("count", Is.EqualTo(3)));
            Assert.That(listB, Json.HasProperty("count", Is.EqualTo(0)));
        }));
    }

    [Test]
    public async Task tool_call_with_unknown_slot_returns_slot_not_found_payload()
    {
        // No spawn — just call a plugin tool with a bogus slot id. The router
        // must short-circuit in the dispatcher with a structured error, not
        // attempt to auto-spawn and not hang.
        string response = await _router.CallToolTextAsync(
            "list_objects",
            Args.Of(("slot", "made-up-slot-xyz")));

        Assert.That(response, Json.HasProperty("error", Is.EqualTo("slot_not_found")));
        Assert.That(response, Json.HasProperty("message", Does.Contain("made-up-slot-xyz")));
    }
}
