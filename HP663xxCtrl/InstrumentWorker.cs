using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ivi.Visa;

namespace HP663xxCtrl {
    public class InstrumentWorker {
        IFastSMU dev = null;
        public string VisaAddress {
           get ; private set;
        }
        BlockingCollection<Command> EventQueue;

        public volatile uint refreshDelay_ms = 1000;

        volatile bool StopRequested = false;
        public volatile bool StopAcquireRequested = false;
        ProgramDetails LastProgramDetails;

        public enum StateEnum {
            Disconnected,
            Connected,
            Measuring
        }
        enum CommandEnum {
            IRange,
            Acquire,
            Program,
            ClearProtection,
            Log,
            SetACDCDetector,
            DLFirmware
        }
        struct Command {
            public CommandEnum cmd;
            public object arg;
        }
        public struct AcquireDetails {
            public int NumPoints;
            public double Interval;
            public SenseModeEnum SenseMode;
            public double Level;
            public double TriggerHysteresis;
            public TriggerSlopeEnum triggerEdge;
            public int SegmentCount;
            public int SampleOffset;
        }
        void RefreshDisplay() {
            var state = dev.ReadState();
            if (NewState != null)
                NewState(this, state);
        }
        public InstrumentWorker(string address) {
            this.VisaAddress = address;
            EventQueue = new BlockingCollection<Command>(new ConcurrentQueue<Command>());
        }
        public event EventHandler WorkerDone;
        public event EventHandler<InstrumentState> NewState;
        public event EventHandler<StateEnum> StateChanged;
        public event EventHandler<ProgramDetails> ProgramDetailsReadback;
        DateTime LastRefresh;
        public void ThreadMain() {
            // have to open the device to find the ID 
            IMessageBasedSession visaDev = (IMessageBasedSession)GlobalResourceManager.Open(VisaAddress,AccessModes.None, 1000);
            visaDev.Clear();
            visaDev.FormattedIO.WriteLine("*IDN?");
            string idn = visaDev.FormattedIO.ReadLine();
            if (K2304.SupportsIDN(idn))
                dev = new K2304(visaDev);
            else if (HP663xx.SupportsIDN(idn))
                dev = new HP663xx(visaDev);
            else
                throw new Exception("unsupported device");
            if (StateChanged != null) StateChanged(this, StateEnum.Connected);
            if (ProgramDetailsReadback != null) {
                ProgramDetails progDetails = dev.ReadProgramDetails();
                LastProgramDetails = progDetails;
                ProgramDetailsReadback(this, LastProgramDetails);
            }
            RefreshDisplay();
            LastRefresh = DateTime.Now;

            while (!StopRequested) {
                Command cmd;
                int timeout = (int)LastRefresh.AddMilliseconds(refreshDelay_ms).Subtract(DateTime.Now).TotalMilliseconds;
                while (EventQueue.TryTake(out cmd, timeout<10?30:timeout)) {
                    switch (cmd.cmd) {
                        case CommandEnum.IRange:
                            DoSetCurrentRange((double)cmd.arg);
                            break;
                        case CommandEnum.Acquire:
                            DoAcquisition((AcquireDetails)cmd.arg);
                            break;
                        case CommandEnum.Log:
                            DoLog((SenseModeEnum)((object[])cmd.arg)[0],(double)((object[])cmd.arg)[1]);
                            break;
                        case CommandEnum.Program:
                            DoProgram((ProgramDetails)cmd.arg);
                            break;
                        case CommandEnum.ClearProtection:
                            DoClearProtection();
                            break;
                        case CommandEnum.SetACDCDetector:
                            DoACDCDetector((CurrentDetectorEnum)cmd.arg);
                            break;
                        case CommandEnum.DLFirmware:
                            DoDLFirmware((string)cmd.arg);
                            break;
                        default:
                            throw new Exception("Unhandled command in InstrumentWorker");
                    }
                }
                RefreshDisplay();
                LastRefresh = DateTime.Now;
            }
            try {
                EventQueue.Dispose();
                EventQueue = null;
            } catch {}
            
            dev.Close();
            if (StateChanged != null) StateChanged(this, StateEnum.Disconnected);
            if(WorkerDone!=null)
                WorkerDone.Invoke(this,null);
        }
        public event EventHandler<MeasArray> DataAcquired;
        void DoSetCurrentRange(double range) {
            dev.SetCurrentRange(range);
            LastProgramDetails.I1Range = range;
        }
        public void RequestIRange(double range) {
            EventQueue.Add(new Command() { cmd = CommandEnum.IRange, arg = range });
        }
        // Must set StopAcquireRequested to false before starting acquisition
        void DoAcquisition(AcquireDetails arg) {
            if (StateChanged != null) StateChanged(this, StateEnum.Measuring);

            int remaining = arg.SegmentCount;
            while (remaining > 0 && !StopRequested && !StopAcquireRequested) {
                int count = 0;
                if (arg.triggerEdge == TriggerSlopeEnum.Immediate)
                    count = 1;
                else
                    count = Math.Min(remaining, 4096 / arg.NumPoints);
                dev.StartTransientMeasurement(
                    mode: arg.SenseMode,
                    numPoints: arg.NumPoints,
                    interval: arg.Interval,
                    triggerEdge: arg.triggerEdge,
                    level: arg.Level,
                    hysteresis: arg.TriggerHysteresis,
                    triggerCount: count,
                    triggerOffset: arg.SampleOffset);

                while (!dev.IsMeasurementFinished() && !StopAcquireRequested
                    && !StopRequested) {
                    System.Threading.Thread.Sleep(70);
                }

                if (StopAcquireRequested || StopRequested) {
                    dev.AbortMeasurement();
                    if (StateChanged != null) StateChanged(this, StateEnum.Connected);
                    return;
                }
                var data = dev.FinishTransientMeasurement(mode: arg.SenseMode, triggerCount: count);

                if (DataAcquired != null)
                    DataAcquired(this, data);
                remaining -= count;
            }
            if (StateChanged != null) StateChanged(this, StateEnum.Connected);
        }
        // Must set StopAcquireRequested to false before starting acquisition
        //
        // Also, the returned AcquisitionData structure will have a blank 
        // SamplingPeriod and DataSeries
        //
        public AcquisitionData RequestAcquire(AcquireDetails details) {
            AcquisitionData data = new AcquisitionData();
            data.AcqDetails = details;
            data.ProgramDetails = LastProgramDetails;
            data.StartAcquisitionTime = DateTime.Now;

            if (StopAcquireRequested == true)
                return data;
            EventQueue.Add(new Command() {
                cmd = CommandEnum.Acquire,
                arg = details
            });
            return data;
        }
        public event EventHandler<LoggerDatapoint> LogerDatapointAcquired;
        void DoLog(SenseModeEnum mode, double interval) {
            if (StateChanged != null) StateChanged(this, StateEnum.Measuring);
            dev.SetupLogging(mode, interval);

            while (!StopRequested && !StopAcquireRequested) {

                if (StopAcquireRequested || StopRequested) {
                    dev.AbortMeasurement();
                    if (StateChanged != null) StateChanged(this, StateEnum.Connected);
                    return;
                }
                var data = dev.MeasureLoggingPoint(mode);
                if (LogerDatapointAcquired != null) {
                    foreach(var p in data)
                        LogerDatapointAcquired(this, p);

                }
            }
            if (StateChanged != null) StateChanged(this, StateEnum.Connected);
        }
        void DoDLFirmware(string filename) {
            try {
                using (BinaryWriter bw = new BinaryWriter(File.Open(filename, FileMode.Create))) {
                    for (uint i = 0; i <= 0xFFFF && !StopAcquireRequested; i+=4) {
                        var x = ((HP663xx)dev).GetFirmwareWord(i);
                        foreach(var w in x)
                            bw.Write(w);
                    }
                }
            } catch { // mostly IO exceptions

            }
        }
        public void RequestDLFirmware(string filename) {
            if (StopAcquireRequested == true)
                return;
            EventQueue.Add(new Command() {
                cmd = CommandEnum.DLFirmware,
                arg = filename
            });
        }

        public void RequestLog(SenseModeEnum mode, double interval) {
            if (StopAcquireRequested == true)
                return;
            EventQueue.Add(new Command() {
                cmd = CommandEnum.Log,
                arg = new object[] {mode,interval}
            });
        }
        void DoProgram(ProgramDetails details) {
            if (!details.Enabled) {
                dev.EnableOutput(details.Enabled);
                dev.SetOCP(details.OCP);
            }
            if(dev.HasOVP)
                dev.SetOVP(details.OVP? details.OVPVal:double.NaN);
            dev.SetIV(1, details.V1, details.I1);
            if(details.HasOutput2)
                dev.SetIV(2, details.V2, details.I2);
            if (details.Enabled) {
                dev.SetOCP(details.OCP);
                dev.EnableOutput(details.Enabled);
            }
            LastRefresh = DateTime.MinValue;
            // Copy element by element to keep old value of detector, etc....
            LastProgramDetails.V1 = details.V1;
            LastProgramDetails.I1 = details.I1;
            LastProgramDetails.V2 = details.V2;
            LastProgramDetails.I2 = details.I2;
            LastProgramDetails.OVP = details.OVP;
            LastProgramDetails.OVPVal = details.OVPVal;
            LastProgramDetails.Enabled = details.Enabled;
            LastProgramDetails.OCP = details.OCP;
        }

        public void RequestProgram(ProgramDetails details) {
            EventQueue.Add(new Command() {
                cmd = CommandEnum.Program,
                arg = details
            });
        }
        void DoDLFirmware() {

        }
        void DoClearProtection() {
            dev.ClearProtection();
        }
        public void RequestClearProtection() {
            EventQueue.Add(new Command() {
                cmd = CommandEnum.ClearProtection,
                arg = null
            });
        }
        public void RequestShutdown() {
            StopRequested = true;
        }
        void DoACDCDetector(CurrentDetectorEnum detector) {
            dev.SetCurrentDetector(detector);
            LastProgramDetails.Detector = detector;
        }
        public void RequestACDCDetector(CurrentDetectorEnum detector) {
            EventQueue.Add(new Command() {
                cmd = CommandEnum.SetACDCDetector,
                arg = detector
            });
        }
    }
}
