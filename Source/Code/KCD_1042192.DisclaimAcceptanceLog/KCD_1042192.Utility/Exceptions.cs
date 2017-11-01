using System;

namespace KCD_1042192.Utility
{
    public class Exceptions
    {
        public class DisclaimerAppIsNotInstalledExcepetion : Exception
        {
            public DisclaimerAppIsNotInstalledExcepetion()
                : base("Please install the disclaimer Application into a workspace")
            {
            }
        }

        public class NullUserHttpSessionExcepetion : Exception
        {
            public NullUserHttpSessionExcepetion()
                : base("Unable to retrieve the User ID from the HTTP Session")
            {
            }

            public NullUserHttpSessionExcepetion(string message)
                : base(message)
            {
            }

            public NullUserHttpSessionExcepetion(string message, Exception inner)
                : base(message, inner)
            {
            }
        }
    }
}