using System;
using System.Collections.Generic;
using System.Text;

namespace DistributedCoordinator.ZkClient
{
    public class ZookeeperClientOptions
    {

        public ZookeeperClientOptions()
        {
            SessionTimeout = TimeSpan.FromSeconds(30);
            OperatingTimeout = TimeSpan.FromSeconds(60);
            SessionId = 0;
            SessionPasswd = null;
        }

        public ZookeeperClientOptions(string connectionString) : this()
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            ConnectionString = connectionString;
        }

        /// <summary>
        /// 连接字符串。
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// 执行ZooKeeper操作的等待时间。
        /// </summary>
        public TimeSpan OperatingTimeout { get; set; }

        /// <summary>
        /// zookeeper会话超时时间。
        /// </summary>
        public TimeSpan SessionTimeout { get; set; }

        /// <summary>
        /// 会话Id。
        /// </summary>
        public long SessionId { get; set; }

        /// <summary>
        /// 会话密码。
        /// </summary>
        public byte[] SessionPasswd { get; set; }
    }
}
