//-----------------------------------------------------------------------
// <copyright file="AncestryPutTrigger.cs" company="Hibernating Rhinos LTD">
//     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System.ComponentModel.Composition;
using Newtonsoft.Json.Linq;
using Raven.Abstractions.Data;
using Raven.Database.Plugins;
using Raven.Http;
using Raven.Json.Linq;

namespace Raven.Bundles.Replication.Triggers
{
	[ExportMetadata("Order", 10000)]
    public class AncestryPutTrigger : AbstractPutTrigger
    {
        private ReplicationHiLo hiLo;
        public override void Initialize()
        {
            base.Initialize();
            hiLo = new ReplicationHiLo
            {
                Database = Database
            };
        }
        public override void OnPut(string key, RavenJObject document, RavenJObject metadata, TransactionInformation transactionInformation)
        {
            if (key.StartsWith("Raven/")) // we don't deal with system documents
                return;
            var doc = Database.Get(key, null);
            if (doc != null)
            {
                metadata[ReplicationConstants.RavenReplicationParentVersion] =
                    doc.Metadata[ReplicationConstants.RavenReplicationVersion];
                metadata[ReplicationConstants.RavenReplicationParentSource] =
                doc.Metadata[ReplicationConstants.RavenReplicationSource];
            }
            metadata[ReplicationConstants.RavenReplicationVersion] = RavenJToken.FromObject(hiLo.NextId());
			metadata[ReplicationConstants.RavenReplicationSource] = RavenJToken.FromObject(Database.TransactionalStorage.Id);
        }
    }
}
