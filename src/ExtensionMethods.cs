using System;
using Akka.Actor;
using Akka.Persistence;
using Akka.Serialization;
using Google.Cloud.Datastore.V1;

namespace akka.persistence.gcp.datastore
{
    internal static class ExtensionMethods
    {
        /// <summary>
        /// Gets the PersistenceExtension instance registered with the ActorSystem. Throws an InvalidOperationException if not found.
        /// </summary>
        internal static PersistenceExtension PersistenceExtension(this ActorSystem system)
        {
            var ext = system.GetExtension<PersistenceExtension>();
            if (ext == null)
                throw new InvalidOperationException("Persistence extension not found.");

            return ext;
        }

        
    }
}