using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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

    public class TMLDocument
    {
        static Dictionary<PropertyInfo, SubSnippetAttribute> subsnippets = new Dictionary<PropertyInfo, SubSnippetAttribute>();
        static HashSet<string> subsnippetTags = new HashSet<string>();
        static TMLDocument()
        {
            
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
        
        public void TransformDocument(Stream output, TMLOutputType type, HierarchicalNumber? solutionsUpToTask, bool useSpecialSolution = false)
        {
            using (var str = new StreamWriter(output, Encoding.UTF8, 0x8000, true))
                TransformDocument(str, type, solutionsUpToTask, useSpecialSolution);
        }

        public void TransformDocument(StreamWriter output, TMLOutputType type, HierarchicalNumber? solutionsUpToTask, bool useSpecialSolution = false)
        {
            foreach (var node in nodes)
            {
                node.Print(output, text, type, solutionsUpToTask, useSpecialSolution);
            }
        }

        private abstract class SemanticNode
        {
            public abstract void Print(StreamWriter output, string originalDocument, TMLOutputType type, HierarchicalNumber? solutionsUpToTask, bool useSpecialSolution = false);
        }

        private class TextNode : SemanticNode
        {
            public TMLTerminalNode Content { get; set; }

            public TextNode(TMLTerminalNode content)
            {
                this.Content = content;
            }

            public override void Print(StreamWriter output, string originalDocument, TMLOutputType type, HierarchicalNumber? solutionsUpToTask, bool useSpecialSolution)
            {
                output.Write(originalDocument.Substring(Content.BeginIndex, Content.Length));
            }
        }


        private class SnippetNode : SemanticNode
        {
            [SubSnippet("student")]
            public Snippet StudentContent { get; set; }

            [SubSnippet("solution")]
            public Snippet SolutionContent { get; set; }

            [SubSnippet("specialsolution")]
            public Snippet SpecialSolutionContent { get; set; }

            public bool HasAtMostOneActiveSubSnippet
            {
                get
                {
                    int activeSnippets = 0;
                    foreach (var subsnippet in subsnippets)
                    {
                        var value = (Snippet)subsnippet.Key.GetValue(this);
                        if (value != null && value.HasUncommentedLines)
                            ++activeSnippets;
                    }                    
                    return activeSnippets <= 1;
                }
            }

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

            public override void Print(StreamWriter output, string originalDocument, TMLOutputType type, HierarchicalNumber? solutionsUpToTask, bool useSpecialSolution)
            {
                var printAll = type == TMLOutputType.TMLWithActiveSolution || type == TMLOutputType.TMLWithActiveStudentVersion;

                bool solutionIsActive = (type == TMLOutputType.TMLWithActiveSolution || type == TMLOutputType.Solution) && IsInOrBeforeTask(solutionsUpToTask);
                bool specialSolutionIsActive = SpecialSolutionContent != null && useSpecialSolution && solutionIsActive;
                if (specialSolutionIsActive)
                    solutionIsActive = false;

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

                if (!(solutionIsActive || specialSolutionIsActive) || printAll)
                {
                    if (StudentContent != null)
                        StudentContent.Print(output, printUncommented: !(solutionIsActive || specialSolutionIsActive), indentation: Indentation);
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
                        SolutionContent.Print(output, printUncommented: solutionIsActive, indentation: Indentation);
                }

                if (printAll)
                {
                    if (SolutionContent != null)
                    {
                        output.WriteLine();
                        output.Write(Indentation);
                        output.WriteLine("//</solution>");
                    }
                    if (SpecialSolutionContent != null)
                    {
                        output.Write(Indentation);
                        output.WriteLine("//<specialsolution>");
                        output.Write(Indentation);
                    }
                }

                if (specialSolutionIsActive || printAll)
                {
                    if (SpecialSolutionContent != null)
                        SpecialSolutionContent.Print(output, printUncommented: specialSolutionIsActive, indentation: Indentation);
                }

                if (printAll)
                {
                    if (SpecialSolutionContent != null)
                    {
                        output.WriteLine();
                        output.Write(Indentation);
                        output.WriteLine("//</specialsolution>");
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

            public void Print(StreamWriter output, bool printUncommented, string indentation)
            {
                bool addComments = !printUncommented && HasUncommentedLines;
                bool removeComments = !HasUncommentedLines && printUncommented;

                for (int i = 0; i < lines.Length; ++i)
                {                    
                    if (addComments)
                    {
                        if (lines[i].StartsWith(indentation))
                        {
                            output.Write(indentation);
                            output.Write("//");
                            output.Write(lines[i].Substring(indentation.Length, firstNonWhiteSpace[i] - indentation.Length));
                        }
                        else
                        {
                            output.Write(lines[i].Substring(0, firstNonWhiteSpace[i]));
                            output.Write("//");
                        }
                    }
                    else
                        output.Write(lines[i].Substring(0, firstNonWhiteSpace[i]));

                    if (firstNonWhiteSpace[i] != lines[i].Length)
                    {
                        if (removeComments)
                        {
                            if (firstNonWhiteSpace[i] > lines[i].Length - 2 || lines[i].Substring(firstNonWhiteSpace[i], 2) != "//")
                                throw new FormatException("Cannot remove comment from line \"" + lines[i] + "\".");
                            output.Write(lines[i].Substring(firstNonWhiteSpace[i] + 2));
                        }
                        else
                            output.Write(lines[i].Substring(firstNonWhiteSpace[i]));
                    }

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
