using System;
using System.Collections.Generic;
using System.Text;

namespace AtCoderTestCaseHelper
{
    public class TestCase
    {
        public string Input { get; }
        public string Output { get; }

        public TestCase(string input, string output)
        {
            Input = input.Trim();
            Output = output.Trim();
        }

        public override string ToString() => $"[Input]{Environment.NewLine}{Input}{Environment.NewLine}[Output]{Environment.NewLine}{Output}";
    }
}
