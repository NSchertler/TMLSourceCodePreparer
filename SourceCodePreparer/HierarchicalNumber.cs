using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TML
{
    /// <summary>
    /// Represents an immutable hierarchical integer number with up to three levels in the form Major.Minor.SubMinor
    /// </summary>
    public struct HierarchicalNumber
    {
        /// <summary>
        /// Returns the Major part of the number.
        /// </summary>
        public int Major { get; private set; }

        /// <summary>
        /// Returns the Minor part of the number or <c>null</c> if the number does not contain this level.
        /// </summary>
        public int? Minor { get; private set; }

        /// <summary>
        /// Returns the SubMinor part of the number or <c>null</c>  if the number does not contain this level.
        /// </summary>
        public int? SubMinor { get; private set; }

        /// <summary>
        /// Regular expression used for parsing
        /// </summary>
        private static Regex parseRegex;

        /// <summary>
        /// Instantiates a hierarchical number with a given Major. Minor and SubMinor are set to <c>null</c>.
        /// </summary>
        public HierarchicalNumber(int major)
        {
            Major = major;
            Minor = SubMinor = null;
        }

        /// <summary>
        /// Instantiates a hierarchical number with a given Major and Minor. SubMinor is set to <c>null</c>.
        /// </summary>
        public HierarchicalNumber(int major, int? minor)
        {
            Major = major;
            Minor = minor;
            SubMinor = null;
        }

        /// <summary>
        /// Instantiates a hierarchical number with a given Major, Minor, and SubMinor.
        /// </summary>        
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

        /// <summary>
        /// Parses a hierarchical number from its string representation.
        /// </summary>
        /// <exception cref="FormatException">Thrown if the provided string does not represent a hierarchical number.</exception>
        public static HierarchicalNumber ParseFromString(string input)
        {
            if(parseRegex == null)
                parseRegex = new Regex(@"(?<major>\d)(\.(?<minor>\d)(\.(?<subminor>\d))?)?");
            var matches = parseRegex.Matches(input);
            if (matches.Count != 1)
                throw new FormatException($"Cannot parse hierarchical number from \"{input}\"");
            int major = int.Parse(matches[0].Groups["major"].Value);

            var minorCapture = matches[0].Groups["minor"];
            int? minor = null;
            if (minorCapture.Success)
                minor = int.Parse(minorCapture.Value);

            var subminorCapture = matches[0].Groups["subminor"];
            int? subminor = null;
            if (subminorCapture.Success)
                subminor = int.Parse(subminorCapture.Value);

            return new HierarchicalNumber(major, minor, subminor);
        }
    }
}
