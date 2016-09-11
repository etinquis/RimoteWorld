using RimoteWorld.Core.API;
using System.Reflection;
using RimoteWorld.Core;
using System.Threading.Tasks;
using RimoteWorld.Client.API.Remote;
using RimoteWorld.Core.Messaging.Instancing.UI;
using RimoteWorld.Core.API.UI;

namespace RimoteWorld.Client.API
{
    public class ClientAPI : IClientAPI, IRemoteServerAPI, IRemoteMainMenuAPI, System.IDisposable
    {
        private RPCClient _rpcClient;

        public ClientAPI()
        {
            _rpcClient = new RPCClient();
        }

        public void Shutdown()
        {
            Dispose();
        }

        public void Dispose()
        {
            _rpcClient.Dispose();
        }

        Version IClientAPI.GetRimoteWorldVersion()
        {
            return Assembly.GetCallingAssembly().GetName().Version;
        }

        Task<Version> IRemoteServerAPI.GetRimoteWorldVersion()
        {
            return _rpcClient.MakeRemoteStaticCall<IServerAPI, Version>((api) => api.GetRimoteWorldVersion());
        }

        Task<Version> IRemoteServerAPI.GetRimWorldVersion()
        {
            return _rpcClient.MakeRemoteStaticCall<IServerAPI, Version>((api) => api.GetRimWorldVersion());
        }

        Task<Version> IRemoteServerAPI.GetCCLVersion()
        {
            return _rpcClient.MakeRemoteStaticCall<IServerAPI, Version>((api) => api.GetCCLVersion());
        }

        Task<MainMenuOptionLocator[]> IRemoteMainMenuAPI.GetAvailableMainMenuOptions()
        {
            return
                _rpcClient.MakeRemoteStaticCall<IMainMenuAPI, MainMenuOptionLocator[]>(
                    (api) => api.GetAvailableMainMenuOptions());
        }

        Task IRemoteMainMenuAPI.ClickMainMenuOption(MainMenuOptionLocator locator)
        {
            return
                _rpcClient.MakeRemoteStaticCall<IMainMenuAPI>(
                    (api) => api.ClickMainMenuOption(locator));
        }
    }
}
