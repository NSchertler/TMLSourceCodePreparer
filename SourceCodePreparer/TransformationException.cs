using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TML
{
    /// <summary>
    /// Represents an arbitrary error during TML transformation.
    /// </summary>
    class FileTransformationException : Exception
    {
        /// <summary>
        /// Returns a path to the file that caused the error.
        /// </summary>
        public string File { get; private set; }

        public FileTransformationException(string file)
            : base($"Error during transformation of file \"{file}\".")
        {
            File = file;
        }

        public FileTransformationException(string file, Exception inner)
            : base($"Error during transformation of file \"{file}\": {inner.Message}", inner)
        {
            File = file;
        }
    }
}
