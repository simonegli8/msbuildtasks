
using System;
using NUnit.Framework;
using System.Xml;


namespace MSBuild.Community.Tasks.Tests.Xml
{
    public class XPathAsserter
    {
        string message;
        string actualValue;
        string expectedValue;

        public XPathAsserter(XmlDocument document, string xpath, string expectedValue, string message, params object[] args)
        {
            XmlNode node = document.SelectSingleNode(xpath);
            if (node == null)
            {
                actualValue = null;
            }
            else
            {
                actualValue = node.Value;
            }
            this.expectedValue = expectedValue;
            this.message = message;
        }

        public string Message
        {
            get
            {
                return $"{this.Expectation} expected, but {actualValue} found.";
            }
        }

        protected virtual string Expectation
        {
            get
            {
                return string.Format("<\"{0}\">", this.expectedValue);
            }
        }

        public bool Test()
        {
            return (expectedValue == actualValue);           
        }

    }

}
