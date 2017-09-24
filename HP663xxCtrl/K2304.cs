using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ivi.Visa;

namespace HP663xxCtrl {
    class K2304 :IFastSMU {
        CultureInfo CI = System.Globalization.CultureInfo.InvariantCulture;

        IMessageBasedSession dev;
        public bool HasDVM { get; private set; }
        public bool HasOutput2 { get; private set; }

        public bool HasOVP { get { return false; } }
        string ID;
        public void Reset()
        {
            WriteString("*RST");
            WriteString("*CLS");
            WriteString("STAT:PRES");
            WriteString("*SRE 0");
            WriteString("*ESE 0");
        }

        public void SetCurrentRange(double range) {
            WriteString("SENS:CURR:RANG " + range.ToString(CI));
        }
        [Flags]
        public enum MeasurementEventEnum {
            ReadingOverflow = 8,
            PulseTriggertimeout = 16,
            ReadingAvailable = 32,
            BufferFull = 512
        }
        [Flags]
        public enum QuestionableEventEnum {
            CalibrationSummary = 256, // invalid calibration
        }
        [Flags]
        public enum OperationStatusEnum
        {
            CC = 8,
            CurrentLimitTripped = 16,
            HeatSinkShutdown = 32,
            PowerSupplyShutdown =64
        }
        [Flags]
        public enum StatusByteEnum
        {
            MeasurementSummary = 1,
            ErrorAvailable = 4,
            QuestionableStatusSummary = 8,
            MessageAvailable = 16,
            EventSTB = 32,
            MasterStatusSummary = 64,
            OperationStatusSummary = 128
        }

        private double LFrequency;
        public ProgramDetails ReadProgramDetails() {
            ProgramDetails details = new ProgramDetails() {
                OVP =false, // no OVP on this unit
                OVPVal = Double.NaN,
                HasDVM = HasDVM,
                HasOutput2 = HasOutput2,
                HasOVP = this.HasOVP,
                I1Ranges = new double[] {0.005, 5},
                ID = ID
            };
            details.Enabled = (Query("OUTP?").Trim().StartsWith("1"));
            details.V1 = double.Parse(Query("VOLT?").Trim(),CI);
            details.I1 = double.Parse(Query(":CURR:LIMIT?").Trim(), CI);
            details.OCP = (Query(":CURR:LIMIT:TYPE?").Trim()== "TRIP");
            // Maximums
            details.MaxV1 = double.Parse(Query("VOLT? MAX").Trim(), CI);
            details.MaxI1 = double.Parse(Query(":SENSE:CURRENT:RANGE? MAX"),CI);
            /*if (HasOutput2) {
                parts = Query("VOLT2? MAX; CURR2? MAX").Trim().Split(new char[] { ';' });
                details.MaxV2 = double.Parse(parts[0],CI);
                details.MaxI2 = double.Parse(parts[1],CI);

            }*/
            details.I1Range = Double.Parse(Query(":sense:curr:range?").Trim(),CI);

            details.Detector = CurrentDetectorEnum.ACDC;

            return details;
        }
        public InstrumentState ReadState(bool measureCh2=true, bool measureDVM=true) {
            InstrumentState ret = new InstrumentState();
            DateTime start = DateTime.Now;
              //  ":OUTP?;VOLTage:PROTection:STAT?;:CURR:PROT:STAT?").Trim();
           // string[] statuses = statusStr.Split(new char[] { ';' });
            ret.Flags = DecodeFlags(
                (OperationStatusEnum)int.Parse(Query("stat:oper:cond?"), CI),
                (QuestionableEventEnum)int.Parse(Query(":stat:ques:cond?"), CI));
            ret.IRange = double.Parse(Query(":sense:curr:range?"),CI);
            ret.OutputEnabled = Query("OUTP?").Trim() == "1";
            ret.OVP = false;
            ret.OCP = (Query(":CURR:LIMIT:TYPE?").Trim() == "TRIP");
            WriteString("SENSE:NPLC 1;AVERAGE 1");
            // Channel is about 30 ms
            // RMS is also available using MEAS:DVM:ACDC
            if (measureDVM && HasDVM)
                ret.DVM = QueryDouble("MEAS:DVM?")[0]; // 2048*(15.6us) => 50 ms
            else
                ret.DVM = Double.NaN;
            ret.V = QueryDouble("MEAS:VOLT?")[0];
            ret.I = QueryDouble("MEAS:CURR?")[0];
            // Ch2 is about 100 ms
            if (measureCh2 && HasOutput2) {
                ret.V2 = Double.Parse(Query("MEAS:VOLT2?"),CI);
                ret.I2 = Double.Parse(Query("MEAS:CURR2?"),CI); // Fixed at 2048*(15.6us)
            } else {
                ret.V2 = double.NaN;
                ret.I2 = double.NaN;
            }

            ret.duration = DateTime.Now.Subtract(start).TotalMilliseconds;
            return ret;
        }
        protected StatusFlags DecodeFlags(OperationStatusEnum opFlags, QuestionableEventEnum questFlags) {
            StatusFlags flags = new StatusFlags();
            flags.Calibration = questFlags.HasFlag(QuestionableEventEnum.CalibrationSummary);
            flags.CC = opFlags.HasFlag(OperationStatusEnum.CC);
            flags.OCP = opFlags.HasFlag(OperationStatusEnum.CurrentLimitTripped);
            flags.OverTemperature = opFlags.HasFlag(OperationStatusEnum.HeatSinkShutdown);
            flags.MeasurementOverload = opFlags.HasFlag(OperationStatusEnum.PowerSupplyShutdown);
            return flags;
        }
        public StatusFlags GetStatusFlags()
        {
            string val = Query("stat:oper:cond?;:stat:ques:cond?");
            int[] statuses = val.Split(new char[] { ';' }).Select(x => int.Parse(x,CI)).ToArray();
            return DecodeFlags((OperationStatusEnum)statuses[0], (QuestionableEventEnum)statuses[1]);
        }
        public OperationStatusEnum GetOperationStatus()
        {
            return (OperationStatusEnum)int.Parse(Query("STAT:OPER:COND?"),CI);
        }
        public QuestionableEventEnum GetQuestionableStatus()
        {
            return (QuestionableEventEnum)int.Parse(Query("STAT:QUES:COND?"), CI);
        }

        public void ClearErrors()
        {
            WriteString("SYSTEM:CLEAR");
            //string msg;
            //while(!( (msg = Query("SYSTem:ERRor?")).StartsWith("0,"))) {
            //}
        }

        public void SetupLogging(
            SenseModeEnum mode, double interval
            ) {
            string modeString;
            double nplc = (interval - 400e-6) * 0.9;
            nplc = nplc / (1 / LFrequency);
            if (nplc < 0.01)
                nplc = 0.01;
            else if (nplc > 10)
                nplc = 10.0;
            else if(nplc > 1)
                nplc = Math.Floor(nplc);

            if (mode == SenseModeEnum.DVM && !HasDVM)
                throw new Exception();
            switch (mode) {
                case SenseModeEnum.CURRENT: modeString = "CURR"; break;
                case SenseModeEnum.VOLTAGE: modeString = "VOLT"; break;
                case SenseModeEnum.DVM: modeString = "DVM"; break;
                default: throw new InvalidOperationException("Unknown transient measurement mode");
            }
            // Immediate always has a trigger count of 1
            WriteString("SENSe:FUNCtion \"" + modeString + "\"");
            WriteString("SENS:NPLC " + nplc.ToString());

            Query("*OPC?");
        }
        public LoggerDatapoint[] MeasureLoggingPoint( SenseModeEnum mode) {
            LoggerDatapoint ret = new LoggerDatapoint();
            double[] rsp;
            switch(mode) {
                case SenseModeEnum.CURRENT:
                    rsp = QueryDouble("MEAS:CURR?");
                   //parts = rsp.Split(new char[] { ';' });
                    ret.Mean = rsp[0];
                    // K2304A doesn't support these other things
                    ret.Mean = ret.Max = ret.RMS = ret.Mean;
                   /* ret.Min = double.Parse(parts[1], CI);
                    ret.Max = double.Parse(parts[2], CI);
                    ret.RMS = double.Parse(parts[3], CI);*/
                    break;
                case SenseModeEnum.VOLTAGE:
                    rsp = QueryDouble("MEAS:VOLT?");
                    ret.Mean = rsp[0];
                   /* ret.Min = double.Parse(parts[1], CI);
                    ret.Max = double.Parse(parts[2], CI);
                    ret.RMS = double.Parse(parts[3], CI);*/
                    break;
                case SenseModeEnum.DVM:
                    rsp = QueryDouble("MEAS:DVM?");
                    ret.Mean = rsp[0];
                    /* ret.Min = double.Parse(parts[1], CI);
                     ret.Max = double.Parse(parts[2], CI);
                     ret.RMS = double.Parse(parts[3], CI);*/
                    break;
            }
            ret.time = DateTime.Now;
            return new LoggerDatapoint[] {ret};
        }
        public void StartTransientMeasurement(
            SenseModeEnum mode,
            int numPoints = 4096,
            double interval = 15.6e-6,
            double level = double.NaN,
            double hysteresis = 0.0,
            int triggerCount = 1,
            TriggerSlopeEnum triggerEdge = TriggerSlopeEnum.Positive,
            int triggerOffset = 0
            )
        {
            //double nplc = 1;
            // Always 33 us integration time, but a 278 us period

            if (triggerCount * numPoints > 5000) {
                throw new InvalidOperationException();
            }
            string modeString;
            if (mode == SenseModeEnum.DVM && !HasDVM)
                throw new Exception();
            switch (mode)
            {
                case SenseModeEnum.CURRENT: modeString = "CURR"; break;
                case SenseModeEnum.VOLTAGE:  modeString = "VOLT";  break;
                case SenseModeEnum.DVM:  modeString = "DVM"; break;
                default: throw new InvalidOperationException("Unknown transient measurement mode");
            }
            WriteString("SENSE:PCURRENT:SYNC OFF"); // off is digitization mode
            WriteString("SENSe:FUNCtion \"" + modeString + "\"");
            if (numPoints < 1 || numPoints > 4096)
                throw new InvalidOperationException("Number of points must be betweer 1 and 4096");
            // Immediate always has a trigger count of 1
            if (triggerEdge == TriggerSlopeEnum.Immediate)
                triggerCount = 1;
            if (interval < 15.6e-6)
                interval = 15.6e-6;
            if (interval > 1e4)
                interval = 1e4;
            WriteString("SENSe:PCUR:AVERAGE " + numPoints.ToString(CI) + "; " +
                "TINTerval " + interval.ToString(CI) + ";" +
                "OFFSET:POINTS " + triggerOffset.ToString(CI));
            if(triggerEdge== TriggerSlopeEnum.Immediate || double.IsNaN(level)) {
                WriteString("TRIG:ACQ:SOURCE BUS");
                WriteString("ABORT;*WAI");
                WriteString("INIT:NAME ACQ;:TRIG:ACQ");
            } else {
                string slopeStr = "EITH";
                switch (triggerEdge) {
                    case TriggerSlopeEnum.Either: slopeStr = "EITH"; break;
                    case TriggerSlopeEnum.Positive: slopeStr = "POS"; break;
                    case TriggerSlopeEnum.Negative: slopeStr = "NEG"; break;
                }
                WriteString("TRIG:ACQ:COUNT:" + modeString + " " + triggerCount.ToString(CI) + ";" +
                    ":TRIG:ACQ:LEVEL:" + modeString + " " + level.ToString(CI) + ";" +
                    ":TRIG:ACQ:SLOPE:" + modeString + " " + slopeStr + ";" +
                    ":TRIG:ACQ:HYST:" + modeString + " " + hysteresis.ToString(CI));
                WriteString("TRIG:ACQ:SOURCE INT");
                WriteString("ABORT;*WAI");
                WriteString("INIT:NAME ACQ");
            }
            // Clear status byte
            Query("*ESR?");
            WriteString("*OPC");
        }
        public bool IsMeasurementFinished() {
            return (((int.Parse(Query("*ESR?").Trim(), CI) & 1) == 1));
        }
        public void AbortMeasurement() {
            Query("ABORT;*OPC?");
        }
        public MeasArray FinishTransientMeasurement(
            SenseModeEnum mode,
            int triggerCount = 1) {
            /*StatusByteEnum stb;
            do {
                System.Threading.Thread.Sleep(50);
                stb = (StatusByteEnum)dev.IO.ReadSTB();
            } while (!stb.HasFlag(StatusByteEnum.MesasgeAvailable));
            dev.ReadString(); // read the +1 from *OPC?*/

            if (mode == SenseModeEnum.CURRENT)
                WriteString("FETCH:ARRay:CURRent?");
            else
                WriteString("FETCH:ARRay:VOLTAGE?");
            float[] data = dev.FormattedIO.ReadBinaryBlockOfSingle();
            MeasArray res = new MeasArray();
            res.Mode = mode;
            res.Data = new double[triggerCount][];
            int numPoints = data.Length / triggerCount;
            for (int i = 0; i < triggerCount; i++) {
                res.Data[i] = data.Skip(numPoints*i)
                    .Take(numPoints)
                    .Select(x => (double)x)
                    .ToArray();

            }
            // Might be rounded, so return the actual value, not the requested value
            res.TimeInterval = double.Parse(Query("SENSE:SWEEP:TINT?"),CI);
            return res;
        }
        public void ClearProtection() {
            WriteString("OUTPut:PROTection:CLEar");
        }
        public void EnableOutput(bool enabled)
        {
            WriteString("OUTPUT  " + (enabled?"ON":"OFF"));
        }
        public void SetIV(int channel, double voltage, double current) {
            if (!HasOutput2 && channel == 2)
                return;
            WriteString("VOLT" +
                (channel == 2 ? "2 " : " ") + voltage.ToString(CI));
            WriteString("CURR" +
                (channel == 2 ? "2" : "") + ":LIMIT " + current.ToString(CI) 
                );
        }
        /// <summary>
        /// Set to Double.NaN to disable OVP
        /// </summary>
        /// <param name="ovp"></param>
        public void SetOVP(double ovp) {
            throw new NotImplementedException();
        }
        public void SetOCP(bool enabled) {
            WriteString("CURR:LIMIT:TYPE " + (enabled ? "TRIP":"LIMIT"));
        }
        public static bool SupportsIDN(string idn) {
            if (idn.Contains("2304A,"))
                return true;
            return false;
        }
        public K2304(IMessageBasedSession visaDev)
        {
            dev = visaDev;
            dev.Clear(); // clear I/O buffer
            dev.TerminationCharacter = (byte)0x0a;
            dev.TerminationCharacterEnabled = false;
            dev.TimeoutMilliseconds = 5000; // 5 seconds

            if (dev is IGpibSession) {
                ((IGpibSession)dev).SendRemoteLocalCommand(GpibInstrumentRemoteLocalMode.AssertRen);
            }
            WriteString("*IDN?");
            ID = ReadString().Trim();
            if (ID.Contains("2304A,")) {
                HasDVM = true; HasOutput2 = false;
            } else  {
                dev.Dispose();
                dev = null;
                throw new InvalidOperationException("Not a 66309 supply!");
            }
            WriteString("STATUS:PRESET"); // Clear PTR/NTR/ENABLE register

            WriteString("*CLS"); // clear status registers
            ClearErrors();
            WriteString("FORMAT ASCII"); // Single seems broken, so does double?!
            //WriteString("FORMat:BORDer NORMAL");
            LFrequency = Double.Parse(Query("SYSTEM:LFRequency?"));
        }
        public void SetCurrentDetector(CurrentDetectorEnum detector) {
            switch (detector) {
                case CurrentDetectorEnum.ACDC: WriteString("SENSe:CURRent:DETector ACDC"); break;
                case CurrentDetectorEnum.DC: WriteString("SENSe:CURRent:DETector DC"); break;
            }
        }

        // Usually use low capacitance mode, so it's always stable. Manual says high requires C_in >5uF
        public void SetOutputCompensation(OutputCompensationEnum comp) {
            switch (comp) {
                case OutputCompensationEnum.HighCap:
                    WriteString("OUTPUT:TYPE HIGH");
                    break;
                case OutputCompensationEnum.LowCap:
                    WriteString("OUTPUT:TYPE LOW");
                    break;
            }
        }

        string Query(string cmd) {
            WriteString(cmd);
            return ReadString();
        }
        double[] QueryDouble(string cmd) {
            //WriteString(cmd);
            return Query(cmd).Trim()
                .Split(new char[]{','})
                .Select(x => double.Parse(x)).ToArray();
            //return dev.FormattedIO.ReadBinaryBlockOfDouble();
        }
        void WriteString(string msg) {
            dev.FormattedIO.WriteLine(msg);
        }
        string ReadString() {
            // When  
            StatusByteEnum status =(StatusByteEnum)dev.ReadStatusByte();
            while(!status.HasFlag(StatusByteEnum.MessageAvailable)) {
                status =(StatusByteEnum)dev.ReadStatusByte();
            };
            string ret = dev.FormattedIO.ReadLine();
            return ret;
        }
        public void Close(bool goToLocal = true)
        {
            if (dev != null) {
                if (goToLocal) {
                    if (dev is IGpibSession) {
                        ((IGpibSession)dev).SendRemoteLocalCommand(GpibInstrumentRemoteLocalMode.GoToLocal);
                    }
                }
                dev.Dispose();
                dev = null;
            }
        }
    }
}
