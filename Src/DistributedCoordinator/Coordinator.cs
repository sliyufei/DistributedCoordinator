using DistributedCoordinator.Listeners;
using DistributedCoordinator.Model;
using DistributedCoordinator.Schedulers;
using DistributedCoordinator.ZkClient;
using org.apache.zookeeper;
using org.apache.zookeeper.data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using static org.apache.zookeeper.Watcher;

namespace DistributedCoordinator
{
    public class Coordinator
    {
        private ZookeeperClient zkClient;

        private ZookeeperClientOptions zkOptions;

        /// <summary>
        /// 指令缓冲区
        /// </summary>
        private ConcurrentQueue<Instruction> instructionsBuffer = new ConcurrentQueue<Instruction>();

        /// <summary>
        /// 指令结果
        /// </summary>
        private ConcurrentDictionary<Guid, object> instructionsResult = new ConcurrentDictionary<Guid, object>();

        public static ConcurrentDictionary<Guid, string> WatcherCachePool { get; set; } = new ConcurrentDictionary<Guid, string>();

        /// <summary>
        /// 执行指令的信号量
        /// </summary>
        private AutoResetEvent executeSignal = new AutoResetEvent(false);

        private readonly object reConnectLock = new object();

        public static bool IsFirstConnected { get; set; } = true;

        public static Coordinator Instance { get; private set; }
        static Coordinator()
        {
            Instance = new Coordinator();
        }

        public void Init()
        {

            zkOptions = new ZookeeperClientOptions
            {
                ConnectionString = "47.93.242.4:2181"
            };

            zkClient = new ZookeeperClient(zkOptions);

            //注册事件
            RegisterEvent();

            //初始化组件
            InitComponents();

            Console.WriteLine("zooKeeper wait..");
            CoordinatorScheduler.Instance.Wait(zkOptions.OperatingTimeout);

        }

        private void InitComponents()
        {

            Thread threadExecute = new Thread(() =>
            {
                ExecuteInstruction();
            });

            threadExecute.Priority = ThreadPriority.Highest;
            threadExecute.Start();
        }

        private void RegisterEvent()
        {
            zkClient.WatcherEvent += FairlockListener.Instance.Process;

        }

        public void ReConnect()
        {
            if (!Monitor.TryEnter(reConnectLock, zkOptions.OperatingTimeout))
                return;
            try
            {
                if (zkClient != null)
                    Close();

                zkClient = new ZookeeperClient(zkOptions);

                Console.WriteLine($"ReConnect:{Thread.CurrentThread.ManagedThreadId},options:{Newtonsoft.Json.JsonConvert.SerializeObject(zkOptions)}");


            }
            finally
            {
                Monitor.Exit(reConnectLock);
            }

        }


        public object AddInstruction(Func<object> command)
        {
            var identity = Guid.NewGuid();
            var signal = new AutoResetEvent(false);
            instructionsBuffer.Enqueue(new Instruction
            {
                Identity = identity,
                Signal = signal,
                Command = command
            });

            Console.WriteLine($"AddInstruction:{Newtonsoft.Json.JsonConvert.SerializeObject(command.Target)}");
            //通知执行线程
            executeSignal.Set();

            var receiveSignal = signal.WaitOne(zkOptions.OperatingTimeout);
            if (!receiveSignal)
                throw new TimeoutException("等待执行指令超时");

            object result = null;
            if (!instructionsResult.TryRemove(identity, out result))
                throw new ArgumentException("找不到指定结果");

            return result;
        }

        private void ExecuteInstruction()
        {

            while (true)
            {
                executeSignal.WaitOne();

                while (true)
                {

                    Instruction instruction = new Instruction();

                    if (!instructionsBuffer.TryDequeue(out instruction))
                        break;

                    try
                    {
                        var result = RetryUntilConnected(instruction.Command);
                        instructionsResult.TryAdd(instruction.Identity, result);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.StackTrace);
                    }
                    finally
                    {
                        instruction?.Signal.Set();
                    }

                }

            }

        }

        private object RetryUntilConnected(Func<object> command)
        {
            while (true)
            {
                KeeperException keeperException = null;
                try
                {
                    return command();
                }
                catch (AggregateException ex)
                {
                    ex.Flatten().Handle(e =>
                    {
                        if (e is KeeperException.ConnectionLossException)
                        {
                            keeperException = (KeeperException.ConnectionLossException)e;
                            return true;
                        }
                        else if (e is KeeperException.SessionExpiredException)
                        {
                            keeperException = (KeeperException.SessionExpiredException)e;
                            return true;
                        }
                        else if (e is KeeperException.NoNodeException)
                        {
                            keeperException = (KeeperException.NoNodeException)e;
                            Console.WriteLine(e.ToString());
                            return true;
                        }
                        else if (e is KeeperException.NodeExistsException)
                        {
                            keeperException = (KeeperException.NodeExistsException)e;
                            Console.WriteLine(e.ToString());
                            return true;
                        }

                        return false;
                    });
                }

                switch (keeperException)
                {
                    case KeeperException ex when ex is KeeperException.ConnectionLossException:
                        WaitForSyncConnected();
                        break;
                    case KeeperException ex when ex is KeeperException.SessionExpiredException:
                        WaitForSyncConnected();
                        break;
                    case KeeperException ex when ex is KeeperException.NoNodeException:
                        return null;
                    case KeeperException ex when ex is KeeperException.NodeExistsException:
                        return null;

                }

                Thread.Sleep(1000);

            }
        }

        private void WaitForSyncConnected()
        {

            Console.WriteLine($"before.....Id：{ Thread.CurrentThread.ManagedThreadId}");
            CoordinatorScheduler.Instance.Wait(zkOptions.OperatingTimeout);
            Console.WriteLine("after");
        }

        public void ReRegisterWatcher()
        {
            foreach (var watcherCache in WatcherCachePool)
            {
                zkClient.Exists(watcherCache.Value, true);
                Console.WriteLine($"ReRegisterWatcher:{watcherCache.Value}");
            }

        }

        public void CreatePersistentNode(List<NodeEntry> nodeEntries)
        {
            foreach (var nodeEntry in nodeEntries)
            {
                AddInstruction(() => { return zkClient.Create(nodeEntry.Path, null, ZooDefs.Ids.OPEN_ACL_UNSAFE, CreateMode.PERSISTENT); });
            }

        }

        public string CreatePersistentNode(NodeEntry nodeEntry)
        {
            var result = AddInstruction(() => { return zkClient.Create(nodeEntry.Path, null, ZooDefs.Ids.OPEN_ACL_UNSAFE, CreateMode.PERSISTENT); });
            return result != null ? (string)result : default(string);
        }

        public string CreateEphemeralSequentialNode(NodeEntry nodeEntry)
        {
            var result = AddInstruction(() => { return zkClient.Create(nodeEntry.Path, null, ZooDefs.Ids.OPEN_ACL_UNSAFE, CreateMode.EPHEMERAL_SEQUENTIAL); });
            return result != null ? (string)result : default(string);
        }

        public ChildrenResult GetChildren(string path, bool watch = false)
        {
            var result = AddInstruction(() => { return zkClient.GetChildren(path, watch); });
            return result != null ? (ChildrenResult)result : null;
        }

        public bool Exists(string path, bool watch = false)
        {
            var result = AddInstruction(() => { return zkClient.Exists(path, watch) != null; });
            return result != null ? (bool)result : false;
        }

        public void DeleteNode(string path)
        {
            AddInstruction(() => { return zkClient.Delete(path); });
        }

        public void Close()
        {
            zkClient.Close();
        }



    }
}
