using System;
using System.Linq;
using Raven.Client;
using Raven.Database.Indexing;
using Xunit;

namespace Raven.Tests.Bugs.TransformResults
{
	public class WithGuidId : LocalClientTest
	{
		[Fact]
		public void CanBeUsedForTransformResultsWithDocumentId()
		{
			using(var store = NewDocumentStore())
			{
				new ThorIndex().Execute(((IDocumentStore) store).DatabaseCommands, ((IDocumentStore) store).Conventions);

				using(var s = store.OpenSession())
				{
					s.Store(new Thor
					{
						Id = Guid.NewGuid(),
						Name = "Thor"
					});
					s.SaveChanges();
				}

				using (var s = store.OpenSession())
				{
					var objects = s.Query<dynamic>("ThorIndex")
						.Customize(x=>x.WaitForNonStaleResults())
						.ToArray();

					Assert.DoesNotThrow(() => new Guid(objects[0].Id));
				}
			}
		}
	}
}