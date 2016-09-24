using RimoteWorld.Core.API;
using System.Reflection;
using RimoteWorld.Core;
using RimoteWorld.Core.API.UI;
using Verse;

namespace RimoteWorld.Server.API
{
    class ServerAPI : IServerAPI
    {
        private ILogContext serverAPILog = Log.CreateContext("ServerAPI");
        public GameState GetRimWorldGameState()
        {
            using (var log = serverAPILog.CreateSubcontext("GetRimWorldGameState"))
            {
                log.Debug("Checking Current.Root");

                var root = Current.Root;
                if (root == null) return GameState.Initializing;
                if (root.uiRoot == null) return GameState.Initializing;

                log.Debug("Checking globalInitDone");

                var isInitialized = (bool) typeof(Root).GetField("globalInitDone", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                if (!isInitialized) return GameState.Initializing;

                log.Debug("Checking Root.uiRoot");
                var uiRoot = (root.uiRoot as UIRoot_Entry);
                if (GenScene.InEntryScene && uiRoot != null)
                {
                    if (!LongEventHandler.ShouldWaitForEvent)
                    {
                        return GameState.MainMenu;
                    }
                }

                log.Debug("Giving up");
                return GameState.Unknown;
            }
        }

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
