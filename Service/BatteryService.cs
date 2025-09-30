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
using System.Configuration;

namespace Service
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall)]
    public class BatteryService : IBatteryService
    {
        private static readonly string BaseDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        private static readonly ConcurrentDictionary<string, BatterySession> _sessions = new ConcurrentDictionary<string, BatterySession>();

        private readonly double T_threshold = double.Parse(ConfigurationManager.AppSettings["T_threshold"]);
        private readonly double R_min = double.Parse(ConfigurationManager.AppSettings["R_min"]);
        private readonly double R_max = double.Parse(ConfigurationManager.AppSettings["R_max"]);
        private readonly double Range_min = double.Parse(ConfigurationManager.AppSettings["Range_min"]);
        private readonly double Range_max = double.Parse(ConfigurationManager.AppSettings["Range_max"]);

        public void StartSession(EisMeta meta)
        {
            if (meta == null || string.IsNullOrEmpty(meta.SessionId))
                throw new FaultException<DataFormatFault>(new DataFormatFault { Message = "Invalid metadata" });

            string sessionFolder = Path.Combine(BaseDataPath, meta.BatteryId, meta.TestId, $"{meta.SoC}%");
            Directory.CreateDirectory(sessionFolder);

            var sessionFile = Path.Combine(sessionFolder, "session.csv");
            var rejectsFile = Path.Combine(sessionFolder, "rejects.csv");

            var session = new BatterySession
            {
                SessionFileWriter = new StreamWriter(sessionFile, append: false),
                RejectsWriter = new StreamWriter(rejectsFile, append: false),
                BatteryId = meta.BatteryId,
                SoC = meta.SoC
            };
            session.LastT = double.NaN;

            if (new FileInfo(sessionFile).Length == 0)
                session.SessionFileWriter.WriteLine("RowIndex,FrequencyHz,R_ohm,X_ohm,T_degC,Range_ohm,Timestamp");
            if (new FileInfo(rejectsFile).Length == 0)
                session.RejectsWriter.WriteLine("RowIndex,Reason,Timestamp");

            session.SessionFileWriter.Flush();
            session.RejectsWriter.Flush();

            _sessions[meta.SessionId] = session;
            Console.WriteLine($"Started session {meta.SessionId} for {meta.BatteryId}_{meta.TestId}_SoC{meta.SoC}");
        }

        public AckNackResponse PushSample(EisSample sample)
        {
            if (sample == null || string.IsNullOrEmpty(sample.SessionId))
                throw new FaultException<DataFormatFault>(new DataFormatFault { Message = "Sample or SessionId is null" });

            if (!_sessions.TryGetValue(sample.SessionId, out var session))
                throw new FaultException<DataFormatFault>(new DataFormatFault { Message = "Session not started" });

            try
            {
                if (sample.RowIndex <= session.LastRowIndex)
                    throw new FaultException<ValidationFault>(new ValidationFault { Message = "RowIndex not increasing" });
                if (sample.FrequencyHz <= 0)
                    throw new FaultException<ValidationFault>(new ValidationFault { Message = "Frequency must be > 0" });

                var response = new AckNackResponse
                {
                    Status = "ACK",
                    SessionStatus = "IN_PROGRESS"
                };

                // --- Resistance check
                if (sample.R_ohm < R_min || sample.R_ohm > R_max)
                {
                    string reason = $"Row {sample.RowIndex}, SoC={session.SoC}, Battery={session.BatteryId}, Expected R_ohm [{R_min}-{R_max}], Actual R_ohm={sample.R_ohm}";
                    session.RejectsWriter.WriteLine($"{sample.RowIndex},{reason},{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    session.RejectsWriter.Flush();
                    response.Status = "NACK";
                    response.WarningMessage = reason;
                    return response;
                }

                // --- Range check
                if (sample.Range_ohm < Range_min || sample.Range_ohm > Range_max)
                {
                    string reason = $"Row {sample.RowIndex}, SoC={session.SoC}, Battery={session.BatteryId}, Expected Range_ohm [{Range_min}-{Range_max}], Actual Range_ohm={sample.Range_ohm}";
                    session.RejectsWriter.WriteLine($"{sample.RowIndex},{reason},{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    session.RejectsWriter.Flush();
                    response.Status = "NACK";
                    response.WarningMessage = reason;
                    return response;
                }

                // --- Temperature spike detection (kept unchanged)
                if (!double.IsNaN(session.LastT))
                {
                    double deltaT = sample.T_degC - session.LastT;
                    if (Math.Abs(deltaT) > T_threshold)
                    {
                        response.TemperatureSpike = true;
                        response.DeltaT = deltaT;
                        response.SpikeDirection = deltaT > 0 ? "rise" : "fall";

                        // Add the requested fields
                        response.CurrentT = sample.T_degC;
                        response.FrequencyHz = sample.FrequencyHz;
                        response.SoC = session.SoC;

                        response.WarningMessage =
                            $"Temperature spike {response.SpikeDirection}: ΔT={deltaT}, " +
                            $"T={sample.T_degC}, Freq={sample.FrequencyHz}Hz, SoC={session.SoC}";
                    }
                }
                session.LastT = sample.T_degC;

                // Write sample to session file
                session.SessionFileWriter.WriteLine(
                    $"{sample.RowIndex},{sample.FrequencyHz},{sample.R_ohm},{sample.X_ohm},{sample.T_degC},{sample.Range_ohm},{sample.TimestampLocal:yyyy-MM-dd HH:mm:ss}");
                session.SessionFileWriter.Flush();
                session.LastRowIndex = sample.RowIndex;

                return response;
            }
            catch (Exception ex)
            {
                session.RejectsWriter.WriteLine($"{sample.RowIndex},{ex.Message},{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                session.RejectsWriter.Flush();
                return new AckNackResponse
                {
                    Status = "NACK",
                    SessionStatus = "IN_PROGRESS",
                    WarningMessage = ex.Message
                };
            }
        }

        public void EndSession(EisMeta meta)
        {
            if (meta == null || string.IsNullOrEmpty(meta.SessionId))
                throw new FaultException<DataFormatFault>(new DataFormatFault { Message = "Invalid metadata" });

            if (_sessions.TryRemove(meta.SessionId, out var session))
            {
                session.Dispose();
                Console.WriteLine($"Ended session {meta.SessionId} for {meta.BatteryId}_{meta.TestId}_SoC{meta.SoC}");
            }
            else
            {
                throw new FaultException<DataFormatFault>(new DataFormatFault { Message = "Session not found" });
            }
        }
    }
    }