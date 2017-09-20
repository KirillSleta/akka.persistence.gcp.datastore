using Akka.Configuration;
using Akka.Persistence.TCK.Snapshot;
using Xunit.Abstractions;

namespace akka.persistence.gcp.datastore.tests
{
    public class DatastoreSnapshotStoreSpec : SnapshotStoreSpec
    {
        public DatastoreSnapshotStoreSpec(ITestOutputHelper output)
            : base(TestSetupHelpers.Config, "DatastoreSnapshotSystem", output: output)
        {
            TestSetupHelpers.ResetSnapshotStoreData(Sys);
            Initialize();
        }

        protected override void Dispose(bool disposing)
        {
            TestSetupHelpers.ResetSnapshotStoreData(Sys);
            base.Dispose(disposing);
        }
    }
}