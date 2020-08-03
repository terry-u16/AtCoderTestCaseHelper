using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace AtCoderTestCaseHelper
{
    [MessagePackObject]
    public class LoginCredential
    {
        [Key(0)]
        public string? UserName { get; set; }
        [Key(1)]
        public string? Password { get; set; }
    }
}
