using RimoteWorld.Core.Messaging.Instancing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RimoteWorld.Core
{
    public class Message
    {
        public ulong ID { get; set; }
    }

    public abstract class RequestMessage : Message
    {
        public abstract Type APIType { get; }
        public abstract string TypeName { get; }
        public string RemoteCall { get; set; }
    }

    public class RequestMessage<TAPI> : RequestMessage
    {
        public override Type APIType
        {
            get { return typeof(TAPI); }
        }

        public override string TypeName
        {
            get { return APIType.Name; }
        }

        public InstanceLocator<TAPI> InstanceLocator { get; set; }

        public override string ToString()
        {
            return string.Format("{0} [ID: {1}, TypeName: {2}, RemoteCall: {3}]", base.ToString(), ID, TypeName,
                RemoteCall);
        }
    }

    public abstract class RequestMessageWithArguments<TAPI> : RequestMessage<TAPI>
    {
        public abstract Type[] ArgumentTypes { get; }
        public abstract object[] Arguments { get; }
    }

    public class RequestMessageWithArguments<TAPI, TParam1> : RequestMessageWithArguments<TAPI>
    {
        public TParam1 Argument1 { get; set; }

        public override object[] Arguments
        {
            get { return new object[] {Argument1}; }
        }

        public override Type[] ArgumentTypes
        {
            get { return new Type[] {typeof(TParam1)}; }
        }
    }

    public class RequestMessageWithArguments<TAPI, TParam1, TParam2> : RequestMessageWithArguments<TAPI, TParam1>
    {
        public TParam2 Argument2 { get; set; }

        public override object[] Arguments
        {
            get { return base.Arguments.Concat(new object[] {Argument2}).ToArray(); }
        }

        public override Type[] ArgumentTypes
        {
            get { return base.ArgumentTypes.Concat(new Type[] { typeof(TParam2) }).ToArray(); }
        }
    }

    public abstract class ResponseMessage : Message
    {
        public Message OriginalMessage { get; set; }
    }

    public class ResponseMessage<TAPI> : ResponseMessage
    {
        public RequestMessage<TAPI> TypedOriginalRequest
        {
            get { return (RequestMessage<TAPI>)OriginalMessage; }
        }
    }

    public class ResponseWithErrorMessage : ResponseMessage
    {
        public string ErrorMessage { get; set; }
    }

    public class ResponseWithResultMessage<TAPI, TResult> : ResponseMessage<TAPI>
    {
        public ResponseWithResultMessage()
        {
            
        }

        public ResponseWithResultMessage(TResult result)
        {
            Result = result;
        }

        public TResult Result { get; set; }
    }
}
