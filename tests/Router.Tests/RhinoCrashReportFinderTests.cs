using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging.Abstractions;
using RhMcp.Router;
using Xunit;

namespace RhMcp.Router.Tests;

public class RhinoCrashReportFinderTests
{
    // Hand-crafted minidump exercises the Windows parser without needing a real
    // Rhino crash on disk. Verifies pid extraction (from the MiscInfo stream)
    // and exception-code → Signal/Termination mapping. Drops the file into the
    // WER LocalDumps path that TryFindWindows scans, so the test also covers
    // the directory-discovery half of the code path.
    [Fact]
    public void TryFind_on_windows_parses_synthetic_minidump()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        const int FakePid = 4242;
        const uint AccessViolation = 0xC0000005;

        string werDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CrashDumps");
        Directory.CreateDirectory(werDir);
        string dumpPath = Path.Combine(werDir, $"Rhino.exe.{FakePid}.synthetic-{Guid.NewGuid():N}.dmp");

        try
        {
            File.WriteAllBytes(dumpPath, BuildMinidump(FakePid, AccessViolation));

            var finder = new RhinoCrashReportFinder(NullLogger<RhinoCrashReportFinder>.Instance);
            var report = finder.TryFind(FakePid);

            Assert.NotNull(report);
            Assert.Equal(dumpPath, report.Path);
            Assert.Equal("0xC0000005", report.Signal);
            Assert.Equal("EXCEPTION_ACCESS_VIOLATION", report.Termination);
            Assert.NotNull(report.CaptureTime);
            Assert.Empty(report.ManagedFrames);
            Assert.Empty(report.TopFrames);
        }
        finally
        {
            try { File.Delete(dumpPath); } catch { }
        }
    }

    // Minimal minidump: header + 2-entry directory (Exception @ stream type 6,
    // MiscInfo @ stream type 15). Parser only reads the leading bytes of each
    // stream, so the streams don't need to be fully realistic.
    private static byte[] BuildMinidump(int pid, uint exceptionCode)
    {
        const int HeaderSize = 32;
        const int DirEntrySize = 12;
        const int ExceptionStreamSize = 32;
        const int MiscInfoStreamSize = 12;

        int dirOffset = HeaderSize;
        int exceptionOffset = dirOffset + DirEntrySize * 2;
        int miscOffset = exceptionOffset + ExceptionStreamSize;
        int totalSize = miscOffset + MiscInfoStreamSize;

        byte[] buf = new byte[totalSize];
        using var ms = new MemoryStream(buf);
        using var bw = new BinaryWriter(ms);

        bw.Write(0x504D444Du); // Signature 'MDMP'
        bw.Write(0x0000A793u); // Version (low 16 bits)
        bw.Write(2u);          // NumberOfStreams
        bw.Write((uint)dirOffset);
        bw.Write(0u);          // CheckSum
        bw.Write((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        bw.Write(0UL);         // Flags

        bw.Write(6u);                            // StreamType: ExceptionStream
        bw.Write((uint)ExceptionStreamSize);
        bw.Write((uint)exceptionOffset);
        bw.Write(15u);                           // StreamType: MiscInfoStream
        bw.Write((uint)MiscInfoStreamSize);
        bw.Write((uint)miscOffset);

        bw.Write(100u);          // ThreadId
        bw.Write(0u);            // __alignment
        bw.Write(exceptionCode);
        bw.Write(0u);            // ExceptionFlags
        bw.Write(0UL);           // ExceptionRecord
        bw.Write(0xDEADBEEFUL);  // ExceptionAddress

        bw.Write((uint)MiscInfoStreamSize);
        bw.Write(1u);            // Flags1: MINIDUMP_MISC1_PROCESS_ID
        bw.Write((uint)pid);

        return buf;
    }


    // Smoke test against the user's actual macOS crash reports directory if it
    // exists and has at least one Rhino .ips. Skipped on Windows / when there
    // are no reports — the parser's correctness is what we're checking, not
    // that crashes have happened.
    //
    // Pid-match path has no time window, so we can verify parsing against any
    // historical .ips by reading its pid from the file directly and asking the
    // finder to look it up.
    [Fact]
    public void TryFind_by_pid_parses_real_ips_when_available()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "Logs", "DiagnosticReports");
        if (!Directory.Exists(dir)) return;
        var ips = Directory.GetFiles(dir, "Rhinoceros-*.ips")
            .OrderByDescending(p => new FileInfo(p).LastWriteTimeUtc)
            .FirstOrDefault();
        if (ips is null) return;

        // Sniff pid out of the body JSON ourselves so we can hand it to the finder.
        var text = File.ReadAllText(ips);
        var nl = text.IndexOf('\n');
        if (nl < 0) return;
        using var doc = System.Text.Json.JsonDocument.Parse(text[(nl + 1)..]);
        if (!doc.RootElement.TryGetProperty("pid", out var pidEl) || !pidEl.TryGetInt32(out var pid)) return;

        var finder = new RhinoCrashReportFinder(NullLogger<RhinoCrashReportFinder>.Instance);
        var report = finder.TryFind(pid);

        Assert.NotNull(report);
        Assert.Equal(ips, report.Path);
        Assert.NotEmpty(report.TopFrames);
        Assert.NotNull(report.Signal); // every macOS crash report has one

        // ManagedException is optional (older .ips, non-managed crashes), but
        // when present it must come with at least one managed frame — otherwise
        // we've extracted a header without the stack it belongs to.
        if (report.ManagedException is not null)
        {
            Assert.NotEmpty(report.ManagedFrames);
            // Every managed frame starts with "at " — sanity check the parse.
            Assert.All(report.ManagedFrames, f => Assert.StartsWith("at ", f));
            // Build-server paths must be stripped — the whole point of the
            // post-process step.
            Assert.DoesNotContain(report.ManagedFrames, f => f.Contains("/Users/bozo/TeamCity"));
        }
    }
}
