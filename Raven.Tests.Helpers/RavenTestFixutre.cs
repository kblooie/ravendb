using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Primitives;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Raven.Abstractions.Data;
using Raven.Abstractions.MEF;
using Raven.Client;
using Raven.Client.Document;
using Raven.Client.Embedded;
using Raven.Client.Indexes;
using Raven.Database.Config;
using Raven.Database.Extensions;
using Raven.Database.Impl;
using Raven.Database.Plugins;
using Raven.Database.Server;
using Raven.Database.Storage;
using Raven.Server;
using Raven.Tests.Helpers.Extensions;
using Xunit;
using Xunit.Sdk;

namespace Raven.Tests.Helpers
{
    public class RavenTestFixutre : IDisposable
    {
        protected readonly List<RavenDbServer> servers = new List<RavenDbServer>();
        protected readonly List<IDocumentStore> stores = new List<IDocumentStore>();
        private readonly List<string> pathsToDelete = new List<string>();

        public RavenTestFixutre()
        {
            Environment.SetEnvironmentVariable(Constants.RavenDefaultQueryTimeout, "30");
            CommonInitializationUtil.Initialize();
        }

        public List<RavenDbServer> Servers
        {
            get { return servers; }
        }

        public List<IDocumentStore> Stores
        {
            get { return stores; }
        }

        public string NewDataPath(string prefix = null)
        {
            var newDataDir = Path.GetFullPath(string.Format(@".\{0}-{1}-{2}\", DateTime.Now.ToString("yyyy-MM-dd,HH-mm-ss"), prefix ?? "TestDatabase", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(newDataDir);
            pathsToDelete.Add(newDataDir);
            return newDataDir;
        }

        public EmbeddableDocumentStore NewDocumentStore(
            bool runInMemory = true,
            string requestedStorage = null,
            ComposablePartCatalog catalog = null,
            string dataDir = null,
            bool enableAuthentication = false)
        {
            var storageType = GetDefaultStorageType(requestedStorage);
            var documentStore = new EmbeddableDocumentStore
            {
                Configuration =
                {
                    DefaultStorageTypeName = storageType,
                    DataDirectory = dataDir ?? NewDataPath(),
                    RunInUnreliableYetFastModeThatIsNotSuitableForProduction = true,
                    RunInMemory = storageType.Equals("esent", StringComparison.OrdinalIgnoreCase) == false && runInMemory,
                    Port = 8079
                }
            };

            if (catalog != null)
                documentStore.Configuration.Catalog.Catalogs.Add(catalog);

            try
            {
                ModifyEmbeddedStore(documentStore);
                ModifyConfiguration(documentStore.Configuration);

                documentStore.Initialize();

                if (enableAuthentication)
                {
                    documentStore.DocumentDatabase.EnableAuthentication();
                    ModifyConfiguration(documentStore.Configuration);
                }

                CreateDefaultIndexes(documentStore);

                return documentStore;
            }
            catch
            {
                // We must dispose of this object in exceptional cases, otherwise this test will break all the following tests.
                documentStore.Dispose();
                throw;
            }
            finally
            {
                stores.Add(documentStore);
            }
        }

        public IDocumentStore NewRemoteDocumentStore(bool fiddler = false, RavenDbServer ravenDbServer = null, string databaseName = null,
            bool runInMemory = true,
            string dataDirectory = null,
            string requestedStorage = null,
            bool enableAuthentication = false)
        {
            ravenDbServer = ravenDbServer ?? GetNewServer(runInMemory: runInMemory, dataDirectory: dataDirectory, requestedStorage: requestedStorage, enableAuthentication: enableAuthentication);
            ModifyServer(ravenDbServer);
            var store = new DocumentStore
            {
                Url = GetServerUrl(fiddler),
                DefaultDatabase = databaseName,
            };
            stores.Add(store);
            store.AfterDispose += (sender, args) => ravenDbServer.Dispose();
            ModifyStore(store);
            return store.Initialize();
        }

        private static string GetServerUrl(bool fiddler)
        {
            if (fiddler)
            {
                if (Process.GetProcessesByName("fiddler").Any())
                    return "http://localhost.fiddler:8079";
            }
            return "http://localhost:8079";
        }

        public string GetDefaultStorageType(string requestedStorage = null)
        {
            string defaultStorageType;
            var envVar = Environment.GetEnvironmentVariable("raventest_storage_engine");
            if (string.IsNullOrEmpty(envVar) == false)
                defaultStorageType = envVar;
            else if (requestedStorage != null)
                defaultStorageType = requestedStorage;
            else
                defaultStorageType = "munin";
            return defaultStorageType;
        }

        public RavenDbServer GetNewServer(int port = 8079,
            string dataDirectory = null,
            bool runInMemory = true,
            string requestedStorage = null,
            bool enableAuthentication = false)
        {
            if (dataDirectory != null)
                pathsToDelete.Add(dataDirectory);

            var storageType = GetDefaultStorageType(requestedStorage);
            var ravenConfiguration = new RavenConfiguration
            {
                Port = port,
                DataDirectory = dataDirectory ?? NewDataPath(),
                RunInMemory = storageType.Equals("esent", StringComparison.OrdinalIgnoreCase) == false && runInMemory,
                DefaultStorageTypeName = storageType,
                AnonymousUserAccessMode = enableAuthentication ? AnonymousUserAccessMode.None : AnonymousUserAccessMode.Admin
            };

            ModifyConfiguration(ravenConfiguration);

            ravenConfiguration.PostInit();

            NonAdminHttp.EnsureCanListenToWhenInNonAdminContext(ravenConfiguration.Port);
            var ravenDbServer = new RavenDbServer(ravenConfiguration);
            servers.Add(ravenDbServer);

            try
            {
                using (var documentStore = new DocumentStore
                {
                    Url = "http://localhost:" + port,
                    Conventions =
                    {
                        FailoverBehavior = FailoverBehavior.FailImmediately
                    },
                }.Initialize())
                {
                    CreateDefaultIndexes(documentStore);
                }
            }
            catch
            {
                ravenDbServer.Dispose();
                throw;
            }

            if (enableAuthentication)
            {
                ravenDbServer.Database.EnableAuthentication();
                ModifyConfiguration(ravenConfiguration);
            }

            return ravenDbServer;
        }

        public ITransactionalStorage NewTransactionalStorage(string requestedStorage = null, string dataDir = null)
        {
            ITransactionalStorage newTransactionalStorage;
            string storageType = GetDefaultStorageType(requestedStorage);

            if (storageType == "munin")
                newTransactionalStorage = new Storage.Managed.TransactionalStorage(new RavenConfiguration {DataDirectory = dataDir ?? NewDataPath(),}, () => { });
            else
                newTransactionalStorage = new Storage.Esent.TransactionalStorage(new RavenConfiguration {DataDirectory = dataDir ?? NewDataPath(),}, () => { });

            newTransactionalStorage.Initialize(new DummyUuidGenerator(), new OrderedPartCollection<AbstractDocumentCodec>());
            return newTransactionalStorage;
        }

        public Action<DocumentStore> ModifyStore = store => { };

        public Action<EmbeddableDocumentStore> ModifyEmbeddedStore = store => { };

        public Action<InMemoryRavenConfiguration> ModifyConfiguration = configuration => { };

        public Action<RavenDbServer> ModifyServer = server => { };

        public Action<IDocumentStore> CreateDefaultIndexes = store => 
            new RavenDocumentsByEntityName().Execute(store);
        
        public void ClearDatabaseDirectory(string dataDir)
        {
            bool isRetry = false;

            while (true)
            {
                try
                {
                    IOExtensions.DeleteDirectory(dataDir);
                    break;
                }
                catch (IOException)
                {
                    if (isRetry)
                        throw;

                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    isRetry = true;

                    Thread.Sleep(2500);
                }
            }
        }

        public virtual void Dispose()
        {
            var errors = new List<Exception>();

            foreach (var store in stores)
            {
                try
                {
                    store.Dispose();
                }
                catch (Exception e)
                {
                    errors.Add(e);
                }
            }

            foreach (var server in servers)
            {
                try
                {
                    server.Dispose();
                }
                catch (Exception e)
                {
                    errors.Add(e);
                }
            }

            GC.Collect(2);
            GC.WaitForPendingFinalizers();

            foreach (var pathToDelete in pathsToDelete)
            {
                try
                {
                    ClearDatabaseDirectory(pathToDelete);
                }
                catch (Exception e)
                {
                    errors.Add(e);
                }
            }

            if (errors.Count > 0)
                throw new AggregateException(errors);
        }

        public void PrintServerErrors(ServerError[] serverErrors)
        {
            if (serverErrors.Any())
            {
                Console.WriteLine("Server errors count: " + serverErrors.Count());
                foreach (var serverError in serverErrors)
                {
                    Console.WriteLine("Server error: " + serverError.ToString());
                }
            }
            else
                Console.WriteLine("No server errors");
        }

        public void AssertNoIndexErrors(IDocumentStore documentStore)
        {
            var embeddableDocumentStore = documentStore as EmbeddableDocumentStore;
            var errors = embeddableDocumentStore != null
                ? embeddableDocumentStore.DocumentDatabase.Statistics.Errors
                : documentStore.DatabaseCommands.GetStatistics().Errors;

            try
            {
                Assert.Empty(errors);
            }
            catch (EmptyException)
            {
                Console.WriteLine(errors.First().Error);
                throw;
            }
        }
    }
}