using org.apache.zookeeper;
using org.apache.zookeeper.data;
using System;
using System.Collections.Generic;
using System.Text;

namespace DistributedCoordinator.ZkClient
{
   public interface IZookeeperClient
    {
        /// <summary>
        /// 具体的ZooKeeper连接。
        /// </summary>
        ZooKeeper ZooKeeper { get; }

        /// <summary>
        /// 客户端选项。
        /// </summary>
        ZookeeperClientOptions Options { get; }


        string Create(string path, byte[] data, List<ACL> acls, CreateMode createMode);


        Stat Exists(string path, bool watch = false);

        ChildrenResult GetChildren(string path, bool watch = false);

        bool Delete(string path, int version = -1);


    }
}
