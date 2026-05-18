using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using RhMcp.Integration.Tests.Harness;

namespace RhMcp.Integration.Tests;

// Exercises the adoption path: when a user-started Rhino drops an announcement
// file in the listeners dir, the router should adopt it as a slot with
// `adopted=true`. close_slot on an adopted slot must refuse to kill the process
// and return a structured `cannot_close_adopted` payload.
//
// No real Rhino required — we fake the announcement by spinning up a TcpListener
// on the announced port (so IsPortListening returns true) and using the test
// process's own pid (so IsProcessAlive returns true). Adopted close-paths bail
// out before any kill, so reusing our own pid is safe.
[TestFixture]
internal sealed class AdoptedSlotTests : RouterFixture
{
    [Test]
    public async Task announcement_with_listening_port_is_adopted_on_list_slots()
    {
        using FakeListener listener = FakeListener.Start();
        DropAnnouncement(version: "8", port: listener.Port, pid: Environment.ProcessId);

        string json = await _router.CallToolTextAsync("list_slots");
        JsonElement slot = JsonAssert.Parse(json)[0];

        Assert.That(JsonAssert.Parse(json).GetArrayLength(), Is.EqualTo(1));
        Assert.That(slot, Json.HasProperty("adopted", Is.True));
        Assert.That(slot, Json.HasProperty("version", Is.EqualTo("8")));
        Assert.That(slot, Json.HasProperty("port", Is.EqualTo(listener.Port)));
        Assert.That(slot, Json.HasProperty("pid", Is.EqualTo(Environment.ProcessId)));
        Assert.That(slot, Json.HasProperty("slotId", Is.Not.Empty));
    }

    [Test]
    public async Task close_slot_on_adopted_slot_returns_cannot_close_adopted()
    {
        using FakeListener listener = FakeListener.Start();
        DropAnnouncement(version: "WIP", port: listener.Port, pid: Environment.ProcessId);

        string listJson = await _router.CallToolTextAsync("list_slots");
        string slotId = JsonAssert.Parse(listJson)[0].GetProperty("slotId").GetString()!;

        string closeJson = await _router.CallToolTextAsync("close_slot", Args.Of(("slot", slotId)));

        Assert.That(closeJson, Json.HasProperty("closed", Is.False));
        Assert.That(closeJson, Json.HasProperty("error", Is.EqualTo("cannot_close_adopted")));
        Assert.That(closeJson, Json.HasProperty("message", Does.Contain("Rhino window")));

        // The slot must still be present after a refused close — adoption is
        // sticky until the listener actually dies.
        string listAgain = await _router.CallToolTextAsync("list_slots");
        Assert.That(listAgain, Json.IsArrayOfLength(1));
    }

    [Test]
    public async Task duplicate_announcement_for_same_pid_port_does_not_create_two_slots()
    {
        using FakeListener listener = FakeListener.Start();
        DropAnnouncement(version: "8", port: listener.Port, pid: Environment.ProcessId);

        // First list_slots adopts; the file is then deleted by the router.
        _ = await _router.CallToolTextAsync("list_slots");

        // Plugin races and drops the same announcement again before noticing the
        // first one was already consumed. The (pid, port) dedupe in AdoptIfNew
        // must reject it.
        DropAnnouncement(version: "8", port: listener.Port, pid: Environment.ProcessId);
        string json = await _router.CallToolTextAsync("list_slots");

        Assert.That(json, Json.IsArrayOfLength(1));
    }

    [Test]
    public async Task announcement_with_dead_port_is_discarded_without_adoption()
    {
        // Pick a port, bind+release it so we know nothing is listening there.
        int deadPort = AllocateUnboundPort();
        DropAnnouncement(version: "8", port: deadPort, pid: Environment.ProcessId);

        string json = await _router.CallToolTextAsync("list_slots");
        Assert.That(json, Json.IsArrayOfLength(0));

        // And the doorbell file should have been deleted by the scan.
        Assert.That(Directory.GetFiles(_router.ListenersDir, "*.json"), Is.Empty);
    }

    private void DropAnnouncement(string version, int port, int pid)
    {
        Directory.CreateDirectory(_router.ListenersDir);
        string path = Path.Combine(_router.ListenersDir, $"ann-{Guid.NewGuid():N}.json");
        string body = JsonSerializer.Serialize(new
        {
            v = 1,
            pid,
            port,
            version,
        });
        File.WriteAllText(path, body);
    }

    private static int AllocateUnboundPort()
    {
        TcpListener probe = new(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private sealed class FakeListener : IDisposable
    {
        private readonly TcpListener _listener;
        public int Port { get; }

        private FakeListener(TcpListener listener, int port)
        {
            _listener = listener;
            Port = port;
        }

        public static FakeListener Start()
        {
            TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            return new FakeListener(listener, port);
        }

        public void Dispose()
        {
            try { _listener.Stop(); } catch { /* best effort */ }
        }
    }
}
