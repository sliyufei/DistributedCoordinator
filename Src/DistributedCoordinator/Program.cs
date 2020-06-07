using DistributedCoordinator.Model;
using System;
using System.Threading;

namespace DistributedCoordinator
{
    class Program
    {
        static void Main(string[] args)
        {

            Coordinator.Instance.Init();

            Thread t = new Thread(() =>
              {
                  var fairlockOptions = new FairlockOptions
                  {
                      TenantId = 101200,
                      Key = "ApplicantCheck",
                      Timeout = TimeSpan.FromMinutes(1)
                  };

                  Fairlock.Enter(fairlockOptions);
                  Do();
                  Fairlock.Exit();
              });
            t.Start();


            Console.ReadLine();
        }

        static void Do()
        {
            Console.WriteLine($"ThreadId:{Thread.CurrentThread.ManagedThreadId} start do...");
            Thread.Sleep(1000 * 5);
            Console.WriteLine("end do...");
        }
    }
}
