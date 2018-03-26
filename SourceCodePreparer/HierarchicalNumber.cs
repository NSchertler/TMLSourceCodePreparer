using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SourceCodePreparer
{
    public struct HierarchicalNumber
    {
        public int Major { get; private set; }
        public int? Minor { get; private set; }
        public int? SubMinor { get; private set; }

        private static Regex parseRegex;

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
