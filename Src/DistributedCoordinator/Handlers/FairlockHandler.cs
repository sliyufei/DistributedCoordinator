using DistributedCoordinator.Model;
using DistributedCoordinator.Schedulers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DistributedCoordinator.Handlers
{
    public class FairlockHandler
    {
        private const string rootPath = "/recruit";

        private const string lockType = "fairlocks";

        private string parentPath = string.Empty;

        private string currentNode = string.Empty;

        public static ThreadLocal<string> currentRegisterPath = new ThreadLocal<string>();

        private FairlockOptions _options;
        public FairlockHandler(FairlockOptions options)
        {
            _options = options;
        }

        public FairlockHandler()
        {

        }

        public void ApplyLock()
        {
            var sessionId = Guid.NewGuid();

            var lockPath = GetLockPath(_options);

            //注册锁路径
            var registerPath = RegisterPath(lockPath);

            Console.WriteLine($"ApplyLock:{registerPath},ThreadId:{Thread.CurrentThread.ManagedThreadId}");

            currentRegisterPath.Value = registerPath;

            //判断自己是否为最小节点
            if (!CheckCurrentNodeIsMinimumNode(parentPath, registerPath))
            {
                //监听比自己小的节点
                var sequentialNodes = GetChildrenSequentialNodeEntries(parentPath).OrderBy(o => o.Order).ToList();
                var previousNode = PreviousNode(sequentialNodes, currentNode);

                if (WatchNode(parentPath, previousNode))
                {
                    var identity = Guid.NewGuid();
                    var watchPath = $"{parentPath}/{previousNode}";

                    Console.WriteLine($"watchNodePath:{watchPath},ThreadId:{Thread.CurrentThread.ManagedThreadId}");
                    WorkerScheduler.Instance.AddWorker(watchPath);

                    Coordinator.WatcherCachePool.TryAdd(identity, watchPath);
                    var receiveSignal = WorkerScheduler.Instance.Wait(watchPath, _options.Timeout);

                    if (!receiveSignal)
                    {
                        throw new TimeoutException("获取锁超时");
                    }
                    else
                    {
                        Coordinator.WatcherCachePool.TryRemove(identity, out watchPath);
                        Console.WriteLine($"TryRemove:{watchPath}");
                    }    


                }

            }



        }

        public void ReleaseLock()
        {
            Coordinator.Instance.DeleteNode(currentRegisterPath.Value);
            Console.WriteLine($"ReleaseLock:{currentRegisterPath.Value},ThreadId:{Thread.CurrentThread.ManagedThreadId}");
        }


        private string GetLockPath(FairlockOptions options)
        {
            // /recruit/fairlocks/{key}/{tenantId}/00000001
            return $"{rootPath}/{lockType}/{options.Key}/{options.TenantId}/";
        }

        private string RegisterPath(string path)
        {
            parentPath = GetParentNode(path);

            //判断要创建的永久节点
            CheckPersistentPath(parentPath);

            //申请锁
            return Coordinator.Instance.CreateEphemeralSequentialNode(new NodeEntry { Path = path });
        }

        private string GetParentNode(string path)
        {
            var index = path.LastIndexOf("/");
            return path.Substring(0, index);
        }

        private void CheckPersistentPath(string parentPath)
        {
            List<NodeEntry> nodeEntryList = GetNodeEntryList(parentPath);

            if (nodeEntryList != null && nodeEntryList.Count > 0)
            {
                //反转集合让父节点先创建
                nodeEntryList.Reverse();
                Coordinator.Instance.CreatePersistentNode(nodeEntryList);
            }
        }

        private List<NodeEntry> GetNodeEntryList(string path, List<NodeEntry> nodeEntryList = null)
        {
            if (Coordinator.Instance.Exists(path))
            {
                return nodeEntryList;
            }
            else
            {
                if (nodeEntryList == null)
                    nodeEntryList = new List<NodeEntry>();

                nodeEntryList.Add(new NodeEntry { Path = path });
                var endIndex = path.LastIndexOf("/");
                if (endIndex > 0)
                {
                    var tmpPath = path.Substring(0, endIndex);
                    GetNodeEntryList(tmpPath, nodeEntryList);
                }
                else
                {
                    return nodeEntryList;
                }
            }
            return nodeEntryList;
        }

        private IEnumerable<NodeEntry> GetChildrenSequentialNodeEntries(string path)
        {
            var children = Coordinator.Instance.GetChildren(path);
            foreach (var child in children.Children)
            {
                yield return new NodeEntry { Path = child, Order = long.Parse(child) };

            }
        }

        private bool CheckCurrentNodeIsMinimumNode(string parentPath, string path)
        {
            var result = false;
            currentNode = GetCurrentNode(path);

            if (!string.IsNullOrEmpty(currentNode))
            {
                var nodeEntries = GetChildrenSequentialNodeEntries(parentPath).OrderBy(o => o.Order).ToList();
              
                if (nodeEntries != null && nodeEntries.Count > 0)
                {
                    var firstNodePath = nodeEntries.FirstOrDefault().Path;
                    if (currentNode == firstNodePath)
                        result = true;
                }
                else
                {
                    //一个子节点都没有那么自己就是第一个节点了
                    result = true;
                }
            }
            return result;
        }

        private string GetCurrentNode(string path)
        {
            var index = path.LastIndexOf("/");
            if (index > -1)
                return path.Substring(index + 1, path.Length - index - 1);

            return string.Empty;

        }

        /// <summary>
        /// 未监听上,也就是自己变成了首节点,需要返回false 
        /// </summary>
        /// <param name="parentPath"></param>
        /// <param name="nodePath"></param>
        /// <returns></returns>
        private bool WatchNode(string parentPath, string node)
        {
            while (!Coordinator.Instance.Exists($"{parentPath}/{node}", true))
            {
                var nodeEntries = GetChildrenSequentialNodeEntries(parentPath).OrderBy(o => o.Order).ToList();

                var result = PreviousNode(nodeEntries, node);

                if (result == "-1")
                    return false;
                else
                {
                    node = result;
                }
            }
            return true;
        }

        private string PreviousNode(List<NodeEntry> nodeEntries, string currentNodePath)
        {
            //自己变成第一个节点时需要返回-1
            var currentNodeIndex = nodeEntries.FindIndex(o => o.Path == currentNodePath);
            return currentNodeIndex > 0 ? nodeEntries[currentNodeIndex - 1].Path : "-1";
        }

        public void CheckWaitDeletedNode()
        {
            foreach (var watcherCache in Coordinator.WatcherCachePool)
            {
                var currentPath = watcherCache.Value;
                var currentParentPath= GetParentNode(currentPath);

                if (CheckCurrentNodeIsMinimumNode(currentParentPath, currentPath))
                {
                    WorkerScheduler.Instance.Set(currentPath);
                    Console.WriteLine($"CheckWaitDeletedNode:{currentPath}");
                }
                    
            }
        }

        public void GC()
        {
            // /recruit/fairlocks/{key}/{tenantId}/00000001

            var path = $"{rootPath}/{ lockType}";
            var businessChildren = Coordinator.Instance.GetChildren(path);

            foreach (var businessChildPath in businessChildren.Children)
            {
                var tenantChildren = Coordinator.Instance.GetChildren(businessChildPath);

                foreach (var tenantChildPath in tenantChildren.Children)
                {
                    var children = Coordinator.Instance.GetChildren(tenantChildPath);

                    if (children.Children == null || children.Children.Count <= 0)
                    {
                        Coordinator.Instance.DeleteNode(tenantChildPath);
                    }
                }

            }

        }
    }
}
