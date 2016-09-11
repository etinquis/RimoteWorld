using RimoteWorld.Core;
using System.Threading.Tasks;

namespace RimoteWorld.Client
{
    public interface IRemoteServerAPI
    {
        Task<Version> GetRimoteWorldVersion();
        Task<Version> GetRimWorldVersion();
        Task<Version> GetCCLVersion();
    }
}
