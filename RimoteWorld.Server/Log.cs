using System;

namespace RimoteWorld.Server
{
    internal interface ILogContext : IDisposable
    {
        string ContextString { get; }

        void Debug(string message);
        void Info(string message);
        void Warning(string message);
        void Error(string message);

        ILogContext CreateSubcontext(string context);
    }

    internal static class Log
    {
        private const string DEBUG      = "DBG";
        private const string INFO       = "NFO";
        private const string WARNING    = "WRN";
        private const string ERROR      = "ERR";

        private const string MessageFormat = "RimoteWorld [{0}] [{1}] [{2}] {3}";
        private static LogContext DefaultContext = new LogContext("");

        private static string FormatMessage(ILogContext context, string verbosity, string message)
        {
            return string.Format(MessageFormat, verbosity, DateTime.Now.ToShortTimeString(), context.ContextString, message);
        }

        private class LogContext : ILogContext
        {
            private LogContext _parentContext = null;
            private string _contextName;

            public string ContextString
            {
                get { return string.Format("{0}::{1}", _parentContext?.ContextString, _contextName); }
            }

            public LogContext(string contextName) : this(null, contextName)
            {
                
            }

            private LogContext(LogContext parentContext, string contextName)
            {
                _parentContext = parentContext;
                _contextName = contextName;

                Debug("Starting");
            }

            public void Dispose()
            {
                Debug("Ending");
            }

            public ILogContext CreateSubcontext(string contextName)
            {
                return new LogContext(this, contextName);
            }

            public void Debug(string message)
            {
                Log.Debug(this, message);
            }

            public void Info(string message)
            {
                Log.Info(this, message);
            }

            public void Warning(string message)
            {
                Log.Warning(this, message);
            }

            public void Error(string message)
            {
                Log.Error(this, message);
            }

        }

        #region Internal Methods
        private static void Debug(ILogContext context, string message)
        {
            if (Verse.Prefs.LogVerbose)
            {
                Verse.Log.Message(FormatMessage(context, DEBUG, message));
            }
        }

        private static void Info(ILogContext context, string message)
        {
            Verse.Log.Message(FormatMessage(context, INFO, message));
        }

        private static void Warning(ILogContext context, string message)
        {
            Verse.Log.Warning(FormatMessage(context, WARNING, message));
        }

        private static void Error(ILogContext context, string message)
        {
            Verse.Log.Error(FormatMessage(context, ERROR, message));
        }
        #endregion

        public static void Debug(string message)
        {
            DefaultContext.Debug(message);
        }

        public static void Info(string message)
        {
            DefaultContext.Info(message);
        }

        public static void Warning(string message)
        {
            DefaultContext.Warning(message);
        }

        public static void Error(string message)
        {
            DefaultContext.Error(message);
        }

        public static void Error(string message, Exception ex)
        {
            DefaultContext.Error(string.Format("{0} (ex: {1})", message, ex));
        }

        public static ILogContext CreateContext(string context)
        {
            return DefaultContext.CreateSubcontext(context);
        }
    }
}
