using System.Net;
using Raven.Client.Connection;
using Raven.Client.Document;
using Raven.Tests.Bundles.Versioning;
using Xunit;

namespace Raven.Tests.Bundles.Replication
{
	public class WritesDuringFailover2 : ReplicationBase
	{
		protected override void ConfigureStore(DocumentStore documentStore)
		{
			documentStore.Conventions.FailoverBehavior = FailoverBehavior.FailImmediately;
		}

		[Fact]
		public void Can_disallow_failover()
		{
			var store1 = CreateStore();
			var store2 = CreateStore();

			TellFirstInstanceToReplicateToSecondInstance();

			var serverClient = ((ServerClient)store1.DatabaseCommands);
			serverClient.ReplicationInformer.RefreshReplicationInformation(serverClient);

			using (var session = store1.OpenSession())
			{
				session.Store(new Company { Name = "Hibernating Rhinos" });
				session.SaveChanges();
			}

			WaitForReplication(store2, "companies/1");

			Servers[0].Dispose();

			using (var session = store1.OpenSession())
			{
				Assert.Throws<WebException>(() => session.Load<Company>("companies/1"));
			}
		}
	}
}