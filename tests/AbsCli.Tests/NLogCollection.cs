using Xunit;

namespace AbsCli.Tests;

[CollectionDefinition("NLog")]
public class NLogCollection
{
    // Empty marker — disables parallel execution of tests in this collection.
    //
    // Join it from any test that makes production code log, not just from tests
    // that assert on log output. NLog's configuration is process-global, so a
    // stray line emitted from a parallel collection lands in whichever
    // MemoryTarget a log-asserting test has installed, and fails it on count.

}
