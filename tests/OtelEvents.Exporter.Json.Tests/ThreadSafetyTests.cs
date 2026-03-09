using System.Text;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace OtelEvents.Exporter.Json.Tests;

/// <summary>
/// Tests for thread safety: Monitor.TryEnter with configurable timeout,
/// batch dropping on timeout, and I/O failure handling.
/// </summary>
public sealed class ThreadSafetyTests
{
    [Fact]
    public void Export_LockTimeout_DropsBatchAndReturnsSuccess()
    {
        // Use a very short lock timeout to make the test fast
        var options = new OtelEventsJsonExporterOptions { LockTimeout = TimeSpan.FromMilliseconds(1) };
        var stream = new MemoryStream();
        var exporter = new OtelEventsJsonExporter(options, stream);

        var lr = TestExporterHarness.CreateLogRecord(eventName: "test.event", message: "msg");

        // Acquire the lock on the same _lock object via reflection
        var lockField = typeof(OtelEventsJsonExporter)
            .GetField("_lock", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var lockObj = lockField.GetValue(exporter)!;

        ExportResult result;
        lock (lockObj)
        {
            // With the lock held, the exporter should time out and drop the batch
            var batch = new Batch<LogRecord>([lr], 1);
            result = exporter.Export(batch);
        }

        // Lock timeout should return Success (backpressure, not failure)
        Assert.Equal(ExportResult.Success, result);

        exporter.Dispose();
    }

    [Fact]
    public void Export_IOException_ReturnsFailure()
    {
        var failingStream = new FailingStream();
        var options = new OtelEventsJsonExporterOptions();
        var exporter = new OtelEventsJsonExporter(options, failingStream);

        var lr = TestExporterHarness.CreateLogRecord(eventName: "test.event", message: "msg");
        var batch = new Batch<LogRecord>([lr], 1);

        // Enable failure mode so the stream fails on Flush (which Export calls after writing)
        failingStream.ShouldFail = true;
        var result = exporter.Export(batch);

        Assert.Equal(ExportResult.Failure, result);

        // Disable failure so Dispose succeeds
        failingStream.ShouldFail = false;
        exporter.Dispose();
    }

    [Fact]
    public async Task Export_ConcurrentBatches_AllRecordsWritten()
    {
        var stream = new MemoryStream();
        var options = new OtelEventsJsonExporterOptions { LockTimeout = TimeSpan.FromSeconds(5) };
        var exporter = new OtelEventsJsonExporter(options, stream);

        const int threadCount = 4;
        const int recordsPerThread = 25;
        var barrier = new Barrier(threadCount);
        var tasks = new Task[threadCount];

        for (int t = 0; t < threadCount; t++)
        {
            var threadId = t;
            tasks[t] = Task.Run(() =>
            {
                barrier.SignalAndWait();
                for (int r = 0; r < recordsPerThread; r++)
                {
                    var lr = TestExporterHarness.CreateLogRecord(
                        eventName: $"thread{threadId}.record{r}",
                        message: $"Thread {threadId} Record {r}");
                    var batch = new Batch<LogRecord>([lr], 1);
                    exporter.Export(batch);
                }
            });
        }

        await Task.WhenAll(tasks);

        stream.Position = 0;
        var output = Encoding.UTF8.GetString(stream.ToArray());
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // All records should be written (threads wait 5s for lock, so no drops)
        Assert.Equal(threadCount * recordsPerThread, lines.Length);

        exporter.Dispose();
    }

    /// <summary>
    /// A stream that throws IOException on flush when <see cref="ShouldFail"/> is true.
    /// </summary>
    private sealed class FailingStream : Stream
    {
        public bool ShouldFail { get; set; }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => 0;
        public override long Position { get; set; }

        public override void Flush()
        {
            if (ShouldFail) throw new IOException("Simulated I/O failure");
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count)
        {
            // Allow writes — buffer in StreamWriter; Flush will fail when ShouldFail is true
        }
    }
}
