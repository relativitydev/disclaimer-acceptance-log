using KCD_1042192.Utility;
using kCura.EventHandler;
using kCura.EventHandler.CustomAttributes;
using Relativity.API;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Runtime.InteropServices;

namespace KCD_1042192.EventHandlers
{
    [Description("KCD_1042192.Pre Delete EventHandler")]
    [Guid("e680658a-d7b4-409f-b1d1-d6dddd6631e8")]
    public class DisclaimerPreDeleteEv : PreDeleteEventHandler, IDataEnabled
    {
        public override Response Execute()
        {
            var retVal = new Response { Success = false, Message = String.Empty };

            try
            {
                var workspaceDb = Helper.GetDBContext(Helper.GetActiveCaseID());
                var currentArtifactId = ActiveArtifact.ArtifactID;

                //Move the disclaimer to an archive after deletion so it's imformation is still available
                retVal.Success = MoveDisclaimerToArchive(workspaceDb, currentArtifactId);

                if (!retVal.Success)
                {
                    retVal.Message = "Unable to Move Disclaimer to Archive";
                }
            }
            catch (Exception ex)
            {
                retVal.Success = false;
                retVal.Message = ex.ToString();
            }

            return retVal;
        }

        private Boolean MoveDisclaimerToArchive(IDBContext workspaceDb, Int32 currentArtifactId)
        {
            var disclaimerObjTableName = GetDisclaimerObjTableName(workspaceDb, Utility.Constants.Guids.Objects.Disclaimer);
            var moveDisclaimerSqlCommand = String.Format(SQL.MoveDisclaimerToArchive, disclaimerObjTableName);

            var parameters = new List<SqlParameter>
            {
                new SqlParameter { ParameterName = "@CurrentArtifactID", SqlDbType = SqlDbType.Int, Value = currentArtifactId }
            };

            var affectedRows = workspaceDb.ExecuteNonQuerySQLStatement(moveDisclaimerSqlCommand, parameters);

            return (affectedRows > 0);
        }

        private string GetDisclaimerObjTableName(IDBContext workspaceDb, Guid discObjGuid)
        {
            var parameter = new SqlParameter { ParameterName = "@DisclaimerObjGuid", SqlDbType = SqlDbType.UniqueIdentifier, Value = discObjGuid };

            return (string)workspaceDb.ExecuteSqlStatementAsScalar(SQL.GetDisclaimerObjTableName, parameter);
        }

        public override FieldCollection RequiredFields
        {
            get
            {
                var fields = new FieldCollection();
                return fields;
            }
        }

        public override void Rollback()
        {
        }

        public override void Commit()
        {
        }
    }
}