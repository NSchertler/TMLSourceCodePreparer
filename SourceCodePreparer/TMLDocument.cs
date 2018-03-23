using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SourceCodePreparer
{
    public enum TMLOutputType
    {
        Solution,
        StudentVersion,
        TMLWithActiveSolution,
        TMLWithActiveStudentVersion
    }

    public struct HierarchicalNumber
    {
        public int Major { get; private set; }
        public int? Minor { get; private set; }
        public int? SubMinor { get; private set; }

        public HierarchicalNumber(int major)
        {
            Major = major;
            Minor = SubMinor = null;
        }

        public HierarchicalNumber(int major, int? minor)
        {
            Major = major;
            Minor = minor;
            SubMinor = null;
        }

        public HierarchicalNumber(int major, int? minor, int? subminor)
        {
            Major = major;
            Minor = minor;
            SubMinor = subminor;
        }

        public override string ToString()
        {
            if (!Minor.HasValue && !SubMinor.HasValue)
                return Major.ToString();
            else if (!SubMinor.HasValue)
                return Major.ToString() + "." + Minor.ToString();
            else
                return Major.ToString() + "." + Minor.ToString() + "." + SubMinor.ToString();
        }

        public static HierarchicalNumber ParseFromString(string input)
        {
            var regex = new Regex(@"(?<major>\d)(\.(?<minor>\d)(\.(?<subminor>\d))?)?");
            var matches = regex.Matches(input);
            if (matches.Count != 1)
                throw new FormatException($"Cannot parse hierarchical number from \"{input}\"");
            int major = int.Parse(matches[0].Groups["major"].Value);

            var minorCapture = matches[0].Groups["minor"];
            int? minor = null;
            if(minorCapture.Success)
                minor = int.Parse(minorCapture.Value);

            var subminorCapture = matches[0].Groups["subminor"];
            int? subminor = null;
            if(subminorCapture.Success)
                subminor = int.Parse(subminorCapture.Value);

            return new HierarchicalNumber(major, minor, subminor);
        }
    }

    public class TMLDocument
    {        
        private string text;
        private TMLSyntaxTree syntaxTree;

        private List<SemanticNode> nodes = new List<SemanticNode>();

        public TMLDocument(string text)
        {
            this.text = text;

            syntaxTree = new TMLSyntaxTree(text);
            ParseTML();
        }

        public int GetSnippetCount()
        {
            return nodes.Where(n => n is SnippetNode).Count();
        }

        public int GetSnippetCountForTask(HierarchicalNumber? task)
        {
            return nodes.Where(n => n is SnippetNode).Select(n => (SnippetNode)n).Where(n => n.IsInTask(task)).Count();
        }
        
        public void TransformDocument(Stream output, TMLOutputType type, HierarchicalNumber? solutionsUpToTask)
        {
            using (var str = new StreamWriter(output, Encoding.UTF8, 0x8000, true))
                TransformDocument(str, type, solutionsUpToTask);
        }

        public void TransformDocument(StreamWriter output, TMLOutputType type, HierarchicalNumber? solutionsUpToTask)
        {
            foreach (var node in nodes)
            {
                node.Print(output, text, type, solutionsUpToTask);
            }
        }

        private abstract class SemanticNode
        {
            public abstract void Print(StreamWriter output, string originalDocument, TMLOutputType type, HierarchicalNumber? solutionsUpToTask);
        }

        private class TextNode : SemanticNode
        {
            public TMLTerminalNode Content { get; set; }

            public TextNode(TMLTerminalNode content)
            {
                this.Content = content;
            }

            public override void Print(StreamWriter output, string originalDocument, TMLOutputType type, HierarchicalNumber? solutionsUpToTask)
            {
                output.Write(originalDocument.Substring(Content.BeginIndex, Content.Length));
            }
        }


        private class SnippetNode : SemanticNode
        {
            public Snippet StudentContent { get; set; }
            public Snippet SolutionContent { get; set; }

            public string Indentation { get; set; }

            public HierarchicalNumber? TaskNumber { get; set; }

            public bool IsInOrBeforeTask(HierarchicalNumber? task)
            {
                if (task == null || TaskNumber == null)
                    return true;

                HierarchicalNumber refTask = task.Value;
                HierarchicalNumber myTask = TaskNumber.Value;

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

            public override void Print(StreamWriter output, string originalDocument, TMLOutputType type, HierarchicalNumber? solutionsUpToTask)
            {
                var printAll = type == TMLOutputType.TMLWithActiveSolution || type == TMLOutputType.TMLWithActiveStudentVersion;

                bool solutionIsActive = (type == TMLOutputType.TMLWithActiveSolution || type == TMLOutputType.Solution) && IsInOrBeforeTask(solutionsUpToTask);

                if (printAll)
                {
                    output.Write("//<snippet");
                    if (TaskNumber.HasValue)
                        output.Write($" task=\"{TaskNumber.ToString()}\"");
                    output.WriteLine (">");

                    if (StudentContent != null)
                    {
                        output.Write(Indentation);
                        output.WriteLine("//<student>");
                        output.Write(Indentation);
                    }
                }

                if (!solutionIsActive || printAll)
                {
                    if (StudentContent != null)
                        StudentContent.Print(output, printUncommented: !solutionIsActive);
                }

                if (printAll)
                {
                    if (StudentContent != null)
                    {
                        output.WriteLine();
                        output.Write(Indentation);
                        output.WriteLine("//</student>");
                    }
                    if (SolutionContent != null)
                    {
                        output.Write(Indentation);
                        output.WriteLine("//<solution>");
                        output.Write(Indentation);
                    }
                }

                if (solutionIsActive || printAll)
                {
                    if(SolutionContent != null)
                        SolutionContent.Print(output, printUncommented: solutionIsActive);
                }

                if (printAll)
                {
                    if (SolutionContent != null)
                    {
                        output.WriteLine();
                        output.Write(Indentation);
                        output.WriteLine("//</solution>");
                    }
                    output.Write(Indentation);
                    output.Write("//</snippet>");
                }
            }
        }
        
        class Snippet
        {
            string[] lines;
            int[] firstNonWhiteSpace;

            public bool HasUncommentedLines { get; private set; } = false;

            public Snippet(string[] lines)
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

            public Snippet()
            { }

            public void Print(StreamWriter output, bool printUncommented)
            {
                bool addComments = !printUncommented && HasUncommentedLines;
                bool removeComments = !HasUncommentedLines && printUncommented;

                for (int i = 0; i < lines.Length; ++i)
                {
                    output.Write(lines[i].Substring(0, firstNonWhiteSpace[i]));
                    if (firstNonWhiteSpace[i] == lines[i].Length)
                        continue;
                    if (addComments)
                        output.Write("//");
                    if (removeComments)
                    {
                        if (firstNonWhiteSpace[i] > lines[i].Length - 2 || lines[i].Substring(firstNonWhiteSpace[i], 2) != "//")
                            throw new FormatException("Cannot remove comment from line \"" + lines[i] + "\".");
                        output.Write(lines[i].Substring(firstNonWhiteSpace[i] + 2));
                    }
                    else
                        output.Write(lines[i].Substring(firstNonWhiteSpace[i]));
                    if (i != lines.Length - 1)
                        output.WriteLine();
                }
            }
        }

        private void ParseTML()
        {
            nodes.Clear();            

            foreach (var node in syntaxTree.Nodes)
            {
                var contentNode = node as TMLTerminalNode;
                var tagNode = node as TMLTagNode;
                if (contentNode != null)
                    nodes.Add(new TextNode(contentNode));
                else if (tagNode != null)
                {
                    if (tagNode.TagName != "snippet")
                        throw new FormatException($"Did not expect tag {tagNode.TagName} here.");

                    var snippetNode = new SnippetNode();

                    var startIndentIncl = tagNode.OpeningTagBeginIndex;
                    Func<char, bool> isIndentation = (char c) => Char.IsWhiteSpace(c) && c != '\r' && c != '\n';
                    while (startIndentIncl > 0 && isIndentation(text[startIndentIncl - 1]))
                        --startIndentIncl;
                    snippetNode.Indentation = text.Substring(startIndentIncl, tagNode.OpeningTagBeginIndex - startIndentIncl);

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

                    snippetNode.SolutionContent = FindSubSnippet(tagNode, "solution");
                    snippetNode.StudentContent = FindSubSnippet(tagNode, "student");

                    //Check if there are invalid subtags
                    if (tagNode.InnerNodes.Where(n => n is TMLTagNode).Select(n => (TMLTagNode)n).Where(n => n.TagName != "solution" && n.TagName != "student").Count() > 0)
                        throw new FormatException("The snippet tag may only contain solution or student subtags.");

                    if (snippetNode.SolutionContent != null && snippetNode.SolutionContent.HasUncommentedLines && snippetNode.StudentContent != null && snippetNode.StudentContent.HasUncommentedLines)                        
                        throw new FormatException("Cannot determine if the snippet has an active solution or student version. Both have uncommented lines.");

                    nodes.Add(snippetNode);
                }
            }           
        }

        private Snippet FindSubSnippet(TMLTagNode tagNode, string snippetName)
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

        Snippet ParseTrimmedContent(TMLTerminalNode content)
        {
            var startIncl = content.BeginIndex;
            var endIncl = content.BeginIndex + content.Length - 1;
            while (Char.IsWhiteSpace(text[startIncl]) && startIncl <= endIncl)
                ++startIncl;
            if (startIncl > endIncl)
                return new Snippet(); //no left content, all white-space

            while (Char.IsWhiteSpace(text[endIncl]))
                --endIncl;

            return new Snippet(text.Substring(startIncl, endIncl + 1 - startIncl).Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None));
        }      
    }
}
