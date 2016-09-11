using RimoteWorld.Core.API;
using System.Reflection;
using RimoteWorld.Core;
using RimoteWorld.Core.API.UI;

namespace RimoteWorld.Server.API
{
    class ServerAPI : IServerAPI
    {
        public Version GetRimoteWorldVersion()
        {
            return Assembly.GetAssembly(typeof(ServerAPI)).GetName().Version;
        }

        public Version GetRimWorldVersion()
        {
            return RimWorld.VersionControl.CurrentVersion;
        }

        public Version GetCCLVersion()
        {
            return CommunityCoreLibrary.Version.Current;
        }
    }
}
