using System;
using System.Collections.Generic;
using System.Text;

namespace DistributedCoordinator.Model
{
    public class FairlockOptions
    {
        public int TenantId { get; set; }
        public string Key { get; set; }
        public TimeSpan Timeout { get; set; }
    }
}
