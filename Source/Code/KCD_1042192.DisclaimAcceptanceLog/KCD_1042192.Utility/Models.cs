using Relativity.Services.Objects.DataContracts;
using System;
using System.Collections.Generic;
using DTOs = kCura.Relativity.Client.DTOs;

namespace KCD_1042192.Utility
{
    public class Models
    {
        public class Response
        {
            public String Message;
            public bool Success;
        }

        public class EligibleDisclaimers : Response
        {
            public IEnumerable<Disclaimer> Disclaimers;
        }

        public class Identifier
        {
            public Int32? Id;
        }

        public class Group
        {
            public Int32 GroupId;
            public String GroupName;
        }

        public class Disclaimer
        {
            private String _disclaimerTitle;
            private String _disclaimerText;

            public Int32 DisclaimerId;

            public String DisclaimerTitle
            {
                get { return System.Net.WebUtility.HtmlDecode(_disclaimerTitle); }
                set { _disclaimerTitle = value; }
            }

            public String DisclaimerText
            {
                get { return System.Net.WebUtility.HtmlDecode(_disclaimerText); }
                set { _disclaimerText = value; }
            }

            public Int32 Order;
            public Boolean Enabled;
            public Boolean AllUsers;
            public Int32 ReacceptancePeriod;
            public IEnumerable<Int32> ApplicableGroups;
            public Relativity.Services.Objects.DataContracts.Choice Status;
        }

        public class User
        {
            public Int32 UserId;
            public String EmailAddress;
            public bool ShowDisclaimer;
        }

        public class Error
        {
            public String ErrorMessage;
        }
    }
}