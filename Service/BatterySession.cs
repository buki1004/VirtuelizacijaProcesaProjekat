using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service
{
    public class BatterySession : IDisposable
    {
        public StreamWriter SessionFileWriter { get; set; }
        public StreamWriter RejectsWriter { get; set; }
        public int LastRowIndex { get; set; } = -1;

        public double LastT { get; set; } = double.NaN;

        public string BatteryId { get; set; }

        public int SoC { get; set; }

        public void Dispose()
        {
            try { SessionFileWriter?.Dispose(); } catch { }
            try { RejectsWriter?.Dispose(); } catch { }
        }
    }
}
