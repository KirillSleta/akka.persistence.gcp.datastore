using akka.persistence.gcp.datastore.journal;
using akka.persistence.gcp.datastore.snapshot;
using Akka.Actor;

namespace akka.persistence.gcp.datastore.tests
{
    /// <summary>
    /// Some static helper methods for resetting Cassandra between tests or test contexts.
    /// </summary>
    public static class TestSetupHelpers
    {
        public static string Config = $@"
            akka.persistence.journal.plugin = ""akka.persistence.journal.datastore-journal""
            akka.persistence.snapshot-store.plugin = ""akka.persistence.snapshot-store.datastore-snapshot-store""
            akka.persistence.publish-plugin-commands = on
            akka.test.single-expect-default = 10s
            akka.persistence.journal.datastore-journal.project-id = ""modified-ripsaw-162819""
            akka.persistence.journal.datastore-journal.namespace-id = ""akka-db-dev""
            akka.persistence.snapshot-store.datastore-snapshot-store.project-id = ""modified-ripsaw-162819""
            akka.persistence.snapshot-store.datastore-snapshot-store.namespace-id = ""akka-db-dev""
        ";

        public static void ResetJournalData(ActorSystem sys)
        {
            // Get or add the extension
            var ext = DatastorePersistence.Instance.Apply(sys);

            // Use session to remove keyspace
//            DatastoreJournal journal = new DatastoreJournal();
//            journal.Purge().Wait();
        }

        public static void ResetSnapshotStoreData(ActorSystem sys)
        {
            // Get or add the extension
            var ext = DatastorePersistence.Instance.Apply(sys);

            // Use session to remove the keyspace
//            DatastoreSnapshotStore snapshotStore = new DatastoreSnapshotStore();
//            snapshotStore.DeleteAsync()
        }
    }
}
