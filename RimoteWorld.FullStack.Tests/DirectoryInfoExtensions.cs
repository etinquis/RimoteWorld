using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RimoteWorld.FullStack.Tests
{
    public static class DirectoryInfoExtensions
    {
        public static void CopyTo(this DirectoryInfo dir, string fullPath)
        {
            DirectoryInfo targetDir = new DirectoryInfo(fullPath);
            targetDir.Create();

            foreach (var subDir in dir.GetDirectories())
            {
                subDir.CopyTo(Path.Combine(fullPath, subDir.Name));
            }

            foreach (var file in dir.GetFiles())
            {
                file.CopyTo(Path.Combine(fullPath, file.Name));
            }
        }
    }
}
