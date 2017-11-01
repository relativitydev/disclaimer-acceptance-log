using System;

namespace KCD_1042192.Utility
{
    public class Constants
    {
        public class Guids
        {
            public class Applications
            {
                public static Guid DisclaimerAcceptanceLog = new Guid("87E45C02-2D97-41A6-834C-E6AAA905EA85");
            }

            public class Objects
            {
                public static Guid Disclaimer = new Guid("3F24FC94-F118-44FA-9A40-078D3B92FDB4");
                public static Guid DislcaimerGroup = new Guid("F62ED5FF-9F6D-4428-83E0-3FEF19750753");
                public static Guid DisclaimerSolutionConfiguration = new Guid("A0365CEC-2657-4683-8A88-AFAB12724405");
            }

            public class Fields
            {
                public static Guid DisclaimerTitle = new Guid("5A40761E-0FBB-421E-A433-465F9A03642E");
                public static Guid DisclaimerText = new Guid("4B49715B-ECE6-4F41-948D-27123E85BE22");
                public static Guid DisclaimerOrder = new Guid("EBDC5E76-79C2-4EBA-BBBF-1AA5CC47E19A");
                public static Guid DisclaimerEnabled = new Guid("7E61ADE6-7208-4403-8A88-A6DC1DFAFF68");
                public static Guid DisclaimerAllUsers = new Guid("1A535483-CBE5-4ED4-BD63-16DE7BA605D4");
                public static Guid DisclaimerApplicableGroups = new Guid("EB6DE92F-9EF0-4032-AA33-83D2EF5C25EF");
                public static Guid DisclaimerSystemCreatedOn = new Guid("BB9BE208-06AD-49DB-9BCE-1673F4D92B2C");
                public static Guid DisclaimerReacceptancePeriod = new Guid("BC089479-233B-4FA2-BEA9-E00141A772F0");
                public static Guid DisclaimerStatus = new Guid("15DED447-A361-42D9-88D0-B2F8F67E405F");

                public static Guid DisclaimerGroupsGroupArtifactId = new Guid("F8BBDAE4-4C98-4150-8495-8EE9AF9BFFCD");
                public static Guid DisclaimerGroupsGroupName = new Guid("6A689753-1362-4FCB-938D-CCC4F0F5B20A");

                public static Guid ConfigurationEnabled = new Guid("DB58FDCD-7445-40F0-B3C4-510EF4180652");
                public static Guid ConfigurationAllowAccessOnError = new Guid("9112A06E-7205-4E42-AD70-287C83C0D893");
            }

            public class Choices
            {
                public static Guid DisclaimerActive = new Guid("436F3EFC-820B-4AC4-87D4-17E33D54771A");
                public static Guid DisclaimerQueuedForActivation = new Guid("D7369FF2-6252-4040-9BA2-E3544C7083C0");
                public static Guid DisclaimerQueuedForDeactivation = new Guid("3AE6435A-5ECC-453E-9E23-A97A157C52C4");
            }
        }

        public class OtherConstants
        {
            public class RelativityVersion
            {
                public const string September94Release = "9.4.315.5";
            }
        }
    }
}