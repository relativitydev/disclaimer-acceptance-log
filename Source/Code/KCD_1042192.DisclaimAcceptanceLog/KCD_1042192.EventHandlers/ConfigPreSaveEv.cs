using KCD_1042192.Utility;
using kCura.EventHandler;
using kCura.EventHandler.CustomAttributes;
using kCura.Relativity.Client;
using kCura.Relativity.Client.DTOs;
using Relativity.API;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.InteropServices;

namespace KCD_1042192.EventHandlers
{
    [Description("Pre Save EventHandler")]
    [Guid("56ff504e-190f-4ebb-a96d-0151d94fa062")]
    public class ConfigPreSaveEv : PreSaveEventHandler, IDataEnabled
    {
        private const string ErrorMaxInstances = "Save aborted, only one Disclaimer Config object is allowed";

        public override Response Execute()
        {
            var retVal = new Response { Success = true, Message = String.Empty };
            var enabled = (bool?)ActiveArtifact.Fields["Enabled"].Value.Value;
            var allowAccessOnError = (bool?)ActiveArtifact.Fields["Allow Access On Error"].Value.Value;
            var eddsDbContext = Helper.GetDBContext(-1);

            try
            {
                //Only allow a single Config RDO to be created
                if (FirstConfigObject(retVal))
                {
                    ToggleSolution(eddsDbContext, enabled, allowAccessOnError);
                }
                else
                {
                    retVal.Message = ErrorMaxInstances;
                }
            }
            catch (Exception ex)
            {
                //Change the response Success property to false to let the user know an error occurred
                retVal.Success = false;
                retVal.Message = ex.ToString();
            }

            return retVal;
        }

        //Throw exception if this is not the first config object
        private Boolean FirstConfigObject(Response response)
        {
            var retValue = true;
            using (var proxy = Helper.GetServicesManager().CreateProxy<IRSAPIClient>(ExecutionIdentity.System))
            {
                proxy.APIOptions = new APIOptions { WorkspaceID = Helper.GetActiveCaseID() };

                var query = new Query<RDO>
                {
                    ArtifactTypeGuid = Utility.Constants.Guids.Objects.DisclaimerSolutionConfiguration,
                    Fields = kCura.Relativity.Client.DTOs.FieldValue.NoFields
                };

                var results = proxy.Repositories.RDO.Query(query);
                if (results.Success && results.Results.Any())
                {
                    var configObjectsArtIds = results.Results.AsEnumerable().Select(x => x.Artifact.ArtifactID).ToList();
                    if (!configObjectsArtIds.Contains(ActiveArtifact.ArtifactID))
                    {
                        response.Success = false;
                        response.Message = ErrorMaxInstances;
                        retValue = false;
                    }
                }
                else if (!results.Success)
                {
                    throw new Exception("Unable to Query Config objects. " + results.Message);
                }
                return retValue;
            }
        }

        private void ToggleSolution(IDBContext eddsDbContext, bool? enabled, bool? allowAccessOnError)
        {
            var currentRelativityVersion = Functions.GetRelativityVersion(typeof(kCura.EventHandler.Application)).ToString();
            var relativityVersion = new Version(currentRelativityVersion);
            var supportedRelativityVersion = new Version(KCD_1042192.Utility.Constants.OtherConstants.RelativityVersion.September94Release);
            if (enabled == true)
            {
                var allowAccessInsert = allowAccessOnError.GetValueOrDefault(false).ToString().ToLower();

                //THe html has many curly braces that confuse format, so String.replace was used
                var loginPageHtml = HTML.LoginPage.Replace("{0}", Utility.Constants.Guids.Applications.DisclaimerAcceptanceLog.ToString());
                loginPageHtml = loginPageHtml.Replace("{1}", allowAccessInsert);

                var parameters = new List<SqlParameter>{
                    new SqlParameter { ParameterName = "@HTML", SqlDbType = SqlDbType.NVarChar, Value = loginPageHtml }
                };

                //var currentRelativityVersion = Functions.GetAssemblyVersion(typeof(kCura.EventHandler.Application)).ToString();

                if (relativityVersion >= supportedRelativityVersion)
                {
                    eddsDbContext.ExecuteNonQuerySQLStatement(SQL.EnableDisclaimerSolutionSept, parameters);
                }
                else
                {
                    eddsDbContext.ExecuteNonQuerySQLStatement(SQL.EnableDisclaimerSolution, parameters);
                }
                //eddsDbContext.ExecuteNonQuerySQLStatement(SQL.EnableDisclaimerSolution, parameters);
            }
            else
            {
                if (relativityVersion >= supportedRelativityVersion)
                {
                    eddsDbContext.ExecuteNonQuerySQLStatement(SQL.DisableDisclaimerSolutionSept);
                }
                else
                {
                    eddsDbContext.ExecuteNonQuerySQLStatement(SQL.DisableDisclaimerSolution);
                }
                //eddsDbContext.ExecuteNonQuerySQLStatement(SQL.DisableDisclaimerSolution);
            }
        }

        public override FieldCollection RequiredFields
        {
            get
            {
                var retVal = new FieldCollection{
                       new kCura.EventHandler.Field(Utility.Constants.Guids.Fields.ConfigurationEnabled),
                       new kCura.EventHandler.Field(Utility.Constants.Guids.Fields.ConfigurationAllowAccessOnError)
                };
                return retVal;
            }
        }
    }
}