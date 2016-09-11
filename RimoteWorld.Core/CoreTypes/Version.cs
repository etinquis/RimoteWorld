using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RimoteWorld.Core
{
    public class Version
    {
        public int Major { get; set; }
        public int Minor { get; set; }
        public int Revision { get; set; }
        public int Build { get; set; }

        public static implicit operator Version(System.Version v)
        {
            return new Version()
            {
                Major = v.Major,
                Minor = v.Minor,
                Revision = v.Revision,
                Build = v.Build
            };
        }

        public static explicit operator System.Version(Version v)
        {
            if (v.Build < 0)
            {
                return new System.Version(v.Major, v.Minor);
            }
            else if (v.Revision < 0)
            {
                return new System.Version(v.Major, v.Minor, v.Build);
            }
            return new System.Version(v.Major, v.Minor, v.Build, v.Revision);
        }

        public override string ToString()
        {
            return ((System.Version)this).ToString();
        }
    }
}
