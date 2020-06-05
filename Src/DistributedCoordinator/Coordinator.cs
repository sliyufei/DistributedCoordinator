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

        private Event.KeeperState _currentState;

        /// <summary>
        /// 指令缓冲区
        /// </summary>
        public static ConcurrentQueue<Instruction> instructionsBuffer = new ConcurrentQueue<Instruction>();

        /// <summary>
        /// 当前指令处理结果
        /// </summary>
        private static object currentCommandResult;

        /// <summary>
        /// 执行指令的信号量
        /// </summary>
        private AutoResetEvent executeSignal = new AutoResetEvent(false);

        public static Coordinator Instance { get; private set; }
        static Coordinator()
        {
            Instance = new Coordinator();
        }

        public void Init()
        {
            //注册事件
            RegisterEvent();

            //初始化组件
            InitComponents();

            zkOptions = new ZookeeperClientOptions
            {
                ConnectionString = ""
            };

            zkClient = new ZookeeperClient(zkOptions);

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

            try
            {

                zkOptions.SessionId = zkClient.GetSessionId();
                zkOptions.SessionPasswd = zkClient.GetSessionPasswd();

                Console.WriteLine($"ReConnect:{Thread.CurrentThread.ManagedThreadId},options:{Newtonsoft.Json.JsonConvert.SerializeObject(zkOptions)}");

                zkClient = new ZookeeperClient(zkOptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
            }

        }

        public void SetCurrentState(Event.KeeperState state)
        {
            _currentState = state;
        }

        public object AddInstruction(Func<object> command)
        {
            var signal = new AutoResetEvent(false);
            instructionsBuffer.Enqueue(new Instruction
            {
                Signal = signal,
                Command = command
            });

            //通知执行线程
            executeSignal.Set();

            signal.WaitOne(zkOptions.OperatingTimeout);

            return currentCommandResult;
        }

        private void ExecuteInstruction()
        {

            while (true)
            {
                executeSignal.WaitOne();

                while (true)
                {
                    Instruction instruction = new Instruction();
                    try
                    {

                        if (!instructionsBuffer.TryDequeue(out instruction))
                            continue;

                        currentCommandResult = RetryUntilConnected(instruction.Command);

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.StackTrace);
                    }
                    finally
                    {
                        instruction.Signal.Set();
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
                            return true;
                        }
                        else if (e is KeeperException.NodeExistsException)
                        {
                            keeperException = (KeeperException.NodeExistsException)e;
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

            while (_currentState != Event.KeeperState.SyncConnected)
            {
                Console.WriteLine($"before.state:{ _currentState}....Id：{ Thread.CurrentThread.ManagedThreadId}");
                CoordinatorScheduler.Instance.Wait(zkOptions.OperatingTimeout);
                Console.WriteLine("after" + _currentState);
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
