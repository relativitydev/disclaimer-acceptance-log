using KCD_1042192.Utility;
using Relativity.API;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace KCD_1042192.MVC
{
    public class DisclaimerDatabase
    {
        public static bool AcceptDisclaimer(ICPHelper relativityConnectionHelper, Int32 userId, Int32 disclaimerId)
        {
            var parameters = new List<SqlParameter>{
                new SqlParameter { ParameterName = "@DisclaimerId", SqlDbType = SqlDbType.Int, Value = disclaimerId },
                new SqlParameter { ParameterName = "@UserId", SqlDbType = SqlDbType.Int, Value = userId },
                new SqlParameter { ParameterName = "@Date", SqlDbType = SqlDbType.DateTime, Value = Functions.FormatDateForSql(DateTime.UtcNow) }
            };

            var affectedRows = relativityConnectionHelper.GetDBContext(-1).ExecuteNonQuerySQLStatement(SQL.AcceptDisclaimer, parameters);

            return (affectedRows > 0);
        }

        public static bool LogError(ICPHelper relativityConnectionHelper, Int32 userId, String errorMsg)
        {
            var parameters = new List<SqlParameter>{
                new SqlParameter { ParameterName = "@Date", SqlDbType = SqlDbType.DateTime, Value = Functions.FormatDateForSql(DateTime.UtcNow) },
                new SqlParameter { ParameterName = "@UserId", SqlDbType = SqlDbType.Int, Value = userId },
                new SqlParameter { ParameterName = "@ErrorMessage", SqlDbType = SqlDbType.NVarChar, Value = errorMsg}
            };

            Int32 affectedRows = relativityConnectionHelper.GetDBContext(-1).ExecuteNonQuerySQLStatement(SQL.LogError, parameters);

            return (affectedRows > 0);
        }

        public static IEnumerable<Models.Disclaimer> GetEligibleDisclaimers(ICPHelper relativityConnectionHelper, Int32 userId)
        {
            var workspaceArtifactId = Functions.FindFirstWorkspaceWhereAppIsInstalled(relativityConnectionHelper.GetDBContext(-1), Constants.Guids.Applications.DisclaimerAcceptanceLog);
            var allDisclaimers = Functions.GetDisclaimers(workspaceArtifactId, relativityConnectionHelper).Where(x => x.Enabled).ToList();

            var eligibleDisclaimers = new List<Models.Disclaimer>();

            foreach (var disclaimer in allDisclaimers)
            {
                var usersWhoNeedToAcceptDislcaimer = Functions.FindUsersWhoNeedToAcceptDisclaimer(relativityConnectionHelper.GetDBContext(-1), disclaimer);
                if (usersWhoNeedToAcceptDislcaimer.Contains(userId))
                {
                    eligibleDisclaimers.Add(disclaimer);
                }
            }

            return eligibleDisclaimers;
        }
    }
}