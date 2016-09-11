using RimoteWorld.Core.API.UI;

namespace RimoteWorld.Core.API
{
    public interface IServerAPI
    {
        Version GetRimoteWorldVersion();
        Version GetRimWorldVersion();
        Version GetCCLVersion();
    }
}
