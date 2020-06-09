using DistributedCoordinator.Schedulers;
using DistributedCoordinator.Watchers;
using org.apache.zookeeper;
using org.apache.zookeeper.data;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DistributedCoordinator.ZkClient
{
    public class ZookeeperClient : Watcher, IZookeeperClient
    {
        public event EventHandler<WatcherArgs> WatcherEvent;

        public ZooKeeper ZooKeeper { get; private set; }

        public ZookeeperClientOptions Options { get; }

        public ZookeeperClient(ZookeeperClientOptions options)
        {
            Options = options;
            ZooKeeper = CreateZooKeeper();
        }

        private ZooKeeper CreateZooKeeper()
        {
            return new ZooKeeper(Options.ConnectionString, (int)Options.SessionTimeout.TotalMilliseconds, this);

        }

     
        public string Create(string path, byte[] data, List<ACL> acls, CreateMode createMode)
        {
            return ZooKeeper.createAsync(path, data, acls, createMode).Result;
        }

        public bool Delete(string path, int version = -1)
        {
             ZooKeeper.deleteAsync(path, version).Wait();
            return true;
        }

        public Stat Exists(string path, bool watch = false)
        {
            return ZooKeeper.existsAsync(path, watch).Result;
        }

        public ChildrenResult GetChildren(string path, bool watch = false)
        {
            Console.WriteLine($"GetChildren,:{Thread.CurrentThread.ManagedThreadId}");
            return ZooKeeper.getChildrenAsync(path, watch).Result;
        }

        public void Close()
        {
             ZooKeeper.closeAsync().Wait();
        }

        public override Task process(WatchedEvent @event)
        {
            var state = @event.getState();
            var type = @event.get_Type();
            var path = @event.getPath();

            Console.WriteLine($"state:{state},type:{type},path:{path},Id:{Thread.CurrentThread.ManagedThreadId}");

            if (state == Event.KeeperState.Expired)
            {
                Coordinator.Instance.ReConnect();
            }
            else if (state == Event.KeeperState.SyncConnected)
            {
                CoordinatorScheduler.Instance.Set();
                WatcherEvent(this, new WatcherArgs { Type = type, Path = path });
            }

            return Task.CompletedTask;
        }
    }
}
