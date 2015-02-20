using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ServiceHosting;

namespace DemoProject
{
    class Program
    {
        static void Main(string[] args)
        {
            Bootstrap.Jumpstart<MySimpleService>("DemoService", "Demo Service", "This is a simple service that does nothing interesting, at all.", args);
        }
    }
}
