using System;
using System.Collections.Generic;
using System.Text;

namespace AtCoderTestCaseHelper
{
    public class NetworkConnectionException : Exception
    {
        public NetworkConnectionException(string? message) : base(message)
        {
        }
    }
}
