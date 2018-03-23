using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SourceCodePreparer
{
    abstract class TMLSyntaxNode
    {
        public abstract void PrintSubtree(int indentation);
    }

    class TMLTerminalNode : TMLSyntaxNode
    {
        public int BeginIndex { get; set; }
        public int Length { get; set; }

        public override void PrintSubtree(int indentation)
        {
            Console.WriteLine(new String(' ', 2 * indentation) + "Content of length " + Length);
        }
    }

    class TMLTagNode : TMLSyntaxNode
    {
        public string TagName { get; set; }

        public List<TMLSyntaxNode> InnerNodes { get; private set; } = new List<TMLSyntaxNode>();

        public int OpeningTagBeginIndex { get; set; }
        public int OpeningTagLength { get; set; }

        public int ClosingTagBeginIndex { get; set; }
        public int ClosingTagLength { get; set; }

        public Dictionary<string, string> Attributes { get; private set; } = new Dictionary<string, string>();

        public override void PrintSubtree(int indentation)
        {
            Console.WriteLine(new String(' ', 2 * indentation) + TagName);
            foreach (var t in InnerNodes)
            {
                t.PrintSubtree(indentation + 1);
            }
        }
    }

    class TMLSyntaxTree
    {
        public List<TMLSyntaxNode> Nodes { get; private set; } = new List<TMLSyntaxNode>();

        public TMLSyntaxTree(string text)
        {
            //Setup the regex patterns to match opening and closing tags
            var tagStartRegex = new Regex(@"//<(?<tag>\w+)(?<attributes>[^>]*?)(?<!/)>");
            var tagStartMatches = tagStartRegex.Matches(text);
            var tagEndRegex = new Regex(@"//</(?<tag>\w+)>");
            var tagEndMatches = tagEndRegex.Matches(text);
            var attributesRegex = new Regex("\\b(?<key>\\w+)\\b=\"(?<value>.*?)\"");

            if (tagStartMatches.Count != tagEndMatches.Count)
                throw new FormatException($"There are {tagStartMatches.Count} opening tags but {tagEndMatches.Count} closing tags.");

            var lastTextPosition = 0;
            int nextTagStart = 0;
            int nextTagEnd = 0;

            var tagStack = new Stack<TMLTagNode>();

            //go through the matched opening and closing tags in order
            while (nextTagStart < tagStartMatches.Count || nextTagEnd < tagEndMatches.Count)
            {
                int nextStartPosition = (nextTagStart < tagStartMatches.Count ? tagStartMatches[nextTagStart].Index : text.Length);
                int nextEndPosition = (nextTagEnd < tagEndMatches.Count ? tagEndMatches[nextTagEnd].Index : text.Length);
                int nextPosition = Math.Min(nextStartPosition, nextEndPosition);

                var childrenOfParentNode = (tagStack.Count == 0 ? Nodes : tagStack.Peek().InnerNodes);

                if (nextPosition > lastTextPosition)
                    childrenOfParentNode.Add(new TMLTerminalNode() { BeginIndex = lastTextPosition, Length = nextPosition - lastTextPosition });

                if (nextStartPosition < nextEndPosition)
                {
                    //The next tag is an opening one
                    var match = tagStartMatches[nextTagStart++];
                    var tag = new TMLTagNode() { TagName = match.Groups["tag"].Value, OpeningTagBeginIndex = match.Index, OpeningTagLength = match.Length };
                    var attributesString = match.Groups["attributes"].Value;
                    var attributeMatches = attributesRegex.Matches(attributesString);
                    foreach (Match a in attributeMatches)
                    {
                        tag.Attributes[a.Groups["key"].Value] = a.Groups["value"].Value;
                    }

                    childrenOfParentNode.Add(tag);
                    tagStack.Push(tag);
                    lastTextPosition = match.Index + match.Length;
                }
                else
                {
                    //The next tag is a closing one
                    var match = tagEndMatches[nextTagEnd++];
                    var tagname = match.Groups["tag"].Value;
                    if (tagStack.Count == 0)
                        throw new FormatException($"Encountered a closing tag of {tagname} but expected no closing tag.");

                    if (tagStack.Peek().TagName != tagname)
                        throw new FormatException($"Encountered a closing tag of {tagname} but expected a closing tag of {tagStack.Peek().TagName}.");

                    var tag = tagStack.Pop();
                    tag.ClosingTagBeginIndex = match.Index;
                    tag.ClosingTagLength = match.Length;
                    lastTextPosition = match.Index + match.Length;
                }
            }
            if (text.Length > lastTextPosition)
                Nodes.Add(new TMLTerminalNode() { BeginIndex = lastTextPosition, Length = text.Length - lastTextPosition });
        }

        public void PrintSyntaxTree()
        {
            foreach (var node in Nodes)
            {
                node.PrintSubtree(0);
            }
        }
    }
}
