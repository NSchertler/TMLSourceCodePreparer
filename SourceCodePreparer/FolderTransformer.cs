using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SourceCodePreparer
{
    class FolderTransformer
    {
        string lastFolder = null;

        public string BackupFolder { get; private set; }
        public FolderTransformer()
        {
            //Create backup folder if it does not exist
            BackupFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\SourceCodePreparer\Backup";
            Directory.CreateDirectory(BackupFolder);
        }

        public void TransformFolder(string folder, TMLOutputType outputType, string filter, string externalFolder, HierarchicalNumber? upToTask, bool onlyTransformedFiles, bool useSpecialSolution)
        {
            if (folder == null)
                throw new NullReferenceException("No input folder is provided for transformation.");
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
                    throw new NullReferenceException("No output folder is provided for transformation.");
                externalDir = new DirectoryInfo(externalFolder);
                if (externalDir.FullName == dir.FullName)
                    throw new ArgumentException("The source and target directories cannot be the same.");                
                externalDir.Create();
            } 

            var filters = new HashSet<string>(filter.Split(' '));

            bool copyUntransformedFiles = (outputType == TMLOutputType.StudentVersion || outputType == TMLOutputType.Solution) && !onlyTransformedFiles;

            foreach (string newPath in Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories))
            {
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

                if (filters.Contains(inputFile.Extension))
                {
                    try
                    {
                        transformed = TransformFile(inputFile, outputType, targetFilename, upToTask, useSpecialSolution);
                    }
                    catch(Exception x)
                    {
                        throw new Exception($"Error transforming file \"{inputFile.FullName}\": {x.Message}", x);
                    }
                }

                if(!transformed && copyUntransformedFiles)
                    inputFile.CopyTo(targetFilename);
            }
        }

        bool TransformFile(FileInfo file, TMLOutputType outputType, string targetFilename, HierarchicalNumber? upToTask, bool useSpecialSolution)
        {
            var doc = new TMLDocument(File.ReadAllText(file.FullName));
            if (doc.GetSnippetCount() == 0)
                return false;

            if (outputType == TMLOutputType.Solution && doc.GetSnippetCountForTask(upToTask) == 0)
                return false;
            
            using (var ms = new MemoryStream())
            {
                //Construct the new file first. Then, if no errors occured, write the file.
                doc.TransformDocument(ms, outputType, upToTask, useSpecialSolution);
                ms.Seek(0, SeekOrigin.Begin);
                System.IO.File.WriteAllText(targetFilename, string.Empty);
                using (var fs = new FileStream(targetFilename, FileMode.OpenOrCreate))
                    ms.CopyTo(fs);                    
            }

            return true;
        }

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
