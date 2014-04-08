//-----------------------------------------------------------------------
// <copyright file="RavenTest.cs" company="Hibernating Rhinos LTD">
//     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Primitives;
using System.Diagnostics;
using System.Threading;
using Raven.Abstractions.Data;
using Raven.Client;
using Raven.Client.Connection;
using Raven.Client.Document;
using Raven.Client.Embedded;
using Raven.Client.Indexes;
using Raven.Database;
using Raven.Database.Config;
using Raven.Database.Storage;
using Raven.Json.Linq;
using Raven.Server;
using Raven.Tests.Helpers.Extensions;
using Xunit;

namespace Raven.Tests.Helpers
{
    public class RavenTestBase : IUseFixture<RavenTestFixutre>, IDisposable
    {
        private RavenTestFixutre ravenTestFixture;

        public void SetFixture(RavenTestFixutre fixture)
        {
            ravenTestFixture = fixture;
            ravenTestFixture.ModifyConfiguration = ModifyConfiguration;
            ravenTestFixture.ModifyEmbeddedStore = ModifyStore;
            ravenTestFixture.ModifyServer = ModifyServer;
            ravenTestFixture.ModifyStore = ModifyStore;
        }

        public List<RavenDbServer> Servers
        {
            get { return ravenTestFixture.Servers; }
        }

        public List<IDocumentStore> Stores
        {
            get { return ravenTestFixture.Stores; }
        }

        protected string NewDataPath(string prefix = null)
        {
            return ravenTestFixture.NewDataPath(prefix);
        }

        public EmbeddableDocumentStore NewDocumentStore(
            bool runInMemory = true,
            string requestedStorage = null,
            ComposablePartCatalog catalog = null,
            string dataDir = null,
            bool enableAuthentication = false)
        {
            return ravenTestFixture.NewDocumentStore(runInMemory, requestedStorage, catalog, dataDir, enableAuthentication);
        }

        public static void EnableAuthentication(DocumentDatabase database)
        {
            database.EnableAuthentication();
        }

        public IDocumentStore NewRemoteDocumentStore(bool fiddler = false, RavenDbServer ravenDbServer = null, string databaseName = null,
            bool runInMemory = true,
            string dataDirectory = null,
            string requestedStorage = null,
            bool enableAuthentication = false)
        {
            return ravenTestFixture.NewRemoteDocumentStore(fiddler, ravenDbServer, databaseName);
        }

        public string GetDefaultStorageType(string requestedStorage = null)
        {
            return ravenTestFixture.GetDefaultStorageType(requestedStorage);
        }

        protected RavenDbServer GetNewServer(int port = 8079,
            string dataDirectory = null,
            bool runInMemory = true,
            string requestedStorage = null,
            bool enableAuthentication = false)
        {
            return ravenTestFixture.GetNewServer(port, dataDirectory, runInMemory, requestedStorage, enableAuthentication);
        }

        public ITransactionalStorage NewTransactionalStorage(string requestedStorage = null, string dataDir = null)
        {
            return ravenTestFixture.NewTransactionalStorage(requestedStorage, dataDir);
        }

        protected virtual void ModifyStore(DocumentStore documentStore)
        {
        }

        protected virtual void ModifyStore(EmbeddableDocumentStore documentStore)
        {
        }

        protected virtual void ModifyConfiguration(InMemoryRavenConfiguration configuration)
        {
        }

        protected virtual void ModifyServer(RavenDbServer ravenDbServer)
        {
        }

        protected virtual void CreateDefaultIndexes(IDocumentStore documentStore)
        {
            new RavenDocumentsByEntityName().Execute(documentStore);
        }

        public static void WaitForIndexing(IDocumentStore store, string db = null, TimeSpan? timeout = null)
        {
            store.WaitForIndexing(db, timeout);
        }

        public static void WaitForIndexing(DocumentDatabase db)
        {
            db.WaitForIndexing();
        }

        public static void WaitForAllRequestsToComplete(RavenDbServer server)
        {
            server.WaitForAllRequestsToComplete();
        }

        protected void WaitForBackup(DocumentDatabase db, bool checkError)
        {
            db.WaitForBackup(checkError);
        }

        protected void WaitForBackup(IDatabaseCommands commands, bool checkError)
        {
            commands.WaitForBackup(checkError);
        }

        protected void WaitForRestore(IDatabaseCommands databaseCommands)
        {
            databaseCommands.WaitForRestore();
        }

        protected void WaitForDocument(IDatabaseCommands databaseCommands, string id)
        {
            databaseCommands.WaitForDocument(id);
        }

        public static void WaitForUserToContinueTheTest(EmbeddableDocumentStore documentStore, bool debug = true)
        {
            documentStore.WaitForUserToContinueTheTest(debug);
        }

        protected void WaitForUserToContinueTheTest(bool debug = true, string url = null)
        {
            if (debug && Debugger.IsAttached == false)
                return;

            using (var documentStore = new DocumentStore
            {
                Url = url ?? "http://localhost:8079"
            })
            {
                documentStore.Initialize();
                documentStore.DatabaseCommands.Put("Pls Delete Me", null,
                    RavenJObject.FromObject(new { StackTrace = new StackTrace(true) }), new RavenJObject());

                Process.Start(documentStore.Url); // start the server

                do
                {
                    Thread.Sleep(100);
                } while (documentStore.DatabaseCommands.Get("Pls Delete Me") != null && (debug == false || Debugger.IsAttached));
            }
        }

        protected void ClearDatabaseDirectory(string dataDir)
        {
            ravenTestFixture.ClearDatabaseDirectory(dataDir);
        }

        public virtual void Dispose()
        {

        }

        protected void PrintServerErrors(ServerError[] serverErrors)
        {
            ravenTestFixture.PrintServerErrors(serverErrors);
        }

        protected void AssertNoIndexErrors(IDocumentStore documentStore)
        {
            ravenTestFixture.AssertNoIndexErrors(documentStore);
        }

        public static LicensingStatus GetLicenseByReflection(DocumentDatabase database)
        {
            return database.GetLicenseByReflection();
        }
    }
}