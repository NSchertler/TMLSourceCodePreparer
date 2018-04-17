using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TML
{
    /// <summary>
    /// Performs TML transformation for all files in a given folder.
    /// </summary>
    class FolderTransformer
    {
        /// <summary>
        /// The source folder of the last transformation. Used to determine if files should be backed up.
        /// </summary>
        string lastFolder = null;

        /// <summary>
        /// Returns the location of the backup folder.
        /// </summary>
        public string BackupFolder { get; private set; }

        public FolderTransformer()
        {
            //Create backup folder if it does not exist
            BackupFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\SourceCodePreparer\Backup";
            Directory.CreateDirectory(BackupFolder);
        }

        /// <summary>
        /// Starts the transformation of all files in the given source folder.
        /// </summary>
        /// <param name="folder">Source folder to process</param>
        /// <param name="outputType">The type of the transformation to perform</param>
        /// <param name="filter">The filter string for file extensions (file extensions separated by <c>' '</c>; extensions must start with <c>'.'</c>)</param>
        /// <param name="externalFolder">The output folder. Only used if <paramref name="outputType"/> is <see cref="TMLOutputType.Solution"/> or <see cref="TMLOutputType.StudentVersion"/>.</param>
        /// <param name="solutionsUpToTask">Use solutions up to a given task number. Student versions are used otherwise. If <paramref name="outputType"/> is <see cref="TMLOutputType.Solution"/> and <paramref name="onlyTransformedFiles"/> is <c>true</c>, the output will only contain files that have snippets with the provided task number.</param>
        /// <param name="onlyTransformedFiles">Determines if only files with any snippets in them are copied to the output directly. Only used if <paramref name="outputType"/> is <see cref="TMLOutputType.Solution"/> or <see cref="TMLOutputType.StudentVersion"/>.</param>
        /// <param name="useSpecialSolution">Determines if the processor uses the <c>specialsolution</c> subtags instead of the <c>solution</c> subtags if available.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="folder"/> is <c>null</c> or if <paramref name="externalFolder"/> is null but required for the current operation.</exception>
        /// <exception cref="ArgumentException">Thrown if the directory defined by <paramref name="folder"/> does not exist or if <paramref name="externalFolder"/> is required for the current operation and points to the same directory as <paramref name="folder"/>.</exception>
        /// <exception cref="FileTransformationException">Thrown if one of the files cannot be transformed.</exception>
        public void TransformFolder(string folder, TMLOutputType outputType, string filter, string externalFolder, HierarchicalNumber? solutionsUpToTask, bool onlyTransformedFiles, bool useSpecialSolution)
        {
            if (folder == null)
                throw new ArgumentNullException("No input folder is provided for transformation.");
            var dir = new DirectoryInfo(folder);
            if (!dir.Exists)
                throw new ArgumentException($"The directory {folder} does not exist.");

            //Backup
            if (folder != lastFolder)
                CopyDirectories(dir, new DirectoryInfo(BackupFolder + "\\" + dir.Name));

            DirectoryInfo externalDir = null;
            if (outputType == TMLOutputType.Solution || outputType == TMLOutputType.StudentVersion)
            {
                if (externalFolder == null)
                    throw new ArgumentNullException("No output folder is provided for transformation.");
                externalDir = new DirectoryInfo(externalFolder);
                if (externalDir.FullName == dir.FullName)
                    throw new ArgumentException("The source and target directories cannot be the same.");                
                externalDir.Create();
            } 

            //put all filters into a hash set for efficient queries
            var filters = new HashSet<string>(filter.Split(' '));

            //determines if we want to copy files that have no transformed snippets.
            bool copyUntransformedFiles = (outputType == TMLOutputType.StudentVersion || outputType == TMLOutputType.Solution) && !onlyTransformedFiles;

            //process each file in the directory
            foreach (string newPath in Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories))
            {
                //stores if the current file has been transformed
                bool transformed = false;
                var inputFile = new FileInfo(newPath);

                string targetFilename;
                if (outputType == TMLOutputType.Solution || outputType == TMLOutputType.StudentVersion)
                {
                    targetFilename = newPath.Replace(dir.FullName, externalDir.FullName);
                    var targetFile = new FileInfo(targetFilename);
                    if (targetFile.Exists)
                        targetFile.Delete();
                }
                else
                    targetFilename = inputFile.FullName;

                //only process files with the provided extensions
                if (filters.Contains(inputFile.Extension))
                {
                    try
                    {
                        //try to perform the transformation
                        transformed = TransformFile(inputFile, outputType, targetFilename, solutionsUpToTask, useSpecialSolution);
                    }
                    catch(Exception x)
                    {
                        throw new FileTransformationException(inputFile.FullName, x);
                    }
                }

                //Copy untransformed files to the output directory if applicable
                if(!transformed && copyUntransformedFiles)
                    inputFile.CopyTo(targetFilename);
            }
        }

        /// <summary>
        /// Performs transformation for a single file.
        /// </summary>
        /// <param name="file">The file to transform</param>
        /// <param name="outputType">The type of the transformation to perform</param>
        /// <param name="targetFilename">The output file into which to write the result.</param>
        /// <param name="solutionsUpToTask">Use solutions up to a given task number. Student versions are used otherwise. If <paramref name="outputType"/> is <see cref="TMLOutputType.Solution"/> and <paramref name="onlyTransformedFiles"/> is <c>true</c>, the output will only contain files that have snippets with the provided task number.</param>
        /// <param name="useSpecialSolution">Determines if the processor uses the <c>specialsolution</c> subtags instead of the <c>solution</c> subtags if available.</param>        
        /// <returns>Returns true if the file has been transformed. Returns false if the file did not contain any relevant snippets.</returns>
        bool TransformFile(FileInfo file, TMLOutputType outputType, string targetFilename, HierarchicalNumber? solutionsUpToTask, bool useSpecialSolution)
        {
            //Parse the input file
            var doc = new TMLDocument(File.ReadAllText(file.FullName));

            //If the file does not contain any snippets, do not do anything
            if (doc.GetSnippetCount() == 0)
                return false;

            //Check if there are relevant snippets.
            if (outputType == TMLOutputType.Solution && doc.GetSnippetCountForTask(solutionsUpToTask) == 0)
                return false;
            
            using (var ms = new MemoryStream())
            {
                //Construct the new file first. Then, if no errors occured, write the file.
                doc.TransformDocument(ms, outputType, solutionsUpToTask, useSpecialSolution);
                ms.Seek(0, SeekOrigin.Begin);
                System.IO.File.WriteAllText(targetFilename, string.Empty);
                using (var fs = new FileStream(targetFilename, FileMode.OpenOrCreate))
                    ms.CopyTo(fs);                    
            }

            return true;
        }

        /// <summary>
        /// Copies all files and folders in <paramref name="sourceFolder"/> to <paramref name="targetFolder"/>.
        /// If the target folder already exists, all content is removed.
        /// </summary>
        private void CopyDirectories(DirectoryInfo sourceFolder, DirectoryInfo targetFolder)
        {            
            if (targetFolder.Exists)
                targetFolder.Delete(true);
            targetFolder.Create();

            //Now Create all of the directories
            foreach (string dirPath in Directory.GetDirectories(sourceFolder.FullName, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(dirPath.Replace(sourceFolder.FullName, targetFolder.FullName));

            //Copy all the files & Replaces any files with the same name
            foreach (string newPath in Directory.GetFiles(sourceFolder.FullName, "*.*", SearchOption.AllDirectories))
                File.Copy(newPath, newPath.Replace(sourceFolder.FullName, targetFolder.FullName), true);
        }
    }
}
