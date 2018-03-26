using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SourceCodePreparer
{
    [AttributeUsage(AttributeTargets.Property)]
    class SubSnippetAttribute : Attribute
    {
        public string TagName { get; private set; }

        public SubSnippetAttribute(string tagName)
        {
            this.TagName = tagName;
        }
    }
}
