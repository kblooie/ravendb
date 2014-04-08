using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Raven.Abstractions.Data;
using Raven.Abstractions.Extensions;
using Raven.Client;
using Raven.Client.Connection;
using Raven.Client.Embedded;
using Raven.Database;
using Raven.Database.Server;
using Raven.Json.Linq;
using Raven.Server;
using Xunit;

namespace Raven.Tests.Helpers.Extensions
{
    public static class DatabaseWaitTestExtensions
    {

        public static void WaitForIndexing(this IDocumentStore store, string db = null, TimeSpan? timeout = null)
        {
            var databaseCommands = store.DatabaseCommands;
            if (db != null)
                databaseCommands = databaseCommands.ForDatabase(db);
            bool spinUntil = SpinWait.SpinUntil(() => databaseCommands.GetStatistics().StaleIndexes.Length == 0, timeout ?? TimeSpan.FromSeconds(20));
            if (spinUntil == false)
                WaitForUserToContinueTheTest((EmbeddableDocumentStore)store);
            Assert.True(spinUntil);
        }

        public static void WaitForIndexing(this DocumentDatabase db)
        {
            Assert.True(SpinWait.SpinUntil(() => db.Statistics.StaleIndexes.Length == 0, TimeSpan.FromMinutes(5)));
        }

        public static void WaitForAllRequestsToComplete(this RavenDbServer server)
        {
            Assert.True(SpinWait.SpinUntil(() => server.Server.HasPendingRequests == false, TimeSpan.FromMinutes(15)));
        }

        public static void WaitForBackup(this DocumentDatabase db, bool checkError)
        {
            WaitForBackup(key => db.Get(key, null), checkError);
        }

        public static void WaitForBackup(this IDatabaseCommands commands, bool checkError)
        {
            WaitForBackup(commands.Get, checkError);
        }

        private static void WaitForBackup(Func<string, JsonDocument> getDocument, bool checkError)
        {
            var done = SpinWait.SpinUntil(() =>
            {
                // We expect to get the doc from database that we tried to backup
                var jsonDocument = getDocument(BackupStatus.RavenBackupStatusDocumentKey);
                if (jsonDocument == null)
                    return true;

                var backupStatus = jsonDocument.DataAsJson.JsonDeserialization<BackupStatus>();
                if (backupStatus.IsRunning == false)
                {
                    if (checkError)
                    {
                        var firstOrDefault =
                            backupStatus.Messages.FirstOrDefault(x => x.Severity == BackupStatus.BackupMessageSeverity.Error);
                        if (firstOrDefault != null)
                            Assert.False(true, firstOrDefault.Message);
                    }

                    return true;
                }
                return false;
            }, TimeSpan.FromMinutes(15));
            Assert.True(done);
        }

        public static void WaitForRestore(this IDatabaseCommands databaseCommands)
        {
            var done = SpinWait.SpinUntil(() =>
            {
                // We expect to get the doc from the <system> database
                var doc = databaseCommands.Get(RestoreStatus.RavenRestoreStatusDocumentKey);

                if (doc == null)
                    return false;

                var status = doc.DataAsJson["restoreStatus"].Values().Select(token => token.ToString()).ToList();

                var restoreFinishMessages = new[]
                {
                    "The new database was created",
                    "Esent Restore: Restore Complete", 
                    "Restore ended but could not create the datebase document, in order to access the data create a database with the appropriate name",
                };
                return restoreFinishMessages.Any(status.Last().Contains);
            }, TimeSpan.FromMinutes(5));

            Assert.True(done);
        }

        public static void WaitForDocument(this IDatabaseCommands databaseCommands, string id)
        {
            var done = SpinWait.SpinUntil(() =>
            {
                // We expect to get the doc from the <system> database
                var doc = databaseCommands.Get(id);
                return doc != null;
            }, TimeSpan.FromMinutes(5));

            Assert.True(done);
        }

        public static void WaitForUserToContinueTheTest(this EmbeddableDocumentStore documentStore, bool debug = true)
        {
            if (debug && Debugger.IsAttached == false)
                return;

            documentStore.SetStudioConfigToAllowSingleDb();

            documentStore.DatabaseCommands.Put("Pls Delete Me", null,

                RavenJObject.FromObject(new { StackTrace = new StackTrace(true) }),
                new RavenJObject());

            documentStore.Configuration.AnonymousUserAccessMode = AnonymousUserAccessMode.Admin;
            using (var server = new HttpServer(documentStore.Configuration, documentStore.DocumentDatabase))
            {
                server.StartListening();
                Process.Start(documentStore.Configuration.ServerUrl); // start the server

                do
                {
                    Thread.Sleep(100);
                } while (documentStore.DatabaseCommands.Get("Pls Delete Me") != null && (debug == false || Debugger.IsAttached));
            }
        }
    }
}
