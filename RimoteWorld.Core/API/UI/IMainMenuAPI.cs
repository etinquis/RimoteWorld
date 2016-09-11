using RimoteWorld.Core.Messaging.Instancing.UI;

namespace RimoteWorld.Core.API.UI
{
    public interface IMainMenuAPI
    {
        MainMenuOptionLocator[] GetAvailableMainMenuOptions();
        void ClickMainMenuOption(MainMenuOptionLocator locator);
    }
}
