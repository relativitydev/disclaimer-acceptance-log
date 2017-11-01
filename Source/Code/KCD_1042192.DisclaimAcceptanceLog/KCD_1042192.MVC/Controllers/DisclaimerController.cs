using KCD_1042192.Utility;
using Relativity.CustomPages;
using System;
using System.Reflection;
using System.Web.Mvc;

namespace KCD_1042192.MVC.Controllers
{
    public class DisclaimerController : Controller
    {
        [HttpGet]
        public ContentResult GetEligibleDisclaimers()
        {
            var result = new Models.EligibleDisclaimers { Success = false };
            try
            {
                result.Disclaimers = DisclaimerDatabase.GetEligibleDisclaimers(ConnectionHelper.Helper(), GetUserID());
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
                Log(ex);
            }
            return Content(Newtonsoft.Json.JsonConvert.SerializeObject(result), "application/json");
        }

        [HttpPost]
        public ContentResult AcceptDisclaimer(FormCollection disclaimer)
        {
            var result = new Models.Response { Success = false };

            try
            {
                var disclaimerId = Int32.Parse(disclaimer["Id"]);

                result.Success = DisclaimerDatabase.AcceptDisclaimer(ConnectionHelper.Helper(), GetUserID(), disclaimerId);
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
                Log(ex);
            }

            return Content(Newtonsoft.Json.JsonConvert.SerializeObject(result), "application/json");
        }

        [HttpPost]
        public ContentResult LogError(FormCollection error)
        {
            var result = new Models.Response { Success = false };

            try
            {
                var errorMsg = error["ErrorMessage"];
                result.Success = DisclaimerDatabase.LogError(ConnectionHelper.Helper(), GetUserID(), errorMsg);
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
                Log(ex);
            }

            return Content(Newtonsoft.Json.JsonConvert.SerializeObject(result), "application/json");
        }

        private Int32 GetUserID()
        {
            Int32 retVal;
            Version userSessionInfoMaxRelativityVersion = new Version(9, 3, 0, 0);
            Version currentRelativityVersion = Assembly.GetAssembly(typeof(Relativity.CustomPages.ConnectionHelper)).GetName().Version;

            if (currentRelativityVersion >= userSessionInfoMaxRelativityVersion)
            {
                retVal = GetAuthenticationManagerUserID();
            }
            else
            {
                try
                {
                    retVal = Int32.Parse(Session["UserID"].ToString());
                }
                catch (Exception ex)
                {
                    throw new Exceptions.NullUserHttpSessionExcepetion(ex.ToString(), ex);
                }
            }

            return retVal;
        }

        private Int32 GetAuthenticationManagerUserID()
        {
            return Relativity.CustomPages.ConnectionHelper.Helper().GetAuthenticationManager().UserInfo.ArtifactID;
        }

        private void Log(Exception ex)
        {
            Int32 userId;
            try
            {
                userId = GetUserID();
            }
            catch (Exceptions.NullUserHttpSessionExcepetion)
            {
                // We are unable to record the userID if it can't be retrieved
                userId = 0;
            }
            catch (Exception)
            {
                throw;
            }
            DisclaimerDatabase.LogError(ConnectionHelper.Helper(), userId, ex.ToString());
        }
    }
}