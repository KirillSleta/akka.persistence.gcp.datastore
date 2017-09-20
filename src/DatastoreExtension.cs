using System;
using Akka.Actor;

namespace akka.persistence.gcp.datastore
{
    public class DatastoreExtension : IExtension
    {
        public JournalDatastoreSettings JournalSettings{get; }

        public SnapshotDatastoreSettings SnapshotSettings{get; }

        public DatastoreExtension(ExtendedActorSystem system)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));

            // Initialize fallback configuration defaults
            system.Settings.InjectTopLevelFallback(DatastorePersistence.DefaultConfig());

            var datastoreConfig = system.Settings.Config.GetConfig("akka.persistence.journal.datastore-journal");
            JournalSettings = new JournalDatastoreSettings(datastoreConfig);

            var snapshotstoreConfig = system.Settings.Config.GetConfig("akka.persistence.snapshot-store.datastore-snapshot-store");
            SnapshotSettings = new SnapshotDatastoreSettings(snapshotstoreConfig);
        }
    }
}