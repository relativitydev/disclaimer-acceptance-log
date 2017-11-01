using KCD_1042192.Utility;
using kCura.EventHandler;
using kCura.EventHandler.CustomAttributes;
using System;
using System.Runtime.InteropServices;

namespace KCD_1042192.EventHandlers
{
    [Description("KCD_1042192.Pre UnInstall EventHandler")]
    [Guid("554c300d-a91c-49fd-93e1-d5e55e91ab3c")]
    public class AppPreUninstall : PreUninstallEventHandler, IDataEnabled
    {
        public override Response Execute()
        {
            //Construct a response object with default values.
            var retVal = new Response { Success = true, Message = String.Empty };

            try
            {
                var workspaceContext = Helper.GetDBContext(-1);
                var currentRelativityVersion = Functions.GetRelativityVersion(typeof(kCura.EventHandler.Field)).ToString();
                var relativityVersion = new Version(currentRelativityVersion);
                var supportedRelativityVersion = new Version(KCD_1042192.Utility.Constants.OtherConstants.RelativityVersion.September94Release);

                if (relativityVersion >= supportedRelativityVersion)
                {
                    workspaceContext.ExecuteNonQuerySQLStatement(SQL.DisableDisclaimerSolutionSept);
                }
                else
                {
                    workspaceContext.ExecuteNonQuerySQLStatement(SQL.DisableDisclaimerSolution);
                }
            }
            catch (Exception ex)
            {
                retVal.Success = false;
                retVal.Message = "Unable to completly deactivate the Disclaimer Log Solution. " +
                                 "Please refer to the troubleshooting section of the documentation to fully deactivate disclaimers in Relativity. " +
                                 ex.Message;
            }

            return retVal;
        }
    }
}