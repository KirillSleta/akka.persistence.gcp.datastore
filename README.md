akka.persistence.gcp.datastore
==============================

## Overview
Google Cloud Platform Datastore akka.net prsistence provider

## Sample config

```
akka.persistence.journal.datastore-journal {
  # Type name of the cassandra journal plugin
  class = "akka.persistence.gcp.datastore.journal.DatastoreJournal, akka.persistence.gcp.datastore"
  project-id = "{your-project-id}"
  namespace-id = "{your-namespace-id}"
  # use managed(cloud) or datastore emulator
  use-managed = "on"
  datastore-host = "{datastore emulator host}"
  datastore-port = "9090"
  journalentity-kind = "Journal"
  gcp-cold-storage = "off"
  }

akka.persistence.snapshot-store.datastore-snapshot-store {
  # Type name of the cassandra journal plugin
  class = "akka.persistence.gcp.datastore.snapshot.DatastoreSnapshotStore, akka.persistence.gcp.datastore"
  project-id = "{your-project-id}"
  namespace-id = "{your-namespace-id}"
  # use managed(cloud) or datastore emulator
  use-managed = "on"
  datastore-host = "{datastore emulator host}"
  datastore-port = "9090"
  snapshotentity-kind = "Snapshot"
  }
  ```

## Journal storage schema:

    *****************************
    *   Ancestor(Root) Entity   *
    * D'o*-->  SeqNr            *
    *   |                       *
    *   |                       *
    *   v            Entity     *
    *   o----->o---->o----->*   *
    *   A      B     C      D   *
    *****************************

D' - last snapshot or event
A,B,C,D - sequence of snapshots or events for a persisted entity

## Implementation details

**Warning!** as of now the GCP Datastore do not support queries with several inequality filters (https://cloud.google.com/datastore/docs/concepts/queries#limitations_of_cursors#Inequality filters are limited to at most one property) this library using in-memory LINQ workaround to filter Snapshots in *GetSnapShotByCriteria* and *DeleteAsync* methods of *DatastoreSnapshotStore* class. **Make sure that you don't use it to query\delete a wide range of snapshots by SequenceNr.**

Make sure you applied index.yaml file to datastore from terminal:

```cmd
gcloud datastore create-indexes INDEX_FILE [GCLOUD_WIDE_FLAG ...]
```

Make sure you have a GCP credentials set up when running this code.

Journal Fields:

```csharp
internal static class JournalFields
    {
        public const string SequenceNr = "SequenceNr";
        public const string Payload = "Payload";
        public const string Manifest = "Manifest";
        public const string WriterGuid = "WriterGuid";
        //ancestor
        internal static class Root
        {
            public const string HighestSequenceNr = "HighestSequenceNr";
        }
    }
```

Snapshot Fields:

```csharp
internal static class SnapshotFields
    {
        public const string SequenceNr = "SequenceNr";
        public const string Timestamp = "Timestamp";
        public const string Payload = "Payload";
        //ancestor
        internal static class Root
        {
            public const string LastSequenceNr = "LastSequenceNr";
        }
    }
```
