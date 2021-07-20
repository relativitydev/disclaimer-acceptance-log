using kCura.Relativity.Client;
using kCura.Relativity.Client.DTOs;
using Relativity.API;
using Relativity.Services.Objects;
using Relativity.Services.Objects.DataContracts;
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
            using(IObjectManager objectManager = helper.GetServicesManager().CreateProxy<IObjectManager>(ExecutionIdentity.System))
			      {
              var queryRequest = new QueryRequest
              {
                ObjectType = new ObjectTypeRef
								{
                  ArtifactTypeID = 3
								},
                Fields = new List<FieldRef>
								{
                  new FieldRef
									{
                    Name = "Name"
									}
								},
              };

              var queryResult = objectManager.QueryAsync(-1, queryRequest, 0, 1000).Result;
              foreach(var obj in queryResult.Objects)
				      {
                retVal.Add(obj.ArtifactID, new Models.Group
								{
                  GroupId = obj.ArtifactID,
                  GroupName = obj.FieldValues[0].Value.ToString()
								});
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
            using(IObjectManager objectManager = helper.GetServicesManager().CreateProxy<IObjectManager>(ExecutionIdentity.System))
			      {
              var queryRequest = new QueryRequest
              {
                ObjectType = new ObjectTypeRef
								{
                  Guid = workspaceGroupObject
								},
                Fields = new List<FieldRef>
								{
                  new FieldRef
									{
                    Guid = workspaceGroupObjectEddsIdField
									},
                  new FieldRef
									{
                    Guid = workspaceGroupObjectNameField
									}
								},
              };

              var queryResult = objectManager.QueryAsync(workspaceArtifactId, queryRequest, 0, 1000).Result;
              foreach(var obj in queryResult.Objects)
				      {
                retVal.Add(obj.ArtifactID, new Models.Group
								{
                  GroupId = (int)obj.FieldValues[0].Value,
                  GroupName = obj.FieldValues[1].Value.ToString()
								});
				      }
			      }
            return retVal;
        }

        //Update Disclaimer Group DTO's Group name from Relativity EDDS collection
        private static void UpdateGroupNamesInWorkspace(IHelper helper, Int32 workspaceArtifactId, Dictionary<Int32, Models.Group> allWorkspaceGroups, Dictionary<Int32, Models.Group> allRelativityGroups, Guid workspaceGroupObject, Guid workspaceGroupObjectNameField)
        {
            var massUpdateRequest = new MassUpdatePerObjectsRequest();
            List<ObjectRefValuesPair> objectRefValuesPairs = new List<ObjectRefValuesPair>();
            List<FieldRef> fields = new List<FieldRef>
						{
              new FieldRef
							{
                Guid = workspaceGroupObjectNameField
							}
						};

            foreach (var workspaceGroup in allWorkspaceGroups)
            {
                if (allRelativityGroups.ContainsKey(workspaceGroup.Value.GroupId))
                {
                    var workspaceGroupName = workspaceGroup.Value.GroupName;
                    var relativityGroupName = allRelativityGroups[workspaceGroup.Value.GroupId].GroupName;
                    //if Someone changed the Edds Level GroupName, update it's workspace counterpart
                    if (!workspaceGroupName.Equals(relativityGroupName))
                    {
                        var objectRefValuePair = new ObjectRefValuesPair
												{
                          Object = new RelativityObjectRef
													{
                            ArtifactID = workspaceGroup.Key
													},
                          Values = new List<object> { relativityGroupName }
												};
                        objectRefValuesPairs.Add(objectRefValuePair);
                    }
                }
            }

            if (objectRefValuesPairs.Any())
            {
                massUpdateRequest.ObjectValues = objectRefValuesPairs;
                massUpdateRequest.Fields = fields;
                using(IObjectManager objectManager = helper.GetServicesManager().CreateProxy<IObjectManager>(ExecutionIdentity.System))
				        {
                  var updateResult = objectManager.UpdateAsync(workspaceArtifactId, massUpdateRequest).Result;
                  //Not worried about the results because it's possible that the user removed a group from the workspace, in that case the update would fail for that specific RDO but the others would update successfully
				        }
            }
        }

        private static void AddNewGroupsToWorkspace(IHelper helper, Int32 workspaceArtifactId, Dictionary<Int32, Models.Group> relativityGroupDictionary, IEnumerable<Int32> newGroupArtifactIds, Guid workspaceGroupObject, Guid workspaceGroupObjectNameField, Guid workspaceGroupObjectEddsIdField)
        {
            //build a list of New workspace level groups to create
            var massCreateRequest = new MassCreateRequest();
            massCreateRequest.ObjectType = new ObjectTypeRef { Guid = workspaceGroupObject };
            massCreateRequest.Fields = new List<FieldRef>
						{
              new FieldRef
							{
                Guid = workspaceGroupObjectNameField
              },
              new FieldRef
							{
                Guid = workspaceGroupObjectEddsIdField
              }
						};
            List<List<object>> values = new List<List<object>>();
            
            foreach (var newGroupId in newGroupArtifactIds)
            {
                var newGroupToAdd = relativityGroupDictionary[newGroupId];
                var newGroupValues = new List<object>
								{
                  newGroupToAdd.GroupName,
                  newGroupId
								};
                values.Add(newGroupValues);
            }
            massCreateRequest.ValueLists = values;

            //Send the Create call to Object Manager
            using(IObjectManager objectManager = helper.GetServicesManager().CreateProxy<IObjectManager>(ExecutionIdentity.System))
			      {
              var createResult = objectManager.CreateAsync(workspaceArtifactId, massCreateRequest).Result;
				      if (!createResult.Success)
				      {
                throw new Exception("Unable to create new groups in workspace " + workspaceArtifactId + ": " + createResult.Message);
              }
			      }
        }

        private static void DeleteNonExistantGroupsFromWorkspace(IHelper helper, Int32 workspaceArtifactId, Dictionary<Int32, Models.Group> nonExistantGroups, Guid workspaceGroupObject)
        {
            //Build a list of Groups rdos to Delete
            var massDeleteRequest = new MassDeleteByObjectIdentifiersRequest();
            List<RelativityObjectRef> objectsToDelete = new List<RelativityObjectRef>();
            foreach (var group in nonExistantGroups)
            {
                var obj = new RelativityObjectRef
								{
                  ArtifactID = group.Key
								};
                objectsToDelete.Add(obj);
            }
            massDeleteRequest.Objects = objectsToDelete;

            //Send the call
            using (IObjectManager objectManager = helper.GetServicesManager().CreateProxy<IObjectManager>(ExecutionIdentity.System))
			      {
              var deleteResult = objectManager.DeleteAsync(workspaceArtifactId, massDeleteRequest).Result;
              //Not worried if some deletions fail because it's possible that they are linked to other objects as dependencies or already deleted
			      }
        }
    }
}