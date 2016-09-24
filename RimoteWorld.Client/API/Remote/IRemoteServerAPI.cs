using RimoteWorld.Core;
using RimoteWorld.Core.API;
using System.Threading.Tasks;

namespace RimoteWorld.Client
{
    public interface IRemoteServerAPI
    {
        Task<GameState> GetRimWorldGameState();
        Task<Version> GetRimoteWorldVersion();
        Task<Version> GetRimWorldVersion();
        Task<Version> GetCCLVersion();
    }
}
