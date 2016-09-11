using RimoteWorld.Core.API.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RimoteWorld.Core.Messaging.Instancing.UI
{
    public class MainMenuOptionLocator  : InstanceLocator<IMainMenuOption>
    {
        public string MenuOptionText { get; set; }

        public override string ToString()
        {
            return string.Format("{0} [MenuOptionText: {1}]", base.ToString(), MenuOptionText);
        }
    }
}
