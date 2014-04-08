// -----------------------------------------------------------------------
//  <copyright file="DocumentDatabaseTestExtensions.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Raven.Abstractions.Data;
using Raven.Database;
using Raven.Database.Server.Security;
using Xunit;

namespace Raven.Tests.Helpers.Extensions
{
    public static class DocumentDatabaseTestExtensions
    {
        public static void EnableAuthentication(this DocumentDatabase database)
        {
            var license = GetLicenseByReflection(database);
            license.Error = false;
            license.Status = "Commercial";

            // rerun this startup task
            database.StartupTasks.OfType<AuthenticationForCommercialUseOnly>().First().Execute(database);
        }
        
        public static LicensingStatus GetLicenseByReflection(this DocumentDatabase database)
        {
            var field = database.GetType().GetField("validateLicense", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            var validateLicense = field.GetValue(database);

            var currentLicenseProp = validateLicense.GetType().GetProperty("CurrentLicense", BindingFlags.Static | BindingFlags.Public);
            Assert.NotNull(currentLicenseProp);

            return (LicensingStatus)currentLicenseProp.GetValue(validateLicense, null);
        }
    }
}