using kCura.EventHandler;
using kCura.EventHandler.CustomAttributes;
using kCura.Relativity.Client;
using kCura.Relativity.Client.DTOs;
using Relativity.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace KCD_1042192.EventHandlers
{
    [Description("Pre Save EventHandler")]
    [Guid("485e34e3-b04c-4279-812c-1ba9db2840cc")]
    public class DisclaimerPreSaveEv : PreSaveEventHandler, IDataEnabled
    {
        public override Response Execute()
        {
            var retVal = new Response { Success = true, Message = String.Empty };

            var disclaimerEnabled = ((bool?)ActiveArtifact.Fields[Utility.Constants.Guids.Fields.DisclaimerEnabled.ToString()].Value.Value).GetValueOrDefault(false);
            var order = (Int32?)ActiveArtifact.Fields[Utility.Constants.Guids.Fields.DisclaimerOrder.ToString()].Value.Value;
            var choiceQueuedForActivation = new kCura.EventHandler.Choice(GetArtifactIdByGuid(Utility.Constants.Guids.Choices.DisclaimerQueuedForActivation), "Queued For Activation");
            var choiceQueuedForDeactivation = new kCura.EventHandler.Choice(GetArtifactIdByGuid(Utility.Constants.Guids.Choices.DisclaimerQueuedForDeactivation), "Queued For Deactivation");

            //Set the status field to 'Queued for Activation/Deactivation if the enabled field is set or changes'
            try
            {
                OrderIsValid(order);
                //This is a new disclaimer and the user set it to enabled
                if (ActiveArtifact.IsNew && disclaimerEnabled)
                {
                    ActiveArtifact.Fields[Utility.Constants.Guids.Fields.DisclaimerStatus.ToString()].Value.Value = new ChoiceCollection { choiceQueuedForActivation };
                }
                //This is a disclaimer that already exists and the user changed it's enabled status
                else if (!ActiveArtifact.IsNew && YesNoFieldChanged(Utility.Constants.Guids.Fields.DisclaimerEnabled))
                {
                    if (disclaimerEnabled)
                    {
                        ActiveArtifact.Fields[Utility.Constants.Guids.Fields.DisclaimerStatus.ToString()].Value.Value = new ChoiceCollection { choiceQueuedForActivation };
                    }
                    else
                    {
                        ActiveArtifact.Fields[Utility.Constants.Guids.Fields.DisclaimerStatus.ToString()].Value.Value = new ChoiceCollection { choiceQueuedForDeactivation };
                    }
                }
            }
            catch (Exception ex)
            {
                retVal.Success = false;
                retVal.Message = ex.Message;
            }

            return retVal;
        }

        private void OrderIsValid(Int32? order)
        {
            if (order.GetValueOrDefault(0) < 0)
            {
                throw new Exception("The order must be zero or greater");
            }
        }

        private Boolean YesNoFieldChanged(Guid fieldGuid)
        {
            bool oldValue = (GetOldFieldValueFromRsapi(fieldGuid, Helper.GetActiveCaseID(), ActiveArtifact.ArtifactID).ValueAsYesNo).GetValueOrDefault(false);
            bool newValue = ((bool?)ActiveArtifact.Fields[fieldGuid.ToString()].Value.Value).GetValueOrDefault(false);

            return (!oldValue.Equals(newValue));
        }

        private kCura.Relativity.Client.DTOs.FieldValue GetOldFieldValueFromRsapi(Guid fieldGuid, Int32 workspaceId, Int32 artId)
        {
            kCura.Relativity.Client.DTOs.FieldValue returnObj = null;

            var rdo = new RDO(artId)
            {
                ArtifactTypeGuids = new List<Guid> { Utility.Constants.Guids.Objects.Disclaimer },
                Fields = new List<kCura.Relativity.Client.DTOs.FieldValue> { new kCura.Relativity.Client.DTOs.FieldValue(fieldGuid) }
            };

            using (var proxy = Helper.GetServicesManager().CreateProxy<IRSAPIClient>(ExecutionIdentity.System))
            {
                proxy.APIOptions = new APIOptions { WorkspaceID = workspaceId };
                var result = proxy.Repositories.RDO.Read(rdo);
                if (result.Success && result.Results.Any())
                {
                    returnObj = result.Results[0].Artifact.Fields.First(x => x.Guids.Contains(fieldGuid));
                }
            }
            return returnObj;
        }

        public override FieldCollection RequiredFields
        {
            get
            {
                var retVal = new FieldCollection{
                    new kCura.EventHandler.Field(Utility.Constants.Guids.Fields.DisclaimerEnabled),
                    new kCura.EventHandler.Field(Utility.Constants.Guids.Fields.DisclaimerStatus),
                    new kCura.EventHandler.Field(Utility.Constants.Guids.Fields.DisclaimerAllUsers),
                    new kCura.EventHandler.Field(Utility.Constants.Guids.Fields.DisclaimerApplicableGroups),
                    new kCura.EventHandler.Field(Utility.Constants.Guids.Fields.DisclaimerReacceptancePeriod),
                    new kCura.EventHandler.Field(Utility.Constants.Guids.Fields.DisclaimerOrder)
                };
                return retVal;
            }
        }
    }
}