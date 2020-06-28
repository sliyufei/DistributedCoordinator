# DistributedCoordinator
使用.NetCore 3.1和zk实现的分布式公平锁

## 使用方法
首先需要init一下协调者,请确保如下代码在当前进程内只实例化一次
``` C#
 Coordinator.Instance.Init();
```
然后使用如下方法包裹住你的同步代码块即可

``` C#
var fairlockOptions = new FairlockOptions
     {
          TenantId = 101200,
          Key = "ApplicantCheck",
          Timeout = TimeSpan.FromMinutes(1)
      };
      
 Fairlock.Enter(fairlockOptions);
 //ToDo();
 Fairlock.Exit();
```

## 回收顺序节点
调用 FairlockHandler.GC()即可
