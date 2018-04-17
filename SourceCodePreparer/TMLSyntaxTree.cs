using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TML
{
    /// <summary>
    /// Represents a node in the abstract TML syntax tree.
    /// </summary>
    abstract class TMLSyntaxNode
    {
        /// <summary>
        /// Prints an abstract representation of the subtree of this node to the console.
        /// </summary>
        /// <param name="indentation">The number of indentations to use for this subtree</param>
        public abstract void PrintSubtree(int indentation);
    }

    /// <summary>
    /// Represents a terminal node that only contains text.
    /// </summary>
    class TMLTerminalNode : TMLSyntaxNode
    {
        /// <summary>
        /// The character index in the original text where this node begins.
        /// </summary>
        public int BeginIndex { get; set; }

        /// <summary>
        /// The length of the text of this node.
        /// </summary>
        public int Length { get; set; }

        public override void PrintSubtree(int indentation)
        {
            Console.WriteLine(new String(' ', 2 * indentation) + "Content of length " + Length);
        }
    }

    /// <summary>
    /// Represents an internal tag node that may contain other nodes.
    /// </summary>
    class TMLTagNode : TMLSyntaxNode
    {
        /// <summary>
        /// The tag name of this node
        /// </summary>
        public string TagName { get; set; }

        /// <summary>
        /// A list of child nodes
        /// </summary>
        public List<TMLSyntaxNode> InnerNodes { get; private set; } = new List<TMLSyntaxNode>();

        /// <summary>
        /// The character index in the original text where the opening tag begins.
        /// </summary>
        public int OpeningTagBeginIndex { get; set; }

        /// <summary>
        /// The length of the opening tag including any attributs up to ">".
        /// </summary>
        public int OpeningTagLength { get; set; }

        /// <summary>
        /// The character index in the original text where the closing tag begins.
        /// </summary>
        public int ClosingTagBeginIndex { get; set; }

        /// <summary>
        /// The length of the closing tag up to ">".
        /// </summary>
        public int ClosingTagLength { get; set; }

        /// <summary>
        /// A list of attributes contained in this tag. The dictionary maps the attribute names to their respective values.
        /// </summary>
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

    /// <summary>
    /// Represents an abstract syntax tree of a TML document
    /// </summary>
    class TMLSyntaxTree
    {
        /// <summary>
        /// List of all nodes; there is no single root node
        /// </summary>
        public List<TMLSyntaxNode> Nodes { get; private set; } = new List<TMLSyntaxNode>();

        /// <summary>
        /// Constructs the abstract syntax tree from a given text.
        /// </summary>
        /// <param name="text">The original text to analyze</param>
        /// <exception cref="FormatException">Thrown if the number of opening and closing tags to not match or if an unexpected closing tag is encountered.</exception>
        public TMLSyntaxTree(string text)
        {
            //Setup the regex patterns to match opening and closing tags
            var tagStartRegex = new Regex(@"//<(?<tag>\w+)(?<attributes>[^>]*?)(?<!/)>");            
            var tagEndRegex = new Regex(@"//</(?<tag>\w+)>");            
            var attributesRegex = new Regex("\\b(?<key>\\w+)\\b=\"(?<value>.*?)\"");

            //Find the opening and closing tags
            var tagStartMatches = tagStartRegex.Matches(text);
            var tagEndMatches = tagEndRegex.Matches(text);

            if (tagStartMatches.Count != tagEndMatches.Count)
                throw new FormatException($"There are {tagStartMatches.Count} opening tags but {tagEndMatches.Count} closing tags.");

            //the character index where the last text portion (i.e. non-tag text) started
            var lastTextPosition = 0;
            //the index of the next tag start within tagStartMatches
            int nextTagStart = 0;
            //the index of the next tag end within tagEndMatches
            int nextTagEnd = 0;

            //stack of currently open tags
            var tagStack = new Stack<TMLTagNode>();

            //go through the matched opening and closing tags in order
            while (nextTagStart < tagStartMatches.Count || nextTagEnd < tagEndMatches.Count)
            {
                //character index of the next opening tag or text.Length if there are no more opening tags
                int nextStartPosition = (nextTagStart < tagStartMatches.Count ? tagStartMatches[nextTagStart].Index : text.Length);

                //character index of the next closing tag or text.length if there are no more closing tags
                int nextEndPosition = (nextTagEnd < tagEndMatches.Count ? tagEndMatches[nextTagEnd].Index : text.Length);

                //character index of the next opening or closing tag
                int nextPosition = Math.Min(nextStartPosition, nextEndPosition);

                //list of children of the tag we are currently in (either the global list or the child list of a node)
                var childrenOfParentNode = (tagStack.Count == 0 ? Nodes : tagStack.Peek().InnerNodes);

                //Add text nodes if the next tag is not at the current position
                if (nextPosition > lastTextPosition)
                    childrenOfParentNode.Add(new TMLTerminalNode() { BeginIndex = lastTextPosition, Length = nextPosition - lastTextPosition });

                if (nextStartPosition < nextEndPosition)
                {
                    //The next tag is an opening one
                    var match = tagStartMatches[nextTagStart++];
                    var tag = new TMLTagNode() { TagName = match.Groups["tag"].Value, OpeningTagBeginIndex = match.Index, OpeningTagLength = match.Length };

                    //Gather the attributes
                    var attributesString = match.Groups["attributes"].Value;
                    var attributeMatches = attributesRegex.Matches(attributesString);
                    foreach (Match a in attributeMatches)
                    {
                        tag.Attributes[a.Groups["key"].Value] = a.Groups["value"].Value;
                    }

                    //Add the new tag to the parent
                    childrenOfParentNode.Add(tag);
                    //move into this tag
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

                    //leave the tag
                    var tag = tagStack.Pop();
                    tag.ClosingTagBeginIndex = match.Index;
                    tag.ClosingTagLength = match.Length;
                    lastTextPosition = match.Index + match.Length;
                }
            }
            //Add a text node for everything after the last tag
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
