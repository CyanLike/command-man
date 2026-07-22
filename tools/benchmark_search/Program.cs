using System.Diagnostics;

using CommandMan.Core;

var warmup = new CommandSearchEngine();
_ = warmup.Search("ll -a");
_ = warmup.Search("adb");
_ = warmup.Search("kubect");
_ = warmup.Search("监听端口");

Measure("exact+argument (uncached)", 2_000, () => new CommandSearchEngine().Search("ll -a"));
Measure("exact+children (uncached)", 2_000, () => new CommandSearchEngine().Search("adb"));
Measure("prefix (uncached)", 2_000, () => new CommandSearchEngine().Search("kubect"));
Measure("semantic fallback (uncached)", 200, () => new CommandSearchEngine().Search("监听端口"));

var cachedEngine = new CommandSearchEngine();
_ = cachedEngine.Search("git commit --amend");
Measure("result cache hit", 100_000, () => cachedEngine.Search("git commit --amend"));

static void Measure(string name, int iterations, Func<IReadOnlyList<CommandSearchHit>> search)
{
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    var stopwatch = Stopwatch.StartNew();
    var resultCount = 0;
    for (var index = 0; index < iterations; index++)
    {
        resultCount += search().Count;
    }

    stopwatch.Stop();
    var microseconds = stopwatch.Elapsed.TotalMicroseconds / iterations;
    Console.WriteLine($"{name,-30} {microseconds,10:N2} us/op  results={resultCount / iterations}");
}
