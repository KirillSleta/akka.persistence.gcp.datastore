using Akka.Actor;
using Akka.Persistence;

namespace akka.persistence.gcp.datastore.tests
{
    public partial class DatastoreIntegrationSpec : Akka.TestKit.Xunit2.TestKit
    {

          //[Serializable]
          public class DeleteToCommand
          {
              public long SequenceNumber { get; private set; }
              public bool Permanent { get; private set; }

              public DeleteToCommand(long sequenceNumber, bool permanent)
              {
                  SequenceNumber = sequenceNumber;
                  Permanent = permanent;
              }
          }

          //[Serializable]
          public class HandledMessage
          {

              public string Message { get; private set; }
              public long SequenceNumber { get; private set; }
              public bool IsRecovering { get; private set; }

              public HandledMessage(string message, long sequenceNumber, bool isRecovering)
              {
                  Message = message;
                  SequenceNumber = sequenceNumber;
                  IsRecovering = isRecovering;
              }
          }

        public class PersistentActorA : PersistentActor
       {

           public PersistentActorA(string persistenceId)
           {
               PersistenceId = persistenceId;
           }

           public override string PersistenceId { get; }

           protected override bool ReceiveRecover(object message)
           {
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
               if (message is DeleteToCommand)
               {
                   var delete = (DeleteToCommand) message;
                   DeleteMessages(delete.SequenceNumber);
                   return true;
               }

               if (message is string)
               {
                   var payload = (string) message;
                   Persist(payload, Handle);
                   return true;
               }

               return false;
           }

           private void Handle(string payload)
           {
                Sender.Tell(new HandledMessage(payload, LastSequenceNr, IsRecovering), Self);
           }
       }
    }
}
