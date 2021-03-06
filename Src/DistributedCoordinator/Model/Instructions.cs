﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace DistributedCoordinator.Model
{
    public class Instruction
    {
        public Guid Identity { get; set; }
        public AutoResetEvent Signal { get; set; }

        public Func<object> Command { get; set; }

    }
}
