using KCD_1042192.Utility;
using kCura.Agent;
using kCura.Agent.CustomAttributes;
using kCura.Relativity.Client;
using kCura.Relativity.Client.DTOs;
using Relativity.API;
using Relativity.Services.Objects;
using Relativity.Services.Objects.DataContracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace KCD_1042192.DisclaimerAgent
{
    [Name("Disclaimer Acceptance Agent")]
    [Guid("28011d89-29f1-425d-ae5f-c8acadccccdd")]
    public class Kcd1042192DisclaimerAgent : AgentBase
    {
        public override void Execute()
        {
            var eddsDbContext = Helper.GetDBContext(-1);
            try
            {
                //The Disclaimer Application is only allowed to be installed in a single workspace, here we retrieve that workspace's artifactID
                RaiseMessage("Looking for workspace where Disclaimer Acceptance Log is installed", 10);
                var workspaceArtifactID = Functions.FindFirstWorkspaceWhereAppIsInstalled(eddsDbContext, Utility.Constants.Guids.Applications.DisclaimerAcceptanceLog);

                //This solution needs to maintains a representation of the Admin level groups in the workspace
                RaiseMessage(String.Format("Maintaining Groups in workspace: {0}", workspaceArtifactID), 10);
                WorkspaceLevelGroups.MaintainWorkspaceLevelGroups(Helper, workspaceArtifactID, Utility.Constants.Guids.Objects.DislcaimerGroup, Utility.Constants.Guids.Fields.DisclaimerGroupsGroupName, Utility.Constants.Guids.Fields.DisclaimerGroupsGroupArtifactId);

                //Only update Enable/Disable disclaimer's if the solution is enabled
                if (Functions.DisclaimerSolutionIsEnabled(Helper, workspaceArtifactID))
                {
                    //The solution is enabled so we update the [Edds].[Eddsdbo].[User].[HasAgreedToTermsOfUse] value
                    //to control who is prompted to accept a disclaimer upon logging into Relativity
                    RaiseMessage(String.Format("Retrieving Disclaimers in workspace: {0}", workspaceArtifactID), 10);
                    var disclaimers = Functions.GetDisclaimers(workspaceArtifactID, Helper).ToList();

                    if (disclaimers.Any())
                    {
                        RaiseMessage("Updating User Acceptance", 10);
                        UpdateUserAcceptance(eddsDbContext, disclaimers);
                        RaiseMessage("Looking for new disclaimers to activate", 10);
                        MaintainDisclaimerQueueStatus(workspaceArtifactID, Helper.GetDBContext(workspaceArtifactID), disclaimers);
                    }
					          else
					          {
                      RaiseMessage(String.Format("No Disclaimers in workspace: {0}", workspaceArtifactID), 10);
					          }
                }
                else
                {
                    RaiseMessage(String.Format("Disclaimer Acceptance Log is disabled in workspace: {0}", workspaceArtifactID), 10);
                }
            }
            catch (Exceptions.DisclaimerAppIsNotInstalledExcepetion)
            {
                RaiseMessage("Please install the Disclaimer Application into a workspace", 10);
            }
            catch (Exception ex)
            {
                RaiseError("Disclaimer Agent Failed: " + ex.Message, ex.InnerException.Message);
            }
        }

        /// <summary>
        /// The pre-save EventHandler for disclaimers changes it's status to 'Queued for Activation/De-Activation'
        /// Depending on the the value of the 'Enabled' yes/no field. At this point the user's [Edds].[Eddsdbo].[User].[HasAgreedToTermsOfUse] field has been updated,
        /// Therefore we update the user on the status of Disclaimer's activation status.
        /// 'Queued for Activation' changes to -> Active
        /// 'Queued for De-Activation' changes to a blank valuee
        /// </summary>
        /// <param name="workspaceArtifactId">The workspace artifactId of where the solution is installed</param>
        /// <param name="workspaceContext">DB context used to get the ArtifactID of choice values</param>
        /// <param name="allDisclaimers">A list of all the disclaimer objects in the workspace</param>
        private void MaintainDisclaimerQueueStatus(Int32 workspaceArtifactId, IDBContext workspaceContext, IEnumerable<Models.Disclaimer> allDisclaimers)
        {
            var fieldStatus = Utility.Constants.Guids.Fields.DisclaimerStatus;
            var choiceDisclaimerActive = Utility.Constants.Guids.Choices.DisclaimerActive;

            //ArtifactIds of these choices so we can compare their fieldvalue
            var choiceQueuedForActivation = Functions.FindArtifactIdByGuid(workspaceContext, Utility.Constants.Guids.Choices.DisclaimerQueuedForActivation);
            var choiceQueuedForDeactivation = Functions.FindArtifactIdByGuid(workspaceContext, Utility.Constants.Guids.Choices.DisclaimerQueuedForDeactivation);

            var disclaimersToSetActiveStatus = new List<Models.Disclaimer>();
            var disclaimersToSetDisabledStatus = new List<Models.Disclaimer>();

            foreach (var disclaimer in allDisclaimers)
            {
                if (disclaimer.Status != null)
                {
                    if (disclaimer.Status.ArtifactID == choiceQueuedForActivation)
                    {
                        disclaimersToSetActiveStatus.Add(disclaimer);
                    }
                    else if (disclaimer.Status.ArtifactID == choiceQueuedForDeactivation)
                    {
                        disclaimersToSetDisabledStatus.Add(disclaimer);
                    }
                }
            }
            RelativityObjectRef activeStatusChoice = new RelativityObjectRef
						{
              Guid = choiceDisclaimerActive
						};
            UpdateDisclaimerQueueStatus(workspaceArtifactId, disclaimersToSetActiveStatus, activeStatusChoice);
            UpdateDisclaimerQueueStatus(workspaceArtifactId, disclaimersToSetDisabledStatus, null);
        }

        private void UpdateDisclaimerQueueStatus(Int32 workspaceArtifactId, IEnumerable<Models.Disclaimer> disclaimersToUpdate, RelativityObjectRef relativityObjectRef)
        {
            if (disclaimersToUpdate.Any())
            {
                var massUpdateRequest = new MassUpdateByObjectIdentifiersRequest();
                List<RelativityObjectRef> objects = new List<RelativityObjectRef>();
                foreach(var disclaimer in disclaimersToUpdate)
				        {
                  objects.Add(new RelativityObjectRef
                  {
                    ArtifactID = disclaimer.DisclaimerId
                  });
				        }

                List<FieldRefValuePair> fieldRefValuePairs = new List<FieldRefValuePair>
								{
                  new FieldRefValuePair
									{
                    Field = new FieldRef
										{
                      Guid = Utility.Constants.Guids.Fields.DisclaimerStatus
                    },
                    Value = relativityObjectRef
                  }
								};
                massUpdateRequest.Objects = objects;
                massUpdateRequest.FieldValues = fieldRefValuePairs;

                using(IObjectManager objectManager = Helper.GetServicesManager().CreateProxy<IObjectManager>(ExecutionIdentity.System))
				        {
                  var updateResult = objectManager.UpdateAsync(workspaceArtifactId, massUpdateRequest).Result;
					        if (!updateResult.Success)
					        {
                    throw new Exception("Unable to updated disclaimer queue status: " + updateResult.Message);
					        }
				        }
            }
        }

        /// <summary>
        /// Updates the user's [Edds].[Eddsdbo].[User].[HasAgreedToTermsOfUse] for all users. If a users in not eligible to be
        /// presented with a disclaimer their value is set to 0, other wise it is set to 1. This same logic is uses to present
        /// disclaimers to users only in specific groups
        /// </summary>
        /// <param name="eddsDbContext">Admin level DB Context</param>
        /// <param name="alldisclaimers">List of enabled disclaimers</param>
        private void UpdateUserAcceptance(IDBContext eddsDbContext, IEnumerable<Models.Disclaimer> alldisclaimers)
        {
            var usersWhoNeedToAccept = new List<Int32>();
            var allRelativityUserIds = Functions.GetAllUserIds(Helper);

            var enabledDisclaimers = alldisclaimers.Where(x => x.Enabled).ToList();
            //figure out which users need to accept each disclaimer and add their ArtifactIds to a list
            foreach (var disclaimer in enabledDisclaimers)
            {
                usersWhoNeedToAccept.AddRange(Functions.FindUsersWhoNeedToAcceptDisclaimer(eddsDbContext, disclaimer));
            }

            //The users collected from above will be flagged for disclaimer acceptance
            if (usersWhoNeedToAccept.Any())
            {
                var formattedUsersWhoNeedToAccept = String.Join(",", usersWhoNeedToAccept.Distinct());
                var updateStatmentForUsersWhoNeedToAccept = String.Format(SQL.FlagUsersWhoNeedToAccept, formattedUsersWhoNeedToAccept);
                eddsDbContext.ExecuteNonQuerySQLStatement(updateStatmentForUsersWhoNeedToAccept);
            }

            //every other user is reset and are exempt
            var exemptUsers = allRelativityUserIds.Except(usersWhoNeedToAccept).ToList();
            if (exemptUsers.Any())
            {
                var formattedUsersWhoAreExempt = String.Join(",", exemptUsers);
                var updateStatmentForExemptUsers = String.Format(SQL.FlagUsersWhoAlreadyAccepted, formattedUsersWhoAreExempt);
                eddsDbContext.ExecuteNonQuerySQLStatement(updateStatmentForExemptUsers);
            }
        }

        public override string Name
        {
            get
            {
                return "Disclaimer Acceptance Agent";
            }
        }
    }
}