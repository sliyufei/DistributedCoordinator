using System;
using System.Collections.Generic;
using System.Text;
using static org.apache.zookeeper.Watcher;

namespace DistributedCoordinator.Watchers
{
    public class WatcherArgs : EventArgs
    {
        public Event.KeeperState State { get; set; }
        public Event.EventType Type { get; set; }
        public string Path { get; set; }
    }
}
