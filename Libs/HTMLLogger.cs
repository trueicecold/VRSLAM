using Photino.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VRSLAM.Libs
{
    public class HTMLLogger
    {
        private static int LOG_ID = 0;

        public static int Log(string message, int logId = -1)
        {
            if (logId == -1)
            {
                logId = GetNextLogId();
            }
            Shared.Window.SendWebMessage(JSON.Stringify(new
            {
                type = "html_log",
                message = message,
                logId = logId
            }));
            return logId;
        }

        static int GetNextLogId()
        {
            LOG_ID++;
            return LOG_ID;
        }
    }
}