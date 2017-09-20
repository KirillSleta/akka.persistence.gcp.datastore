using Akka.Actor;
using Akka.Persistence;

namespace akka.persistence.gcp.datastore.tests
{
    public partial class DatastoreIntegrationSpec : Akka.TestKit.Xunit2.TestKit
    {
        public class PersistentActorC : PersistentActor
       {
           private readonly IActorRef _probe;

           private string _last;

           public override string PersistenceId { get; }

           public PersistentActorC(string persistenceId, IActorRef probe)
           {
               PersistenceId = persistenceId;
               _probe = probe;
           }

           protected override bool ReceiveRecover(object message)
           {
               if (message is SnapshotOffer)
               {
                   var offer = (SnapshotOffer) message;
                   _last = (string) offer.Snapshot;
                   _probe.Tell(string.Format("offered-{0}", _last));
                   return true;
               }

               if (message is string)
               {
                   var payload = (string) message;
                   Handle(payload);
                   return true;
               }

               if (message is RecoveryCompleted) return true;

               return false;
           }

           protected override bool ReceiveCommand(object message)
           {
               if (message is string)
               {
                   var msg = (string) message;
                   if (msg == "snap")
                       SaveSnapshot(_last);
                   else
                       Persist(msg, Handle);

                   return true;
               }

               if (message is SaveSnapshotSuccess)
               {
                   _probe.Tell(string.Format("snapped-{0}", _last), Context.Sender);
                   return true;
               }

               if (message is DeleteToCommand)
               {
                   var delete = (DeleteToCommand) message;
                   DeleteMessages(delete.SequenceNumber);
                   return true;
               }
                
               return false;
           }

           private void Handle(string payload)
           {
               _last = string.Format("{0}-{1}", payload, LastSequenceNr);
               _probe.Tell(new HandledMessage(payload, LastSequenceNr, IsRecovering));
           }
       }

    }
}
