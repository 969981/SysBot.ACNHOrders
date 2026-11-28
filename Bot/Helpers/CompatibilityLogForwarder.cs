using System;
using SysBot.Base;

namespace SysBot.ACNHOrders
{
    public static class CompatibilityLogForwarder
    {
#if PLATFORM_CANDIDATE
        private sealed class Forwarder(Action<string,string> callback) : ILogForwarder
        {
            public void Forward(string message,string identity)=>callback(message,identity);
        }
#endif
        public static void Add(Action<string,string> callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
#if PLATFORM_CANDIDATE
            LogUtil.Forwarders.Add(new Forwarder(callback));
#else
            LogUtil.Forwarders.Add(callback);
#endif
        }
    }
}
