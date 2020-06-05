using DistributedCoordinator.Handlers;
using DistributedCoordinator.Model;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace DistributedCoordinator
{
    public class Fairlock
    {
        public static void Enter(FairlockOptions options)
        {
            //默认超时60s
            if ((int)options.Timeout.TotalMilliseconds == 0)
                options.Timeout = TimeSpan.FromMinutes(1);

            var handler = new FairlockHandler(options);
            handler.ApplyLock();

        }

        public static void Exit()
        {
            var handler = new FairlockHandler();
            handler.ReleaseLock();
        }
    }
}
