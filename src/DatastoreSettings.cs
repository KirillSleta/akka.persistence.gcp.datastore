using Akka.Configuration;

namespace akka.persistence.gcp.datastore
{
    public class DatastoreSettings
    {
        public string ProjectId { get; }
        public string NamespaceId { get; }
        public bool UseManaged {get;}
        public string DatastoreHost {get;}

        public int DatastorePort { get; }

        public DatastoreSettings(Config config)
        {
            ProjectId = config.GetString("project-id");
            NamespaceId = config.GetString("namespace-id");
            UseManaged = config.GetBoolean("use-managed");
            DatastoreHost = config.GetString("datastore-host");
            DatastorePort = config.GetInt("datastore-port");
        }
    }

    public class JournalDatastoreSettings : DatastoreSettings
    {
        public string JournalKind { get; }
        public JournalDatastoreSettings(Config config) : base(config)
        {
            JournalKind = config.GetString("journalentity-kind");
        }
    }

    public class SnapshotDatastoreSettings : DatastoreSettings
    {
        public string SnapshotKind { get; }
        public SnapshotDatastoreSettings(Config config) : base(config)
        {
            SnapshotKind = config.GetString("snapshotentity-kind");
        }
    }
}