using RimoteWorld.Core.API.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimoteWorld.Core.Messaging.Instancing.UI;
using CommunityCoreLibrary;
using Verse;

namespace RimoteWorld.Server.API.UI
{
    class MainMenuAPI : IMainMenuAPI
    {
        private List<ListableOption_MainMenu> AvailableOptions
        {
            get
            {
                var mainOptions = new List<ListableOption_MainMenu>();
                var currentMainMenuDefs = MainMenuDrawer_Extensions.CurrentMainMenuDefs(MainMenuDrawer_Extensions.AnyMapFiles);

                foreach (var menu in currentMainMenuDefs)
                {
                    mainOptions.Add(new ListableOption_MainMenu(menu) {label = menu.Label});
                }

                return mainOptions;
            }
        }

        public void ClickMainMenuOption(MainMenuOptionLocator locator)
        {
            var option = AvailableOptions.First(opt => opt.label == locator.MenuOptionText);
            option.action();
        }

        public MainMenuOptionLocator[] GetAvailableMainMenuOptions()
        {
            return AvailableOptions.Select(option =>
            {
                return new MainMenuOptionLocator()
                {
                    MenuOptionText = option.label
                };
            }).ToArray();
        }
    }
}
