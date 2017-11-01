using KCD_1042192.Utility;
using kCura.EventHandler;
using kCura.EventHandler.CustomAttributes;
using Relativity.API;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Runtime.InteropServices;

namespace KCD_1042192.EventHandlers
{
    [Description("KCD_1042192.Pre Install EventHandler")]
    [Guid("75797e0b-00eb-490b-b101-cdab7fadfd94")]
    public class AppPreInstallEv : PreInstallEventHandler, IDataEnabled
    {
        public override Response Execute()
        {
            var retVal = new Response { Success = false, Message = String.Empty };

            try
            {
                var eddsDbContext = Helper.GetDBContext(-1);

                if (ApplicationInstalledInAnotherWorkspace(eddsDbContext))
                {
                    retVal.Message = "This application can only be installed successfully in one workspace";
                }
                else
                {
                    eddsDbContext.ExecuteNonQuerySQLStatement(SQL.CreateDisclaimerLogTable);
                    eddsDbContext.ExecuteNonQuerySQLStatement(SQL.CreateDislcaimerArchiveTable);
                    eddsDbContext.ExecuteNonQuerySQLStatement(SQL.CreateDisclaimerErrorTable);
                    retVal.Success = true;
                }
            }
            catch (Exception ex)
            {
                retVal.Success = false;
                retVal.Message = ex.ToString();
            }

            return retVal;
        }

        private Boolean ApplicationInstalledInAnotherWorkspace(IDBContext eddsDbContext)
        {
            var currentWorkspaceId = Helper.GetActiveCaseID();
            var applicationGuid = Utility.Constants.Guids.Applications.DisclaimerAcceptanceLog;

            var workspaceParameter = new SqlParameter { ParameterName = "@ApplicationGuid", SqlDbType = SqlDbType.UniqueIdentifier, Value = applicationGuid };
            var firstWorkspaceWhereAppIsInstalled = (int?)eddsDbContext.ExecuteSqlStatementAsScalar(SQL.WorkspacesWhereAppIsInstalled, workspaceParameter);
            return (firstWorkspaceWhereAppIsInstalled != null && firstWorkspaceWhereAppIsInstalled != currentWorkspaceId);
        }
    }
}