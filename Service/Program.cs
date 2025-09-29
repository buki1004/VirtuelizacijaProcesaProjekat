using System;
using System.ServiceModel;
using Common;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service
{
    class Program
    {
        static void Main(string[] args)
        {
            using (ServiceHost host = new ServiceHost(typeof(BatteryService)))
            {
                host.Open();
                Console.WriteLine("Service running. Press Enter to stop...");
                Console.ReadLine();
            }
        }
    }
}
