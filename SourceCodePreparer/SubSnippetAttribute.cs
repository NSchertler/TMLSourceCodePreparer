using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TML
{
    /// <summary>
    /// Represents a subtag within the snippet tag.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    class SubSnippetAttribute : Attribute
    {
        /// <summary>
        /// The subtag's name (e.g. solution)
        /// </summary>
        public string TagName { get; private set; }

        public SubSnippetAttribute(string tagName)
        {
            this.TagName = tagName;
        }
    }
}
