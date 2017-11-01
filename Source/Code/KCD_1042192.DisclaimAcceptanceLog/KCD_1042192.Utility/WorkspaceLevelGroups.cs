using kCura.Relativity.Client;
using kCura.Relativity.Client.DTOs;
using Relativity.API;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KCD_1042192.Utility
{
    public static class WorkspaceLevelGroups
    {
        /// <summary>
        /// This class maintains a represenation of Admin level groups in a workspace by
        /// performing crud operations to keep the workspace groups objects consistent with admin groups
        /// </summary>
        /// <param name="helper">kCura Helper</param>
        /// <param name="workspaceArtifactId">The Workspace Artifact Id of where the Groups RDOs should be maintained</param>
        /// <param name="workspaceGroupObject">The Guid of the Group RDO Object</param>
        /// <param name="workspaceGroupNameField">The Guid of the Group RDO fixed length text name field</param>
        /// <param name="workspaceGroupEddsArtifactIdField">The Guid of the Group RDO whole number field that holds it's corresponding EDDS level group Artifact ID</param>
        public static void MaintainWorkspaceLevelGroups(IHelper helper, Int32 workspaceArtifactId, Guid workspaceGroupObject, Guid workspaceGroupNameField, Guid workspaceGroupEddsArtifactIdField)
        {
            if (Functions.ApplicationIsLocked(helper, workspaceArtifactId, workspaceGroupObject))
            {
                //Get a list of all the groups in Relativity
                var relativityGroupDictionary = GetGroupsInRelativity(helper);
                var relativityGroupArtifactIds = relativityGroupDictionary.AsEnumerable().Select(x => x.Key).ToList();

                //Get a list of all the Workspace Group objects
                var workspaceGroupDictionary = GetGroupsAddedToWorkspace(helper, workspaceArtifactId, workspaceGroupObject, workspaceGroupNameField, workspaceGroupEddsArtifactIdField);
                var workspaceGroupArtifactIds = workspaceGroupDictionary.AsEnumerable().Select(x => x.Value.GroupId).ToList();

                //Update Existing Groups
                UpdateGroupNamesInWorkspace(helper, workspaceArtifactId, workspaceGroupDictionary, relativityGroupDictionary, workspaceGroupObject, workspaceGroupNameField);

                //Add new Groups
                var newGroups = relativityGroupArtifactIds.Except(workspaceGroupArtifactIds).ToList();
                if (newGroups.Any())
                {
                    AddNewGroupsToWorkspace(helper, workspaceArtifactId, relativityGroupDictionary, newGroups, workspaceGroupObject, workspaceGroupNameField, workspaceGroupEddsArtifactIdField);
                }

                //Delete groups that exist in the workspace but not at the admin level
                var nonExistantGroups = workspaceGroupDictionary.AsEnumerable().Where(x => !relativityGroupDictionary.ContainsKey(x.Value.GroupId)).ToDictionary(y => y.Key, y => y.Value);
                if (nonExistantGroups.Any())
                {
                    DeleteNonExistantGroupsFromWorkspace(helper, workspaceArtifactId, nonExistantGroups, workspaceGroupObject);
                }
            }
        }

        /// <summary>
        /// Get all the Relativity Groups from the EDDS.
        /// -The Dictionary Key is the artifactId of the group (This is different than the Disclaimer Group DTO's dictionary)
        /// -The Dictionary Value Groupid is the EDDS level artifactId of the corresponding group
        /// -The Dictionary Value GroupName is the EDDS level group name of the corresponding group
        /// </summary>
        private static Dictionary<Int32, Models.Group> GetGroupsInRelativity(IHelper helper)
        {
            var retVal = new Dictionary<Int32, Models.Group>();
            using (var proxy = helper.GetServicesManager().CreateProxy<IRSAPIClient>(ExecutionIdentity.System))
            {
                proxy.APIOptions = new APIOptions { WorkspaceID = -1 };
                var queryRelativityGroups = new Query<Group>
                {
                    Fields = new List<FieldValue> { new FieldValue(GroupFieldNames.Name) }
                };
                var relativityGroupResults = proxy.Repositories.Group.Query(queryRelativityGroups);

                if (relativityGroupResults.Success)
                {
                    foreach (var relativityGroup in relativityGroupResults.Results)
                    {
                        retVal.Add(relativityGroup.Artifact.ArtifactID, new Models.Group
                        {
                            GroupId = relativityGroup.Artifact.ArtifactID,
                            GroupName = relativityGroup.Artifact.Name
                        });
                    }
                }
                else
                {
                    throw new Exception("Unable to Query EDDS for Groups: " + relativityGroupResults.Message);
                }
            }
            return retVal;
        }

        /// <summary>
        /// Get all the Group Object RDOs from the workspace.
        /// The Dictionary Key is the workspace artifactId of the group object
        /// The Dictionary Value Groupid is the EDDS level artifactId of the corresponding group
        /// The Dictionary Value GroupName is the EDDS level group name of the corresponding group
        /// </summary>
        private static Dictionary<Int32, Models.Group> GetGroupsAddedToWorkspace(IHelper helper, Int32 workspaceArtifactId, Guid workspaceGroupObject, Guid workspaceGroupObjectNameField, Guid workspaceGroupObjectEddsIdField)
        {
            var retVal = new Dictionary<Int32, Models.Group>();

            using (var proxy = helper.GetServicesManager().CreateProxy<IRSAPIClient>(ExecutionIdentity.System))
            {
                proxy.APIOptions = new APIOptions { WorkspaceID = workspaceArtifactId };
                var queryWorkspaceGroups = new Query<RDO>
                {
                    ArtifactTypeGuid = workspaceGroupObject,
                    Fields = new List<FieldValue>
                    {
                        new FieldValue(workspaceGroupObjectEddsIdField),
                        new FieldValue(workspaceGroupObjectNameField)
                    }
                };
                var workspaceGroupResults = proxy.Repositories.RDO.Query(queryWorkspaceGroups);

                if (workspaceGroupResults.Success)
                {
                    foreach (var workspaceGroup in workspaceGroupResults.Results)
                    {
                        retVal.Add(workspaceGroup.Artifact.ArtifactID, new Models.Group
                        {
                            GroupId = workspaceGroup.Artifact[workspaceGroupObjectEddsIdField].ValueAsWholeNumber.GetValueOrDefault(0),
                            GroupName = workspaceGroup.Artifact[workspaceGroupObjectNameField].ValueAsFixedLengthText
                        });
                    }
                }
                else
                {
                    throw new Exception("Unable to query workspace for Groups RDO: " + workspaceGroupResults.Message);
                }
            }
            return retVal;
        }

        //Update Disclaimer Group DTO's Group name from Relativity EDDS collection
        private static void UpdateGroupNamesInWorkspace(IHelper helper, Int32 workspaceArtifactId, Dictionary<Int32, Models.Group> allWorkspaceGroups, Dictionary<Int32, Models.Group> allRelativityGroups, Guid workspaceGroupObject, Guid workspaceGroupObjectNameField)
        {
            var rdosToUpdate = new List<RDO>();
            foreach (var workspaceGroup in allWorkspaceGroups)
            {
                if (allRelativityGroups.ContainsKey(workspaceGroup.Value.GroupId))
                {
                    var workspaceGroupName = workspaceGroup.Value.GroupName;
                    var relativityGroupName = allRelativityGroups[workspaceGroup.Value.GroupId].GroupName;
                    //if Someone changed the Edds Level GroupName, update it's workspace counterpart
                    if (!workspaceGroupName.Equals(relativityGroupName))
                    {
                        var rdo = new RDO(workspaceGroup.Key)
                        {
                            ArtifactTypeGuids = new List<Guid> { workspaceGroupObject },
                            Fields = new List<FieldValue> { new FieldValue(workspaceGroupObjectNameField, relativityGroupName) }
                        };
                        rdosToUpdate.Add(rdo);
                    }
                }
            }

            if (rdosToUpdate.Any())
            {
                using (var proxy = helper.GetServicesManager().CreateProxy<IRSAPIClient>(ExecutionIdentity.System))
                {
                    proxy.APIOptions = new APIOptions { WorkspaceID = workspaceArtifactId };
                    proxy.Repositories.RDO.Update(rdosToUpdate);
                    //Not worried about the results because it's possible that the user removed a group from the workspace, in that case the update would fail for that specific RDO but the others would update successfully
                }
            }
        }

        private static void AddNewGroupsToWorkspace(IHelper helper, Int32 workspaceArtifactId, Dictionary<Int32, Models.Group> relativityGroupDictionary, IEnumerable<Int32> newGroupArtifactIds, Guid workspaceGroupObject, Guid workspaceGroupObjectNameField, Guid workspaceGroupObjectEddsIdField)
        {
            //build a list of New workspace level groups to create
            var rdosToAdd = new List<RDO>();
            foreach (var newGroupId in newGroupArtifactIds)
            {
                var newGroupToAdd = relativityGroupDictionary[newGroupId];
                var rdo = new RDO
                {
                    ArtifactTypeGuids = new List<Guid> { workspaceGroupObject },
                    Fields = new List<FieldValue>{
                        new FieldValue(workspaceGroupObjectNameField, newGroupToAdd.GroupName),
                        new FieldValue(workspaceGroupObjectEddsIdField, newGroupId)
                    }
                };
                rdosToAdd.Add(rdo);
            }

            //Send the Create call to the RSAPI
            using (var proxy = helper.GetServicesManager().CreateProxy<IRSAPIClient>(ExecutionIdentity.System))
            {
                proxy.APIOptions = new APIOptions { WorkspaceID = workspaceArtifactId };
                var results = proxy.Repositories.RDO.Create(rdosToAdd);

                if (!results.Success)
                {
                    throw new Exception("Unable to create new groups in workspace " + workspaceArtifactId + ": " + String.Join(" , ", results.Results.AsEnumerable().Where(x => x.Success == false).Select(x => x.Message).ToList()));
                }
            }
        }

        private static void DeleteNonExistantGroupsFromWorkspace(IHelper helper, Int32 workspaceArtifactId, Dictionary<Int32, Models.Group> nonExistantGroups, Guid workspaceGroupObject)
        {
            //Build a list of Groups rdos to Delete
            var rdosToDelete = new List<RDO>();
            foreach (var group in nonExistantGroups)
            {
                var rdo = new RDO(group.Key)
                {
                    ArtifactTypeGuids = new List<Guid> { workspaceGroupObject },
                };
                rdosToDelete.Add(rdo);
            }

            //Send the call
            using (var proxy = helper.GetServicesManager().CreateProxy<IRSAPIClient>(ExecutionIdentity.System))
            {
                proxy.APIOptions = new APIOptions { WorkspaceID = workspaceArtifactId };
                proxy.Repositories.RDO.Delete(rdosToDelete);
                //Not worried if some deletions fail because it's possible that they are linked to other objects as dependencies or already deleted
            }
        }
    }
}