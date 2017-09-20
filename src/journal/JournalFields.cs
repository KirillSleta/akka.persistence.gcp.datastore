namespace akka.persistence.gcp.datastore.journal
{
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
}