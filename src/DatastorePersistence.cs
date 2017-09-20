using Akka.Actor;
using Akka.Configuration;

namespace akka.persistence.gcp.datastore
{
    public class DatastorePersistence : ExtensionIdProvider<DatastoreExtension>
    {
        public static readonly DatastorePersistence Instance = new DatastorePersistence();

        public override DatastoreExtension CreateExtension(ExtendedActorSystem system)
        {
            return new DatastoreExtension(system);
        }

        public static Config DefaultConfig()
        {
            return ConfigurationFactory.FromResource<DatastorePersistence>("akka.persistence.gcp.datastore.reference.conf");
        }
    }
}