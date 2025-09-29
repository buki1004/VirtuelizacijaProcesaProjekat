using Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Service
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall)]
    public class BatteryService : IBatteryService
    {
        private static readonly string BaseDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");

        // Track active sessions
        private static readonly ConcurrentDictionary<string, SessionContext> _sessions = new ConcurrentDictionary<string, SessionContext>();

        public void StartSession(EisMeta meta)
        {
            if (meta == null || string.IsNullOrEmpty(meta.SessionId))
                throw new FaultException<DataFormatFault>(new DataFormatFault { Message = "Invalid metadata" });

            string sessionFolder = Path.Combine(BaseDataPath, meta.BatteryId, meta.TestId, $"{meta.SoC}%");
            Directory.CreateDirectory(sessionFolder);

            string sessionFile = Path.Combine(sessionFolder, "session.csv");
            string rejectsFile = Path.Combine(sessionFolder, "rejects.csv");

            // Create files with headers if not exist
            if (!File.Exists(sessionFile))
                File.WriteAllText(sessionFile, "RowIndex,FrequencyHz,R_ohm,X_ohm,T_degC,Range_ohm,Timestamp\n");

            if (!File.Exists(rejectsFile))
                File.WriteAllText(rejectsFile, "RowIndex,Reason,Timestamp\n");

            var context = new SessionContext
            {
                SessionFolder = sessionFolder,
                SessionFileWriter = new StreamWriter(sessionFile, true) { AutoFlush = true },
                RejectsWriter = new StreamWriter(rejectsFile, true) { AutoFlush = true }
            };

            _sessions[meta.SessionId] = context;

            Console.WriteLine($"Started session {meta.SessionId} for {meta.BatteryId}_{meta.TestId}_SoC{meta.SoC}");
        }

        public void PushSample(EisSample sample)
        {
            if (sample == null || string.IsNullOrEmpty(sample.SessionId))
                throw new FaultException<DataFormatFault>(new DataFormatFault { Message = "Sample or SessionId is null" });

            if (!_sessions.TryGetValue(sample.SessionId, out var session))
                throw new FaultException<DataFormatFault>(new DataFormatFault { Message = "Session not started" });

            try
            {
                if (sample.FrequencyHz <= 0)
                {
                    session.RejectsWriter.WriteLine($"{sample.RowIndex},Frequency must be > 0,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    return;
                }

                string line = $"{sample.RowIndex},{sample.FrequencyHz},{sample.R_ohm},{sample.X_ohm},{sample.T_degC},{sample.Range_ohm},{sample.TimestampLocal:yyyy-MM-dd HH:mm:ss}";
                session.SessionFileWriter.WriteLine(line);

                Console.WriteLine($"Saved sample row {sample.RowIndex} for session {sample.SessionId}");
            }
            catch (Exception ex)
            {
                session.RejectsWriter.WriteLine($"{sample.RowIndex},{ex.Message},{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"Failed to write sample {sample.RowIndex}: {ex.Message}");
            }
        }

        public void EndSession(EisMeta meta)
        {
            if (meta == null || string.IsNullOrEmpty(meta.SessionId))
                throw new FaultException<DataFormatFault>(new DataFormatFault { Message = "Invalid metadata" });

            if (_sessions.TryRemove(meta.SessionId, out var session))
            {
                session.SessionFileWriter?.Dispose();
                session.RejectsWriter?.Dispose();
                Console.WriteLine($"Ended session {meta.SessionId} for {meta.BatteryId}_{meta.TestId}_SoC{meta.SoC}");
            }
        }

        public void Dispose()
        {
            foreach (var kv in _sessions)
            {
                kv.Value.SessionFileWriter?.Dispose();
                kv.Value.RejectsWriter?.Dispose();
            }
            _sessions.Clear();
        }

        private class SessionContext
        {
            public string SessionFolder { get; set; }
            public StreamWriter SessionFileWriter { get; set; }
            public StreamWriter RejectsWriter { get; set; }
        }
    }
}
