using RimoteWorld.Core.Messaging.Instancing.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RimoteWorld.Client.API.Remote
{
    public interface IRemoteMainMenuAPI
    {
        Task<MainMenuOptionLocator[]> GetAvailableMainMenuOptions();
        Task ClickMainMenuOption(MainMenuOptionLocator locator);
    }
}
