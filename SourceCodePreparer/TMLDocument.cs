using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TML
{
    /// <summary>
    /// Represents the transformation type.
    /// </summary>
    public enum TMLOutputType
    {
        /// <summary>
        /// Out-of-place generation of the solution
        /// </summary>
        Solution,

        /// <summary>
        /// Out-of-place generation of the student version
        /// </summary>
        StudentVersion,

        /// <summary>
        /// In-place modification with the solutions made active
        /// </summary>
        TMLWithActiveSolution,

        /// <summary>
        /// In-place modification with the student versions made active
        /// </summary>
        TMLWithActiveStudentVersion
    }    

    /// <summary>
    /// Represents a document with TML snippets.
    /// </summary>
    public class TMLDocument
    {
        /// <summary>
        /// Static repository of possible subtags within a snippet
        /// </summary>
        static Dictionary<PropertyInfo, SubSnippetAttribute> subsnippets = new Dictionary<PropertyInfo, SubSnippetAttribute>();
        static HashSet<string> subsnippetTags = new HashSet<string>();
        static TMLDocument()
        {
            //Find all properties of the SnippetNode class that have the SubSnippetAttribute
            foreach (var prop in typeof(SnippetNode).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = prop.GetCustomAttribute<SubSnippetAttribute>();
                if (attr != null)
                {
                    subsnippets.Add(prop, attr);
                    subsnippetTags.Add(attr.TagName);
                }
            }
        }                        

        /// <summary>
        /// Stores the original text of the document
        /// </summary>
        private string text;

        /// <summary>
        /// Stores a parsed syntax tree for the document
        /// </summary>
        private TMLSyntaxTree syntaxTree;

        /// <summary>
        /// Stores a semantic description of the document in form of a tree. This representation
        /// is derived from <see cref="syntaxTree"/> by interpretation.
        /// </summary>
        private List<SemanticNode> nodes = new List<SemanticNode>();

        /// <summary>
        /// Instantiates a new TMLDocument with the given text.
        /// </summary>
        /// <param name="text"></param>
        public TMLDocument(string text)
        {
            this.text = text;

            syntaxTree = new TMLSyntaxTree(text);
            ParseTML();
        }

        /// <summary>
        /// Returns the number of snippets in this document
        /// </summary>
        public int GetSnippetCount()
        {
            return nodes.Where(n => n is SnippetNode).Count();
        }

        /// <summary>
        /// Determines the number of snippets in this document that belong to a given task number.
        /// </summary>
        /// <param name="task">The task number to check. All subtasks are also considered (i.e. 4.3 for task 4). If a snippet does not have a task number, it is considered to belong to all tasks.</param>
        /// <returns>The number of relevant snippets</returns>
        public int GetSnippetCountForTask(HierarchicalNumber? task)
        {
            return nodes.Where(n => n is SnippetNode).Select(n => (SnippetNode)n).Where(n => n.IsInTask(task)).Count();
        }

        /// <summary>
        /// Transforms this document and writes the result into <paramref name="output"/>.
        /// </summary>
        /// <param name="output">Output stream of the result</param>
        /// <param name="type">The type of the transformation to perform</param>
        /// <param name="solutionsUpToTask">Use solutions up to a given task number. Student versions are used otherwise.</param>
        /// <param name="useSpecialSolution">Determines if the processor uses the <c>specialsolution</c> subtags instead of the <c>solution</c> subtags if available.</param>
        public void TransformDocument(Stream output, TMLOutputType type, HierarchicalNumber? solutionsUpToTask, bool useSpecialSolution = false)
        {
            using (var str = new StreamWriter(output, Encoding.UTF8, 0x8000, true))
                TransformDocument(str, type, solutionsUpToTask, useSpecialSolution);
        }

        /// <summary>
        /// Transforms this document and writes the result into <paramref name="output"/>.
        /// </summary>
        /// <param name="output">Output stream of the result</param>
        /// <param name="type">The type of the transformation to perform</param>
        /// <param name="solutionsUpToTask">Use solutions up to a given task number. Student versions are used otherwise.</param>
        /// <param name="useSpecialSolution">Determines if the processor uses the <c>specialsolution</c> subtags instead of the <c>solution</c> subtags if available.</param>
        public void TransformDocument(StreamWriter output, TMLOutputType type, HierarchicalNumber? solutionsUpToTask, bool useSpecialSolution = false)
        {
            //Simply print the active representation of every semantic node to the output.
            foreach (var node in nodes)
            {
                node.Print(output, text, type, solutionsUpToTask, useSpecialSolution);
            }
        }

        /// <summary>
        /// Represents a node used for semantic description of the document.
        /// </summary>
        private abstract class SemanticNode
        {
            /// <summary>
            /// Prints the correct representation according to the transformation type to <paramref name="output"/>.
            /// </summary>
            /// <param name="output">Output stream</param>
            /// <param name="originalDocument">Original content of the document</param>
            /// <param name="type">The transformation type to use</param>
            /// <param name="solutionsUpToTask">Use solutions up to a given task number. Student versions are used otherwise.</param>
            /// <param name="useSpecialSolution">Determines if the processor uses the <c>specialsolution</c> subtags instead of the <c>solution</c> subtags if available.</param>
            public abstract void Print(StreamWriter output, string originalDocument, TMLOutputType type, HierarchicalNumber? solutionsUpToTask, bool useSpecialSolution = false);
        }

        /// <summary>
        /// Represents a part of a document that does not have any snippets but simply text.
        /// </summary>
        private class TextNode : SemanticNode
        {
            /// <summary>
            /// The actual content of this node
            /// </summary>
            public TMLTerminalNode Content { get; set; }

            public TextNode(TMLTerminalNode content)
            {
                this.Content = content;
            }

            public override void Print(StreamWriter output, string originalDocument, TMLOutputType type, HierarchicalNumber? solutionsUpToTask, bool useSpecialSolution)
            {
                //Does not depend on the transformation type, simply print the text.
                output.Write(originalDocument.Substring(Content.BeginIndex, Content.Length));
            }
        }

        /// <summary>
        /// Represents a variable part of a document, i.e. a snippet
        /// </summary>
        private class SnippetNode : SemanticNode
        {
            /// <summary>
            /// Student version of the snippet. May be <c>null</c>.
            /// </summary>
            [SubSnippet("student")]
            public SubSnippet StudentContent { get; set; }

            /// <summary>
            /// Solution version of the snippet. May be <c>null</c>.
            /// </summary>
            [SubSnippet("solution")]
            public SubSnippet SolutionContent { get; set; }

            /// <summary>
            /// Special Solution version of the snippet. May be <c>null</c>.
            /// </summary>
            [SubSnippet("specialsolution")]
            public SubSnippet SpecialSolutionContent { get; set; }

            /// <summary>
            /// Returns if this snippet has at most one active subsnippet. Active snippets are
            /// recognized by the presence of uncommented non-empty lines.
            /// </summary>
            public bool HasAtMostOneActiveSubSnippet
            {
                get
                {
                    int activeSnippets = 0;
                    foreach (var subsnippet in subsnippets)
                    {
                        var value = (SubSnippet)subsnippet.Key.GetValue(this);
                        if (value != null && value.HasUncommentedLines)
                            ++activeSnippets;
                    }                    
                    return activeSnippets <= 1;
                }
            }

            /// <summary>
            /// Stores the string that is used to indent the entire snippet.
            /// </summary>
            public string Indentation { get; set; }

            /// <summary>
            /// Returns the task number that this snippet belongs to.
            /// </summary>
            public HierarchicalNumber? TaskNumber { get; set; }

            /// <summary>
            /// Determines if this snippet belongs to <paramref name="task"/> or one of its subtasks or any task before <paramref name="task"/>.
            /// Returns true if <paramref name="task"/> or <see cref="TaskNumber"/> are <c>null</c>.
            /// </summary>            
            public bool IsInOrBeforeTask(HierarchicalNumber? task)
            {
                if (task == null || TaskNumber == null)
                    return true;

                HierarchicalNumber refTask = task.Value;
                HierarchicalNumber myTask = TaskNumber.Value;

                //go through the number level by level

                if (myTask.Major > refTask.Major)
                    return false;

                if (myTask.Minor == null || refTask.Minor == null)
                    return true;
                if (myTask.Minor > refTask.Minor)
                    return false;

                if (myTask.SubMinor == null || refTask.SubMinor == null)
                    return true;
                if (myTask.SubMinor > refTask.SubMinor)
                    return false;

                return true;
            }

            /// <summary>
            /// Determines if this snippet belongs to <paramref name="task"/> or one of its subtasks.
            /// Returns true if <paramref name="task"/> or <see cref="TaskNumber"/> are <c>null</c>.
            /// </summary>       
            public bool IsInTask(HierarchicalNumber? task)
            {
                if (task == null || TaskNumber == null)
                    return true;

                HierarchicalNumber refTask = task.Value;
                HierarchicalNumber myTask = TaskNumber.Value;

                if (myTask.Major != refTask.Major)
                    return false;

                if (myTask.Minor == null || refTask.Minor == null)
                    return true;
                if (myTask.Minor != refTask.Minor)
                    return false;

                if (myTask.SubMinor == null || refTask.SubMinor == null)
                    return true;
                if (myTask.SubMinor != refTask.SubMinor)
                    return false;

                return true;
            }

            public override void Print(StreamWriter output, string originalDocument, TMLOutputType type, HierarchicalNumber? solutionsUpToTask, bool useSpecialSolution)
            {
                //specifies if the output should complete the entire TML markup.
                var printAll = type == TMLOutputType.TMLWithActiveSolution || type == TMLOutputType.TMLWithActiveStudentVersion;

                var subtagsActive = new Dictionary<string, bool>();

                //determine if the solution is active
                subtagsActive["solution"] = (type == TMLOutputType.TMLWithActiveSolution || type == TMLOutputType.Solution) && IsInOrBeforeTask(solutionsUpToTask);
                //determine if the specialsolution is active
                subtagsActive["specialsolution"] = SpecialSolutionContent != null && useSpecialSolution && subtagsActive["solution"];
                if (subtagsActive["specialsolution"])
                    subtagsActive["solution"] = false;
                subtagsActive["student"] = !subtagsActive["solution"] && !subtagsActive["specialsolution"];

                if (printAll)
                {
                    //Print the initial //<snippet> tag.
                    output.Write("//<snippet");
                    if (TaskNumber.HasValue)
                        output.Write($" task=\"{TaskNumber.ToString()}\"");
                    output.WriteLine (">");
                }

                //Print all subtags if applicable
                foreach (var subtag in subsnippets)
                {
                    var content = (SubSnippet)subtag.Key.GetValue(this);
                    if (content == null)
                        continue;

                    if(printAll)
                    {
                        //print the opening tag
                        output.Write(Indentation);
                        output.WriteLine($"//<{subtag.Value.TagName}>");
                        output.Write(Indentation);
                    }

                    //print the actual content
                    if (subtagsActive[subtag.Value.TagName] || printAll)
                        content.Print(output, subtagsActive[subtag.Value.TagName], Indentation);

                    if(printAll)
                    {
                        //print the closing tag
                        output.WriteLine();
                        output.Write(Indentation);
                        output.WriteLine($"//</{subtag.Value.TagName}>");
                    }
                }                

                if (printAll)
                {
                    //print the closing snippet tag
                    output.Write(Indentation);
                    output.Write("//</snippet>");
                }
            }
        }
        
        /// <summary>
        /// Represents a version of a snippet (e.g. the student version)
        /// </summary>
        class SubSnippet
        {
            /// <summary>
            /// The original lines of this snippet
            /// </summary>
            string[] lines;

            /// <summary>
            /// Index of the first non-whitespace character for each line
            /// </summary>
            int[] firstNonWhiteSpace;

            /// <summary>
            /// Returns true if there is at least one non-empty line without line comments at the beginning
            /// </summary>
            public bool HasUncommentedLines { get; private set; } = false;

            public SubSnippet(string[] lines)
            {               
                this.lines = lines;
                firstNonWhiteSpace = new int[lines.Length];
                for(int l = 0; l < lines.Length; ++l)
                {
                    firstNonWhiteSpace[l] = 0;
                    while (firstNonWhiteSpace[l] < lines[l].Length && Char.IsWhiteSpace(lines[l][firstNonWhiteSpace[l]]))
                        ++firstNonWhiteSpace[l];

                    if (firstNonWhiteSpace[l] < lines[l].Length - 2 && lines[l].Substring(firstNonWhiteSpace[l], 2) != "//")
                        HasUncommentedLines = true;
                }
            }            

            public SubSnippet()
            { }

            /// <summary>
            /// Prints the snippet to <paramref name="output"/>.
            /// </summary>
            /// <param name="output">The output stream</param>
            /// <param name="printUncommented">Determines if an uncommented version should be printed. If the
            /// snippet is already uncommented, no change is performed. Otherwise, line comments will be removed.</param>
            /// <param name="indentation">The indentation to use when producing the text for this snippet.</param>
            /// <exception cref="FormatException">Thrown if line comments should be removed from the snippet but a non-empty line does not begin with a line comment.</exception>
            public void Print(StreamWriter output, bool printUncommented, string indentation)
            {
                //should line comments be added
                bool addComments = !printUncommented && HasUncommentedLines;

                //should line comments be removed
                bool removeComments = !HasUncommentedLines && printUncommented;

                //process all lines
                for (int i = 0; i < lines.Length; ++i)
                {              
                    //add line comments if necessary
                    if (addComments)
                    {
                        if (lines[i].StartsWith(indentation))
                        {
                            //the line's indentation matches the provided one; put the line comment after the indentation
                            output.Write(indentation);
                            output.Write("//");
                            output.Write(lines[i].Substring(indentation.Length, firstNonWhiteSpace[i] - indentation.Length));
                        }
                        else
                        {
                            //the line uses a different indentation; put the line comments just in front of the first non-whitespace character
                            output.Write(lines[i].Substring(0, firstNonWhiteSpace[i]));
                            output.Write("//");
                        }
                    }
                    else
                        //write the indentation
                        output.Write(lines[i].Substring(0, firstNonWhiteSpace[i]));

                    if (firstNonWhiteSpace[i] != lines[i].Length) //if the line is not empty
                    {
                        if (removeComments)
                        {
                            //Check if the line really begins with a line comment and only print the rest
                            if (firstNonWhiteSpace[i] > lines[i].Length - 2 || lines[i].Substring(firstNonWhiteSpace[i], 2) != "//")
                                throw new FormatException("Cannot remove comment from line \"" + lines[i] + "\".");
                            output.Write(lines[i].Substring(firstNonWhiteSpace[i] + 2));
                        }
                        else
                            //Print the original line
                            output.Write(lines[i].Substring(firstNonWhiteSpace[i]));
                    }

                    if (i != lines.Length - 1)
                        output.WriteLine();
                }
            }
        }

        /// <summary>
        /// Interprets the abstract syntax of the TML document and generates semantic node representatives.
        /// </summary>
        /// <exception cref="FormatException">Thrown if an unexpected tag or a task number in an invalid format is encountered and if more than one subsnippet is active.</exception>
        private void ParseTML()
        {
            nodes.Clear();            

            //process all nodes of the abstract syntax tree
            foreach (var node in syntaxTree.Nodes)
            {
                var contentNode = node as TMLTerminalNode;
                var tagNode = node as TMLTagNode;

                //if the node is just content, add a new text node
                if (contentNode != null)
                    nodes.Add(new TextNode(contentNode));

                //if the node is a tag node, try to interpret it as a snippet
                else if (tagNode != null)
                {
                    if (tagNode.TagName != "snippet")
                        throw new FormatException($"Did not expect tag {tagNode.TagName} here.");

                    //Create the semantic snippet node
                    var snippetNode = new SnippetNode();

                    //Try to find the indentation of this snippet (everything that is a whitespace in front of the snippet tag)
                    var startIndentIncl = tagNode.OpeningTagBeginIndex;
                    Func<char, bool> isIndentation = (char c) => Char.IsWhiteSpace(c) && c != '\r' && c != '\n';
                    while (startIndentIncl > 0 && isIndentation(text[startIndentIncl - 1]))
                        --startIndentIncl;
                    snippetNode.Indentation = text.Substring(startIndentIncl, tagNode.OpeningTagBeginIndex - startIndentIncl);

                    //Try to parse the task number
                    string taskNumber;
                    if (tagNode.Attributes.TryGetValue("task", out taskNumber))
                    {
                        try
                        {
                            snippetNode.TaskNumber = HierarchicalNumber.ParseFromString(taskNumber);
                        }
                        catch (Exception x)
                        {
                            throw new FormatException($"Error parsing hierarchical number from {taskNumber}.", x);
                        }
                    }

                    //Assign the existing subsnippets
                    foreach (var subsnippet in subsnippets)
                        subsnippet.Key.SetValue(snippetNode, FindSubSnippet(tagNode, subsnippet.Value.TagName));

                    //Check if there are invalid subtags
                    if (tagNode.InnerNodes.Where(n => n is TMLTagNode).Select(n => (TMLTagNode)n).Any(n => !subsnippetTags.Contains(n.TagName)))
                        throw new FormatException("The snippet tag contains invalid subtags.");

                    if (!snippetNode.HasAtMostOneActiveSubSnippet)
                        throw new FormatException("More than one subsnippet have uncommented lines.");

                    nodes.Add(snippetNode);
                }
            }           
        }

        /// <summary>
        /// Finds the sub snippet with the given tagname in the parenting tag node.
        /// </summary>
        /// <param name="tagNode">The parenting tag node</param>
        /// <param name="snippetName">The subtag to search</param>
        /// <returns>The sub snippet if it is found or <c>null</c> otherwise</returns>
        /// <exception cref="FormatException">Thrown if the subtag has more than just a single <see cref="TMLTerminalNode"/>.</exception>
        private SubSnippet FindSubSnippet(TMLTagNode tagNode, string snippetName)
        {            
            var subnode = tagNode.InnerNodes.Where(n => n is TMLTagNode).Select(n => (TMLTagNode)n).Where(n => n.TagName == snippetName).FirstOrDefault();
            if (subnode != null && subnode.InnerNodes.Count > 0)
            {
                //only single inner node allowed
                if (subnode.InnerNodes.Count > 1 || !(subnode.InnerNodes.First() is TMLTerminalNode))
                    throw new FormatException("The solution tag may not have nested tags.");                
                return ParseTrimmedContent((TMLTerminalNode)subnode.InnerNodes.First());                
            }
            return null;
        }        

        /// <summary>
        /// Returns a new node for a subsnippet whose whitespace at the beginning and end are cut off.
        /// </summary>
        /// <param name="content">The full content of the node to create, which may contain unwanted white spaces</param>
        SubSnippet ParseTrimmedContent(TMLTerminalNode content)
        {
            var startIncl = content.BeginIndex;
            var endIncl = content.BeginIndex + content.Length - 1;
            while (Char.IsWhiteSpace(text[startIncl]) && startIncl <= endIncl)
                ++startIncl;
            if (startIncl > endIncl)
                return new SubSnippet(); //no left content, all white-space

            while (Char.IsWhiteSpace(text[endIncl]))
                --endIncl;

            return new SubSnippet(text.Substring(startIncl, endIncl + 1 - startIncl).Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None));
        }      
    }
}
