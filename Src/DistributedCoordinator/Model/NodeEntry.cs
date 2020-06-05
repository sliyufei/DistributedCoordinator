using System;
using System.Collections.Generic;
using System.Text;

namespace DistributedCoordinator.Model
{
    public class NodeEntry
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public long Order { get; set; }
    }
}
