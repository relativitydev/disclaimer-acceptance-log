using kCura.Relativity.Client;
using kCura.Relativity.Client.DTOs;
using Relativity.API;
using Relativity.Services.Objects;
using Relativity.Services.Objects.DataContracts;
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
          var queryRequest = new QueryRequest()
          {
            ObjectType = new ObjectTypeRef
						{
              ArtifactTypeID = 2
						},
            Fields = new List<FieldRef>
						{
              new FieldRef
							{
                Name = "Name"
							}
						}
          };
          List<int> userIds = new List<int>();
          using(IObjectManager objectManager = helper.GetServicesManager().CreateProxy<IObjectManager>(ExecutionIdentity.System))
			    {
            var queryResult = objectManager.QueryAsync(-1, queryRequest,0, 1000).Result;
            foreach(var obj in queryResult.Objects)
				    {
              userIds.Add(obj.ArtifactID);
				    }
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

            var readRequest = new ReadRequest
						{
              Object = new RelativityObjectRef { ArtifactID = applicationArtifactId },
              Fields = new List<FieldRef>
							{
                new FieldRef
								{
                  Name = "Locked"
								}
							},
						};
            
            using(IObjectManager objectManager = helper.GetServicesManager().CreateProxy<IObjectManager>(ExecutionIdentity.System))
			      {
              var readResult = objectManager.ReadAsync(workspaceArtifactId, readRequest).Result;
				      if (readResult.Object.FieldValues.Any())
				      {
                retVal = (bool)readResult.Object.FieldValues[0].Value;
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
			      var queryRequest = new QueryRequest()
            {
              ObjectType = new ObjectTypeRef
					  	{
                Guid = Constants.Guids.Objects.DisclaimerSolutionConfiguration
					  	},
              Fields = new List<FieldRef>
					  	{
                new FieldRef
					  		{
                  Guid = Constants.Guids.Fields.ConfigurationEnabled
                }
					  	},
              Sorts = new List<Relativity.Services.Objects.DataContracts.Sort>
					  	{
                new Relativity.Services.Objects.DataContracts.Sort
					  		{
                  FieldIdentifier = new FieldRef
					  			{
                    Name = "Artifact ID"
					  			},
                  Direction = Relativity.Services.Objects.DataContracts.SortEnum.Ascending
					  		}
					  	},
            };

            using(IObjectManager objectManager = helper.GetServicesManager().CreateProxy<IObjectManager>(ExecutionIdentity.System))
			      {
              var queryResult = objectManager.QueryAsync(workspaceArtifactId, queryRequest,0, 1000).Result;
              if(queryResult.TotalCount > 0)
				      {
                retVal = (bool)queryResult.Objects[0].FieldValues[0].Value;
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
            var queryRequest = new QueryRequest()
            {
              ObjectType = new ObjectTypeRef
					  	{
                Guid = Constants.Guids.Objects.Disclaimer
					  	},
              Fields = new List<FieldRef>
					  	{
                new FieldRef
					  		{
                  Guid = Constants.Guids.Fields.DisclaimerTitle
                },
                new FieldRef
                {
                  Guid = Constants.Guids.Fields.DisclaimerText
                },
                new FieldRef
                {
                  Guid = Constants.Guids.Fields.DisclaimerAllUsers
                },
                new FieldRef
                {
                  Guid = Constants.Guids.Fields.DisclaimerApplicableGroups
                },
                new FieldRef
                {
                  Guid = Constants.Guids.Fields.DisclaimerOrder
                },
                new FieldRef
                {
                  Guid = Constants.Guids.Fields.DisclaimerEnabled
                },
                new FieldRef
                {
                  Guid = Constants.Guids.Fields.DisclaimerReacceptancePeriod
                },
                new FieldRef
                {
                  Guid = Constants.Guids.Fields.DisclaimerStatus
                },
              },
              Sorts = new List<Relativity.Services.Objects.DataContracts.Sort>
					  	{
                new Relativity.Services.Objects.DataContracts.Sort
					  		{
                  FieldIdentifier = new FieldRef
					  			{
                    Guid = Constants.Guids.Fields.DisclaimerOrder
					  			},
                  Direction = Relativity.Services.Objects.DataContracts.SortEnum.Ascending
					  		}
					  	}
            };

            using(IObjectManager objectManager = helper.GetServicesManager().CreateProxy<IObjectManager>(ExecutionIdentity.System))
			      {
              var queryResult = objectManager.QueryAsync(workspaceArtifactId, queryRequest,0, 1000).Result;
              if(queryResult.TotalCount > 0)
				      {
                retVal.AddRange(BindQueryResultToDisclaimer(queryResult));
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
                    var queryRequest = new QueryRequest
										{
                      ObjectType = new ObjectTypeRef
											{
                        Guid = Constants.Guids.Objects.DislcaimerGroup
											},
                      Fields = new List<FieldRef>
											{
                        new FieldRef
												{
                          Guid = Constants.Guids.Fields.DisclaimerGroupsGroupArtifactId
												}
											},
										};

                    List<int> applicableGroups = new List<int>();
                    using(IObjectManager objectManager = helper.GetServicesManager().CreateProxy<IObjectManager>(ExecutionIdentity.System))
					          {
                      var queryResult = objectManager.QueryAsync(workspaceArtifactId, queryRequest, 0, 1000).Result;
                      if(queryResult.TotalCount > 0)
						          {
                        foreach(var obj in queryResult.Objects)
							          {
                          int artifactId = (int)obj.FieldValues[0].Value;
                          if(Array.Exists(disclaimerGroupIds, x => x == artifactId))
								          {
                            applicableGroups.Add(artifactId);
								          }
                        }
                        disclaimer.ApplicableGroups = applicableGroups;
						          }
						          else
						          {
                        throw new Exception("unable to retrieve groups for disclaimer " + disclaimer.DisclaimerId);
						          }
					          }
                }
            }
        }

        //Binds the results of a Disclaimer RDO query to the structure of this solution's disclaimer model
        private static IEnumerable<Models.Disclaimer> BindQueryResultToDisclaimer(Relativity.Services.Objects.DataContracts.QueryResult queryResults)
        {
            var retVal = new List<Models.Disclaimer>();
            foreach(var obj in queryResults.Objects)
			      {
              bool allUsers = false;
              if(obj.FieldValues[2].Value != null) allUsers = (bool)obj.FieldValues[2].Value;

              IEnumerable<int> applicableGroups = new List<int>();
              if(obj.FieldValues[3].Value != null) applicableGroups = ((List<RelativityObjectValue>)obj.FieldValues[3].Value).Select(x => x.ArtifactID);

              int order = 0;
              if(obj.FieldValues[4].Value != null) order = (int)obj.FieldValues[4].Value;

              bool enabled = false;
              if(obj.FieldValues[5].Value != null) enabled = (bool)obj.FieldValues[5].Value;

              int reacceptancePeriod = 0;
              if(obj.FieldValues[6].Value != null) reacceptancePeriod = (int)obj.FieldValues[6].Value;

              retVal.Add(new Models.Disclaimer
							{
                DisclaimerId = obj.ArtifactID,
                DisclaimerTitle = (string)obj.FieldValues[0].Value,
                DisclaimerText = (string)obj.FieldValues[1].Value,
                AllUsers = allUsers,
                ApplicableGroups = applicableGroups,
                Order = order,
                Enabled = enabled,
                ReacceptancePeriod = reacceptancePeriod,
                Status = (Relativity.Services.Objects.DataContracts.Choice)obj.FieldValues[7].Value
							});
			      }

            return retVal;
        }

        #endregion Functions specific to the disclaimer solution
    }
}