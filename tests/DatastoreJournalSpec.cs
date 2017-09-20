using Akka.Configuration;
using Akka.Persistence.TCK.Journal;
using Xunit.Abstractions;

namespace akka.persistence.gcp.datastore.tests
{
    public class DatastoreJournalSpec : JournalSpec
    {

        public DatastoreJournalSpec(ITestOutputHelper output)
            : base(TestSetupHelpers.Config, "DatastoreJournalSystem", output: output)
        {
            TestSetupHelpers.ResetJournalData(Sys);
            Initialize();
        }
        
        protected override void Dispose(bool disposing)
        {
            TestSetupHelpers.ResetJournalData(Sys);
            base.Dispose(disposing);
        }
    }
}
