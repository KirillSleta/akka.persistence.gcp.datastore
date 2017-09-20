using System;
using Akka.Actor;

namespace akka.persistence.gcp.datastore.tests
{
    public partial class DatastoreIntegrationSpec : Akka.TestKit.Xunit2.TestKit
    {
        public class PersistentActorCWithManualRecovery : PersistentActorC
       {
           public PersistentActorCWithManualRecovery(string persistenceId, IActorRef probe)
               : base(persistenceId, probe)
           {
           }

           protected override void PreRestart(Exception reason, object message)
           {
               // Don't do automatic recovery
           }
       }

    }
}
