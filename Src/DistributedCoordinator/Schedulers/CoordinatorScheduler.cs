using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace DistributedCoordinator.Schedulers
{
    public class CoordinatorScheduler
    {
        private static AutoResetEvent coordinatorNotifyEvent;

        public static CoordinatorScheduler Instance { get; private set; }

        static CoordinatorScheduler()
        {
            coordinatorNotifyEvent = new AutoResetEvent(false);
            Instance = new CoordinatorScheduler();
        }

        public  bool Wait()
        {
            return coordinatorNotifyEvent.WaitOne();
        }

        public  bool Wait(int timeout)
        {
            return coordinatorNotifyEvent.WaitOne(timeout);
        }

        public bool Wait(TimeSpan timeout)
        {
            return coordinatorNotifyEvent.WaitOne(timeout);
        }

        public  void Set()
        {
            coordinatorNotifyEvent.Set();
        }
    }
}
