namespace akka.persistence.gcp.datastore.snapshot
{
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
}