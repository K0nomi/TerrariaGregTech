using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;

// Reads a .nettrace captured with --profile gc-verbose and reports allocations
//
// usage: dotnet run -- <path-to.nettrace>

string path = args.Length > 0 ? args[0] : throw new ArgumentException("pass .nettrace path");

var byType  = new Dictionary<string, (long bytes, long count)>();
var byMethod = new Dictionary<string, long>();
long total = 0;

string etlx = TraceLog.CreateFromEventPipeDataFile(path);
using var log = new TraceLog(etlx);

foreach (var data in log.Events)
{
    if (data is not GCAllocationTickTraceData tick) continue;
    long amt = tick.AllocationAmount64;
    if (amt <= 0) amt = 100_000;
    total += amt;

    string type = tick.TypeName ?? "(null)";
    byType.TryGetValue(type, out var v);
    byType[type] = (v.bytes + amt, v.count + 1);

    var cs = data.CallStack();
    string method = "(no stack)";
    while (cs != null)
    {
        string full = cs.CodeAddress.FullMethodName;
        if (!string.IsNullOrEmpty(full)
            && !full.StartsWith("System.")
            && !full.StartsWith("Microsoft.")
            && !full.Contains("!?"))
        {
            method = full;
            break;
        }
        method = full ?? method;
        cs = cs.Caller;
    }
    byMethod[method] = byMethod.TryGetValue(method, out var mbv) ? mbv + amt : amt;
}

double mb(long b) => b / (1024.0 * 1024.0);
Console.WriteLine($"\n==== total sampled alloc: {mb(total):N0} MB across the trace ====\n");

Console.WriteLine("=== TOP ALLOCATION BY TYPE ===");
foreach (var kv in byType.OrderByDescending(k => k.Value.bytes).Take(30))
    Console.WriteLine($"  {mb(kv.Value.bytes),10:N1} MB  ({kv.Value.count,7:N0} ticks)  {kv.Key}");

Console.WriteLine("\n=== TOP ALLOCATION BY ALLOCATING METHOD (first non-runtime frame) ===");
foreach (var kv in byMethod.OrderByDescending(k => k.Value).Take(30))
    Console.WriteLine($"  {mb(kv.Value),10:N1} MB  {kv.Key}");
