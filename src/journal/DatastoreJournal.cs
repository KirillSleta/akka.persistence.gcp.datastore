using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Persistence;
using Akka.Persistence.Journal;
using Akka.Serialization;
using Google.Cloud.Datastore.V1;
using System.Linq;
using System.Text;
using Akka.Util.Internal;
using Google.Protobuf;
using Grpc.Core;
using LanguageExt;
using static LanguageExt.Prelude;
using Query = Google.Cloud.Datastore.V1.Query;

namespace akka.persistence.gcp.datastore.journal
{
    public class DatastoreJournal : AsyncWriteJournal
    {
        private static readonly Type PersistentRepresentationType = typeof(IPersistentRepresentation);
        private readonly DatastoreExtension _datastoreExtension;
        private readonly Serializer _serializer;
        private DatastoreDb _db;
        private string _journalKindRoot;
        private string _journalKind;

        protected KeyFactory JournalRootKeyFactory => _db.CreateKeyFactory(_journalKindRoot);
        protected Key RootKey(string persistenceId) => JournalRootKeyFactory.CreateKey(persistenceId);
        protected KeyFactory JournalKeyFactory(Key ancestorKey) => new KeyFactory(ancestorKey, _journalKind);

        protected Key EntityKey(string persistenceId, long id) => JournalKeyFactory(RootKey(persistenceId))
            .CreateKey(id);

        public DatastoreJournal()
        {
            _datastoreExtension = DatastorePersistence.Instance.Apply(Context.System);
            _serializer = Context.System.Serialization.FindSerializerForType(PersistentRepresentationType);
        }

        protected override void PreStart()
        {
            base.PreStart();
            JournalDatastoreSettings settings = _datastoreExtension.JournalSettings;
            var projectId = settings.ProjectId;
            var namespaceId = settings.NamespaceId;
            _journalKind = settings.JournalKind;
            _journalKindRoot = $"{_journalKind}_Root";

            if (settings.UseManaged) {
                _db = DatastoreDb.Create(projectId, namespaceId);
            }else{
                var channel = new Channel(settings.DatastoreHost, settings.DatastorePort, ChannelCredentials.Insecure);
                var client = DatastoreClient.Create(channel);
                _db = DatastoreDb.Create(projectId, namespaceId, client);
            }
                        
        }

        protected IPersistentRepresentation MapEntityToPersistentRepresentation(Entity entity)
        {
            var bytes = entity[JournalFields.Payload].BlobValue.ToByteArray();
            IPersistentRepresentation pr = Deserialize(bytes);
            return pr;
        }

        private IPersistentRepresentation Deserialize(byte[] bytes)
        {
            return (IPersistentRepresentation)_serializer.FromBinary(bytes, PersistentRepresentationType);
        }

        private byte[] Serialize(IPersistentRepresentation message)
        {
            return _serializer.ToBinary(message);
        }

        protected async Task<Entity> GetRootEntity(string persistenceId)
        {
            var rootEntityKey = RootKey(persistenceId);
            var entity = await _db.LookupAsync(rootEntityKey).ConfigureAwait(false);
            return entity;
        }

        protected async Task<Entity> GetOrCreateRootEntity(string persistenceId, long? seqNr)
        {
            var entity = await GetRootEntity(persistenceId).ConfigureAwait(false);
            if (entity == null)
            {
                entity = new Entity
                {
                    Key = RootKey(persistenceId)
                };
                if (seqNr.HasValue) entity[JournalFields.Root.HighestSequenceNr] = seqNr.Value;
                else entity[JournalFields.Root.HighestSequenceNr] = 0;

            }
            else
            {
                if (seqNr.HasValue)
                {
                    var entitySeqNr = entity[JournalFields.Root.HighestSequenceNr].IntegerValue;
                    if (seqNr.Value > entitySeqNr)
                    {
                        entity[JournalFields.Root.HighestSequenceNr] = seqNr.Value;

                    }
                }
            }
            return entity;
        }

        protected async Task DeleteRootEntity(string persistenceId)
        {
            await _db.DeleteAsync(RootKey(persistenceId));
        }

        public override async Task ReplayMessagesAsync(IActorContext context, string persistenceId, long fromSequenceNr, long toSequenceNr, long max,
            Action<IPersistentRepresentation> recoveryCallback)
        {
            Query query = new Query(_journalKind)
            {
                Filter = Filter.And(
                    Filter.HasAncestor(RootKey(persistenceId)),
                    Filter.GreaterThanOrEqual(JournalFields.SequenceNr, fromSequenceNr),
                    Filter.LessThanOrEqual(JournalFields.SequenceNr, toSequenceNr)
                ),
                Order = { { JournalFields.SequenceNr, PropertyOrder.Types.Direction.Ascending } }
                ,Limit = max > Int32.MaxValue? (int?)null : (int) max
            };

            var results = await _db.RunQueryAsync(query).ConfigureAwait(false);
            results.Entities
                .Select(MapEntityToPersistentRepresentation)
                .ForEach(recoveryCallback);
        }

        public override async Task<long> ReadHighestSequenceNrAsync(string persistenceId, long fromSequenceNr)
        {
            //query root entity for field
            var entity = await GetRootEntity(persistenceId);
            var maxSeqNr = entity?[JournalFields.Root.HighestSequenceNr].IntegerValue ?? 0L;
            return maxSeqNr;
        }


        protected override async Task<IImmutableList<Exception>> WriteMessagesAsync(IEnumerable<AtomicWrite> messages)
        {
            var writes = messages as IList<AtomicWrite> ?? messages.ToList();
            IList<Entity> entities = new List<Entity>(writes.Count);
            using (var tr = await _db.BeginTransactionAsync())
            {
                foreach (var w in writes)
                {
                    
                var rootEntity = await GetOrCreateRootEntity(w.PersistenceId, w.HighestSequenceNr);
                foreach (var p in (IEnumerable<IPersistentRepresentation>)w.Payload)
                {
                    var entity = new Entity
                    {
                        Key = EntityKey(p.PersistenceId,p.SequenceNr),
                        [JournalFields.SequenceNr] = p.SequenceNr,
                        [JournalFields.Manifest] = p.Manifest,
                        [JournalFields.WriterGuid] = p.WriterGuid,
                        [JournalFields.Payload] = new Value
                        {
                            BlobValue = ByteString.CopyFrom(Serialize(p)),
                            ExcludeFromIndexes = true
                        }
                    };
                    entities.Add(entity);
                }
                //update SeqNr on root object
                tr.Upsert(rootEntity);
            }
                tr.Upsert(entities);
                tr.Commit();
            }
            return await Task.FromResult((IImmutableList<Exception>)null); // all good
        }

        protected override async Task DeleteMessagesToAsync(string persistenceId, long toSequenceNr)
        {
            Query query = new Query(_journalKind)
            {
                Filter = Filter.And(
                    Filter.HasAncestor(RootKey(persistenceId)),
                    Filter.LessThanOrEqual(JournalFields.SequenceNr, toSequenceNr)
                ) 
            };

            var results = await _db.RunQueryAsync(query).ConfigureAwait(false);
            await _db.DeleteAsync(results.Entities);
            var rootEntitySeqNr = await ReadHighestSequenceNrAsync(persistenceId, 0);
            if(rootEntitySeqNr == toSequenceNr)
            await DeleteRootEntity(persistenceId).ConfigureAwait(false);
        }


        /// <summary>
        /// Retry the action when a Grpc.Core.RpcException is thrown.
        /// </summary>
        private T RetryRpc<T>(Func<T> action)
        {
            List<RpcException> exceptions = null;
            var delayMs = 2000;
            int retryCount = 5;
            for (int tryCount = 0; tryCount < retryCount; ++tryCount)
            {
                try
                {
                    return action();
                }
                catch (Grpc.Core.RpcException e)
                {
                    if (exceptions == null)
                        exceptions = new List<Grpc.Core.RpcException>();
                    exceptions.Add(e);
                }
                System.Threading.Thread.Sleep(delayMs);
                delayMs *= 2;  // Exponential back-off.
            }
            throw new AggregateException(exceptions);
        }

        private void RetryRpc(Action action)
        {
            RetryRpc(() => { action(); return 0; });
        }

    }
}
