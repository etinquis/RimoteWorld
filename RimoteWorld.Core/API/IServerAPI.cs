using RimoteWorld.Core.API.UI;

namespace RimoteWorld.Core.API
{
    public enum GameState
    {
        Unknown,
        Initializing,
        MainMenu
    }

    public interface IServerAPI
    {
        GameState GetRimWorldGameState();
        Version GetRimoteWorldVersion();
        Version GetRimWorldVersion();
        Version GetCCLVersion();
    }
}
