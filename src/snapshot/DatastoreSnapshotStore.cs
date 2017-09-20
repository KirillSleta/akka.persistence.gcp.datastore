using System;
using System.Linq;
using System.Threading.Tasks;
using akka.persistence.gcp.datastore.journal;
using Akka.Persistence;
using Akka.Persistence.Serialization;
using Akka.Persistence.Snapshot;
using Akka.Serialization;
using Google.Api;
using Google.Cloud.Datastore.V1;
using Google.Protobuf;
using Grpc.Core;

namespace akka.persistence.gcp.datastore.snapshot
{
    public class DatastoreSnapshotStore : SnapshotStore
    {
        private static readonly Type SnapshotType = typeof(Snapshot);
        private readonly DatastoreExtension _datastoreExtension;
        private readonly Serializer _serializer;
        private DatastoreDb _db;
        private string _snapshotKindRoot;
        private string _snapshotKind;

        protected KeyFactory SnapshotRootKeyFactory => _db.CreateKeyFactory(_snapshotKindRoot);
        protected Key RootKey(string persistenceId) => SnapshotRootKeyFactory.CreateKey(persistenceId);
        protected KeyFactory SnapshotKeyFactory(Key ancestorKey) => new KeyFactory(ancestorKey, _snapshotKind);

        protected Key EntityKey(string persistenceId, long id) => SnapshotKeyFactory(RootKey(persistenceId))
            .CreateKey(id);

        public DatastoreSnapshotStore()
        {
            _datastoreExtension = DatastorePersistence.Instance.Apply(Context.System);
            _serializer = Context.System.Serialization.FindSerializerForType(SnapshotType);
        }

        protected override void PreStart()
        {
            base.PreStart();
            SnapshotDatastoreSettings settings = _datastoreExtension.SnapshotSettings;
            var projectId = settings.ProjectId;
            var namespaceId = settings.NamespaceId;
            _snapshotKind = settings.SnapshotKind;
            _snapshotKindRoot = $"{_snapshotKind}_Root";
            
            if (settings.UseManaged)
            {
                _db = DatastoreDb.Create(projectId, namespaceId);
            }
            else
            {
                var channel = new Channel(settings.DatastoreHost, settings.DatastorePort, ChannelCredentials.Insecure);
                var client = DatastoreClient.Create(channel);
                _db = DatastoreDb.Create(projectId, namespaceId, client);
            }

        }

        protected async Task<Entity> GetRootEntity(string persistenceId)
        {
            var rootEntityKey = RootKey(persistenceId);
            var entity = await _db.LookupAsync(rootEntityKey).ConfigureAwait(false);
            return entity;
        }

        protected async Task DeleteRootEntity(string persistenceId)
        {
            await _db.DeleteAsync(RootKey(persistenceId));
        }

        protected override Task<SelectedSnapshot> LoadAsync(string persistenceId, SnapshotSelectionCriteria criteria)
        {
            return Equals(criteria, SnapshotSelectionCriteria.Latest)
                ? GetLatestSnapshot(persistenceId)
                    : (Equals(criteria, SnapshotSelectionCriteria.None) 
                    ? null : GetSnapShotByCriteria(persistenceId, criteria));
        }

        private async Task<SelectedSnapshot> GetSnapShotByCriteria(string persistenceId, SnapshotSelectionCriteria criteria)
        {
            var filter = Filter.And(
                Filter.HasAncestor(RootKey(persistenceId)),
                Filter.GreaterThanOrEqual(SnapshotFields.SequenceNr, criteria.MinSequenceNr),
                Filter.LessThanOrEqual(SnapshotFields.SequenceNr, criteria.MaxSequenceNr)//,
                //Filter.LessThanOrEqual(SnapshotFields.Timestamp, criteria.MaxTimeStamp.Ticks)
            );
//            if (criteria.MinTimestamp.HasValue)
//            {
//                filter = Filter.And(filter,
//                    Filter.GreaterThanOrEqual(SnapshotFields.Timestamp, criteria.MinTimestamp.Value.Ticks)
//                    );
//            }

            Query query = new Query(_snapshotKind)
            {
                Filter = filter,
                Order = { { JournalFields.SequenceNr, PropertyOrder.Types.Direction.Descending } },
                //,Limit = (int) max
            };

            var results = await _db.RunQueryAsync(query).ConfigureAwait(false);
            if (!results.Entities.Any()) return null;
            var result = results.Entities.FirstOrDefault(
                e => e[SnapshotFields.Timestamp].IntegerValue <= criteria.MaxTimeStamp.Ticks 
                  && e[SnapshotFields.Timestamp].IntegerValue >= criteria.MinTimestamp?.Ticks);
            return result != null ? EntityToSnapshot(persistenceId, result) : null;
        }

        private static SnapshotMetadata MapRowToSnapshotMetadata(string persistenceId, Entity entity)
        {
            return new SnapshotMetadata(
                persistenceId, 
                entity[SnapshotFields.SequenceNr].IntegerValue,
                new DateTime(entity[SnapshotFields.Timestamp].IntegerValue));
        }

        private object Deserialize(byte[] bytes)
        {
            return ((Snapshot)_serializer.FromBinary(bytes, SnapshotType)).Data;
        }

        private byte[] Serialize(object snapshotData)
        {
            return _serializer.ToBinary(new Snapshot(snapshotData));
        }

        private async Task<SelectedSnapshot> GetLatestSnapshot(string persistenceId)
        {
            var rootSnapshot = await GetRootEntity(persistenceId);
            if (rootSnapshot == null) return null;
            return EntityToSnapshot(persistenceId, rootSnapshot);
        }

        private SelectedSnapshot EntityToSnapshot(string persistenceId, Entity snapshotEntity)
        {
            var metadata = MapRowToSnapshotMetadata(persistenceId, snapshotEntity);
            var payload = snapshotEntity[JournalFields.Payload].BlobValue.ToByteArray();
            return new SelectedSnapshot(metadata, Deserialize(payload));
        }

        private Entity SnapshotToEntity(SnapshotMetadata metadata, object snapshot)
        {
            var entity = new Entity()
            {
                [SnapshotFields.SequenceNr] = metadata.SequenceNr,
                [SnapshotFields.Timestamp] = metadata.Timestamp.Ticks,
                [SnapshotFields.Payload] = new Value
                {
                    BlobValue = ByteString.CopyFrom(Serialize(snapshot)),
                    ExcludeFromIndexes = true
                }
            };
            return entity;
        }

        protected override async Task SaveAsync(SnapshotMetadata metadata, object snapshot)
        {
            var rootEntity = SnapshotToEntity(metadata, snapshot);
            var snapshotEntity = new Entity(rootEntity);
            using (var tr = await _db.BeginTransactionAsync())
            {
                rootEntity.Key = RootKey(metadata.PersistenceId);
                snapshotEntity.Key = EntityKey(metadata.PersistenceId, metadata.SequenceNr);
                tr.Upsert(rootEntity, snapshotEntity);
                tr.Commit();
            }
        }

        async Task CheckDeleteRootsnapshot(string persistenceId, long seqNr, long timestamp)
        {
            var rootEntity = await GetRootEntity(persistenceId);
            if (rootEntity?[SnapshotFields.SequenceNr].IntegerValue == seqNr
                && rootEntity[SnapshotFields.Timestamp].IntegerValue == timestamp)
            {
                await _db.DeleteAsync(rootEntity);
            }
        }

        protected override async Task DeleteAsync(SnapshotMetadata metadata)
        {
            var filter = Filter.And(
                Filter.HasAncestor(RootKey(metadata.PersistenceId)),
                Filter.Equal(SnapshotFields.SequenceNr, metadata.SequenceNr)
                //,Filter.Equal(SnapshotFields.Timestamp, metadata.Timestamp.Ticks)
            );
           
            Query query = new Query(_snapshotKind)
            {
                Filter = filter,
                Order = { { JournalFields.SequenceNr, PropertyOrder.Types.Direction.Ascending } }
            };
            var results = await _db.RunQueryAsync(query).ConfigureAwait(false);
            await _db.DeleteAsync(results.Entities);
            await CheckDeleteRootsnapshot(metadata.PersistenceId, metadata.SequenceNr, metadata.Timestamp.Ticks);
        }

        protected override async Task DeleteAsync(string persistenceId, SnapshotSelectionCriteria criteria)
        {
            var filter = Filter.And(
                Filter.HasAncestor(RootKey(persistenceId)),
                Filter.GreaterThanOrEqual(SnapshotFields.SequenceNr, criteria.MinSequenceNr),
                Filter.LessThanOrEqual(SnapshotFields.SequenceNr, criteria.MaxSequenceNr)
                //,Filter.LessThanOrEqual(SnapshotFields.Timestamp, criteria.MaxTimeStamp.Ticks)
            );
            //            if (criteria.MinTimestamp.HasValue)
            //            {
            //                filter = Filter.And(filter,
            //                    Filter.GreaterThanOrEqual(SnapshotFields.Timestamp, criteria.MinTimestamp.Value.Ticks)
            //                );
            //            }

                Query query = new Query(_snapshotKind)
            {
                Filter = filter,
                Order = { { JournalFields.SequenceNr, PropertyOrder.Types.Direction.Ascending } }
                //,Limit = (int) max
            };

            var results = await _db.RunQueryAsync(query).ConfigureAwait(false);
            var timestampFiltered =
                results.Entities?.Where(
                    e => e[SnapshotFields.SequenceNr].IntegerValue <= criteria.MaxSequenceNr
                      && e[SnapshotFields.SequenceNr].IntegerValue >= criteria.MinSequenceNr
                      && e[SnapshotFields.Timestamp].IntegerValue <= criteria.MaxTimeStamp.Ticks
                      && e[SnapshotFields.Timestamp].IntegerValue >= criteria.MinTimestamp?.Ticks
                    );
            await _db.DeleteAsync(timestampFiltered);
            await CheckDeleteRootsnapshot(persistenceId, criteria.MaxSequenceNr, criteria.MaxTimeStamp.Ticks);
        }
    }
}