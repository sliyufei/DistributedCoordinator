using DistributedCoordinator.Handlers;
using DistributedCoordinator.Schedulers;
using DistributedCoordinator.Watchers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

            Console.WriteLine($"FairlockListener:path:{args.Path},Type:{args.Type},ThreadId:{Thread.CurrentThread.ManagedThreadId}");

           
            if (args.State == Event.KeeperState.SyncConnected)
            {
                if (!args.Path.Contains("fairlocks"))
                    return;

                if (args.Type == Event.EventType.NodeDeleted)
                    WorkerScheduler.Instance.Set(args.Path);
            }
            else if (args.State == Event.KeeperState.Expired)
            {
                Task.Run(() =>
                {
                    var handler = new FairlockHandler();
                    handler.CheckWaitDeletedNode();
                });

            }

        }



    }
}
