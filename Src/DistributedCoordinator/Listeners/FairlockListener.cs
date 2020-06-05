using DistributedCoordinator.Schedulers;
using DistributedCoordinator.Watchers;
using System;
using System.Collections.Generic;
using System.Text;
using static org.apache.zookeeper.Watcher;

namespace DistributedCoordinator.Listeners
{
    public class FairlockListener
    {
        public static FairlockListener Instance { get; private set; }
        static FairlockListener()
        {
            Instance = new FairlockListener();
        }

        public void Process(object sender, WatcherArgs args)
        {
            if (string.IsNullOrEmpty(args.Path))
                return;

            Console.WriteLine($"FairlockListener:path:{args.Path},Type:{args.Type}");

            if (!args.Path.Contains("fairlocks"))
                return;

            if (args.Type == Event.EventType.NodeDeleted)
                WorkerScheduler.Instance.Set(args.Path);

        }
    }
}
