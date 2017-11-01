using kCura.Relativity.Client;
using kCura.Relativity.Client.DTOs;
using Relativity.API;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;

namespace KCD_1042192.Utility
{
    public class Functions
    {
        #region General Fuctions

        //Convert date into general sql format string
        public static string FormatDateForSql(DateTime time)
        {
            return time.ToString("yyyy-MM-dd HH:mm:ss");
        }

        // Gets the Relativity version number from the assembly
        public static Version GetRelativityVersion(Type type)
        {
            Version currentRelativityVersion = Assembly.GetAssembly(type).GetName().Version;
            return currentRelativityVersion;
        }

        //Returns all the User ArtifactIds in the Environment
        public static IEnumerable<Int32> GetAllUserIds(IHelper helper)
        {
            var userIds = new List<Int32>();
            var query = new Query<kCura.Relativity.Client.DTOs.User>
            {
                Fields = FieldValue.NoFields
            };

            using (var proxy = helper.GetServicesManager().CreateProxy<IRSAPIClient>(ExecutionIdentity.System))
            {
                proxy.APIOptions = new APIOptions { WorkspaceID = -1 };
                var results = QuerySubset.PerformQuerySubset(proxy.Repositories.User, query, 1000);
                userIds.AddRange(results.Select(x => x.Artifact.ArtifactID).ToList());
            }
            return userIds;
        }

        //Returns the artifactId of a particular Guid
        public static Int32 FindArtifactIdByGuid(IDBContext workspaceContext, Guid guid)
        {
            var parameters = new List<SqlParameter>{
                    new SqlParameter { ParameterName = "@Guid", SqlDbType = SqlDbType.UniqueIdentifier, Value = guid }
                };
            var artifactIdQuery = workspaceContext.ExecuteSqlStatementAsDataTable(SQL.ArtifactIdByGuid, parameters);

            if (artifactIdQuery.Rows.Count < 1)
            {
                throw new Exception("Guid does not exist in workspace: " + guid);
            }
            return (Int32)artifactIdQuery.Rows[0]["ArtifactID"];
        }

        //Returns the first ArtifactId of the workspace that has the oldest install of the provided App Guid
        public static Int32 FindFirstWorkspaceWhereAppIsInstalled(IDBContext eddsDbContext, Guid appGuid)
        {
            var applicationGuid = appGuid;
            var parameters = new List<SqlParameter>{
                    new SqlParameter { ParameterName = "@ApplicationGuid", SqlDbType = SqlDbType.UniqueIdentifier, Value = applicationGuid }
                };
            var workspaceQuery = eddsDbContext.ExecuteSqlStatementAsDataTable(SQL.WorkspacesWhereAppIsInstalled, parameters);

            if (workspaceQuery.Rows.Count < 1)
            {
                throw new Exceptions.DisclaimerAppIsNotInstalledExcepetion();
            }
            return (Int32)workspaceQuery.Rows[0]["CaseID"];
        }

        //Checks if the Application associated with an object is locked
        public static Boolean ApplicationIsLocked(IHelper helper, Int32 workspaceArtifactId, Guid associatedApplicationObject)
        {
            var retVal = false;
            var applicationArtifactId = FindApplicationArtifactIdOfRelatedObject(helper.GetDBContext(workspaceArtifactId), associatedApplicationObject);

            var app = new kCura.Relativity.Client.DTOs.RelativityApplication(applicationArtifactId)
            {
                Fields = new List<FieldValue> { new FieldValue(RelativityApplicationFieldNames.Locked) }
            };

            using (var proxy = helper.GetServicesManager().CreateProxy<IRSAPIClient>(ExecutionIdentity.System))
            {
                proxy.APIOptions = new APIOptions { WorkspaceID = workspaceArtifactId };
                var results = proxy.Repositories.RelativityApplication.Read(app);
                if (results.Success && results.Results.Any())
                {
                    retVal = results.Results[0].Artifact.Locked.GetValueOrDefault(false);
                }
            }

            return retVal;
        }

        //Get the ArtifactId of an Application that is associated with a particular object
        public static Int32 FindApplicationArtifactIdOfRelatedObject(IDBContext workspaceContext, Guid associatedApplicationObject)
        {
            var parameters = new List<SqlParameter>{
                new SqlParameter { ParameterName = "@ObjectGuid", SqlDbType = SqlDbType.UniqueIdentifier, Value = associatedApplicationObject }
            };

            return workspaceContext.ExecuteSqlStatementAsScalar<Int32>(SQL.GetApplicationArtifactIdOfRelatedObject, parameters);
        }

        #endregion General Fuctions

        #region Functions specific to the disclaimer solution

        //Make sure the Disclaimer App is enabled in the workspace
        public static Boolean DisclaimerSolutionIsEnabled(IHelper helper, Int32 workspaceArtifactId)
        {
            var retVal = false;
            using (var proxy = helper.GetServicesManager().CreateProxy<IRSAPIClient>(ExecutionIdentity.System))
            {
                proxy.APIOptions = new APIOptions { WorkspaceID = workspaceArtifactId };
                var query = new Query<RDO>
                {
                    ArtifactTypeGuid = Constants.Guids.Objects.DisclaimerSolutionConfiguration,
                    Condition = new BooleanCondition(Constants.Guids.Fields.ConfigurationEnabled, BooleanConditionEnum.EqualTo, true),
                    Sorts = new List<Sort> { new Sort { Field = ArtifactQueryFieldNames.ArtifactID, Direction = SortEnum.Ascending } },
                    Fields = new List<FieldValue> { new FieldValue(Constants.Guids.Fields.ConfigurationEnabled) }
                };

                if (proxy.Repositories.RDO.Query(query).Results.Count > 0)
                {
                    retVal = true;
                }
            }
            return retVal;
        }

        //Returns a list of UserIds that represent the users that need to accept the disclaimer
        public static IEnumerable<Int32> FindUsersWhoNeedToAcceptDisclaimer(IDBContext eddsDbContext, Models.Disclaimer disclaimer)
        {
            var usersWhoNeedToAccept = new List<Int32>();
            var groupListFormattedForSql = String.Join(",", disclaimer.ApplicableGroups);
            var retrieveEligibleUsers = false;
            var query = string.Empty;
            var parameters = new List<SqlParameter>{
                new SqlParameter { ParameterName = "@DisclaimerArtifactID", SqlDbType = SqlDbType.Int, Value = disclaimer.DisclaimerId }
            };

            if (disclaimer.AllUsers && disclaimer.ReacceptancePeriod > 0)
            {
                query = SQL.NonAccepptedAllUsersTimeCriteria;
                parameters.Add(new SqlParameter { ParameterName = "@ReacceptancePeriod", SqlDbType = SqlDbType.Int, Value = disclaimer.ReacceptancePeriod });
                retrieveEligibleUsers = true;
            }
            else if (disclaimer.AllUsers)
            {
                query = SQL.NonAccepptedAllUsers;
                retrieveEligibleUsers = true;
            }
            else if (disclaimer.ReacceptancePeriod > 0 && disclaimer.ApplicableGroups.Any())
            {
                query = String.Format(SQL.NonAcceptedUsersByGroupsTimeCriteria, groupListFormattedForSql);
                parameters.Add(new SqlParameter { ParameterName = "@ReacceptancePeriod", SqlDbType = SqlDbType.Int, Value = disclaimer.ReacceptancePeriod });
                retrieveEligibleUsers = true;
            }
            else if (disclaimer.ApplicableGroups.Any())
            {
                query = String.Format(SQL.NonAcceptedUsersByGroups, groupListFormattedForSql);
                retrieveEligibleUsers = true;
            }

            if (retrieveEligibleUsers)
            {
                usersWhoNeedToAccept.AddRange(eddsDbContext.ExecuteSqlStatementAsDataTable(query, parameters).AsEnumerable().Select(x => Int32.Parse(x["ArtifactID"].ToString())).ToList());
            }

            return usersWhoNeedToAccept;
        }

        //Returns a list of Workspace Disclaimers in the struture of this solution's Disclaimer Model
        public static IEnumerable<Models.Disclaimer> GetDisclaimers(Int32 workspaceArtifactId, IHelper helper)
        {
            var retVal = new List<Models.Disclaimer>();

            using (var proxy = helper.GetServicesManager().CreateProxy<IRSAPIClient>(ExecutionIdentity.System))
            {
                proxy.APIOptions = new APIOptions { WorkspaceID = workspaceArtifactId };
                var query = new Query<RDO>
                {
                    ArtifactTypeGuid = Constants.Guids.Objects.Disclaimer,
                    Sorts = new List<Sort>{
                            new Sort { Guid = Constants.Guids.Fields.DisclaimerOrder, Direction = SortEnum.Ascending}
                        },
                    Fields = FieldValue.AllFields
                };

                var queryResults = proxy.Repositories.RDO.Query(query);

                if (queryResults.Results.Count > 0)
                {
                    retVal.AddRange(BindQueryResultToDisclaimer(queryResults));
                    //At this point out Models.group has the artifactIds of Disclaimer Group RDO, here we're gonna swap them out for Relativity group Artifact Ids
                    ReplaceWorkspaceGroupIdsWithRelativityGroupIds(workspaceArtifactId, retVal, helper);
                }
            }
            return retVal;
        }

        //Takes a list of Workspace disclaimers that currently have the workspace group ArtifactIds associated with them, this function replaces the workspace level group Id with the EDDS level Group Id
        private static void ReplaceWorkspaceGroupIdsWithRelativityGroupIds(Int32 workspaceArtifactId, IEnumerable<Models.Disclaimer> allDisclaimers, IHelper helper)
        {
            foreach (var disclaimer in allDisclaimers)
            {
                if (disclaimer.ApplicableGroups.Any())
                {
                    var disclaimerGroupIds = disclaimer.ApplicableGroups.AsEnumerable().Select(x => x).ToArray();
                    using (var proxy = helper.GetServicesManager().CreateProxy<IRSAPIClient>(ExecutionIdentity.System))
                    {
                        proxy.APIOptions = new APIOptions { WorkspaceID = workspaceArtifactId };
                        var query = new Query<RDO>
                        {
                            ArtifactTypeGuid = Constants.Guids.Objects.DislcaimerGroup,
                            Condition = new WholeNumberCondition(ArtifactQueryFieldNames.ArtifactID, NumericConditionEnum.In, disclaimerGroupIds),
                            Fields = new List<FieldValue> {
                                    new FieldValue(Constants.Guids.Fields.DisclaimerGroupsGroupArtifactId)
                                }
                        };

                        var queryResults = proxy.Repositories.RDO.Query(query);

                        if (queryResults.Success && queryResults.Results.Any())
                        {
                            disclaimer.ApplicableGroups = queryResults.Results.AsEnumerable().Select(x => x.Artifact[Constants.Guids.Fields.DisclaimerGroupsGroupArtifactId].ValueAsWholeNumber.GetValueOrDefault(0)).ToArray();
                        }
                        else if (!queryResults.Success)
                        {
                            throw new Exception("unable to retrieve groups for disclaimer " + disclaimer.DisclaimerId + " " + queryResults.Message);
                        }
                    }
                }
            }
        }

        //Binds the results of a Disclaimer RDO query to the structure of this solution's disclaimer model
        private static IEnumerable<Models.Disclaimer> BindQueryResultToDisclaimer(ResultSet<RDO> queryResults)
        {
            var retVal = queryResults.Results.AsEnumerable().Select(x => new Models.Disclaimer
            {
                DisclaimerId = x.Artifact.ArtifactID,
                DisclaimerTitle = x.Artifact[Constants.Guids.Fields.DisclaimerTitle].ValueAsFixedLengthText,
                DisclaimerText = x.Artifact[Constants.Guids.Fields.DisclaimerText].ValueAsLongText,
                AllUsers = x.Artifact[Constants.Guids.Fields.DisclaimerAllUsers].ValueAsYesNo.GetValueOrDefault(false),
                ApplicableGroups = x.Artifact[Constants.Guids.Fields.DisclaimerApplicableGroups].GetValueAsMultipleObject<kCura.Relativity.Client.DTOs.Artifact>().Select(y => y.ArtifactID).ToList(),
                Order = x.Artifact[Constants.Guids.Fields.DisclaimerOrder].ValueAsWholeNumber.GetValueOrDefault(0),
                Enabled = x.Artifact[Constants.Guids.Fields.DisclaimerEnabled].ValueAsYesNo.GetValueOrDefault(false),
                ReacceptancePeriod = x.Artifact[Constants.Guids.Fields.DisclaimerReacceptancePeriod].ValueAsWholeNumber.GetValueOrDefault(0),
                Status = x.Artifact[Constants.Guids.Fields.DisclaimerStatus].ValueAsSingleChoice
            }).ToList();

            return retVal;
        }

        #endregion Functions specific to the disclaimer solution
    }
}