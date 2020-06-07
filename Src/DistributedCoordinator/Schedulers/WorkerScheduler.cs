using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace DistributedCoordinator.Schedulers
{
    public class WorkerScheduler
    {
        private static ConcurrentDictionary<string, AutoResetEvent> workerNotifyEvents;

        public static WorkerScheduler Instance { get; private set; }

        static WorkerScheduler()
        {
            workerNotifyEvents = new ConcurrentDictionary<string, AutoResetEvent>();
            Instance = new WorkerScheduler();
        }

        internal void AddWorker(string key)
        {
            var workerNotifyEvent = new AutoResetEvent(false);
            var result = workerNotifyEvents.TryAdd(key, workerNotifyEvent);
            if (!result)
                throw new ArgumentException();
        }

        internal bool Wait(string key, TimeSpan timeout)
        {
            AutoResetEvent workerNotifyEvent;
            var result = workerNotifyEvents.TryGetValue(key, out workerNotifyEvent);
            if (result)
                return workerNotifyEvent.WaitOne(timeout);
            else
                throw new ArgumentException();
           
        }

        internal void Set(string key)
        {
            AutoResetEvent workerNotifyEvent;
            var result = workerNotifyEvents.TryGetValue(key, out workerNotifyEvent);
            if (result)
                workerNotifyEvent.Set();

        }
    }
}
