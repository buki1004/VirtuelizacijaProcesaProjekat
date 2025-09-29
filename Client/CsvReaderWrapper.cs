using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client
{
    public class CsvReaderWrapper : IDisposable
    {
        private StreamReader _reader;
        private bool _disposed = false;

        public CsvReaderWrapper(string filePath)
        {
            _reader = new StreamReader(filePath);
        }

        public string ReadLine()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(CsvReaderWrapper));

            return _reader.ReadLine();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _reader?.Dispose();
                _disposed = true;
            }
        }
    }
}
