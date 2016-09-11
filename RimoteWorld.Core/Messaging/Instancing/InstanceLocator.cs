using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RimoteWorld.Core.Messaging.Instancing
{
    public abstract class InstanceLocator
    {
        public abstract Type InstanceType { get; }
    }

    public class InstanceLocator<T> : InstanceLocator
    {
        public override Type InstanceType
        {
            get { return typeof(T); }
        }
    }
}
