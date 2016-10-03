using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ivi.Visa.Interop;

namespace HP663xxCtrl
{
    public class HP663xx
    {
        ResourceManager rm = new ResourceManager();
        FormattedIO488 dev = new FormattedIO488();
        public bool HasDVM { get; private set; }
        public bool HasOutput2 { get; private set; }

        string ID;
        public void Reset()
        {
            dev.WriteString("*RST");
            dev.WriteString("*CLS");
            dev.WriteString("STAT:PRES");
            dev.WriteString("*SRE 0");
            dev.WriteString("*ESE 0");
        }
        public enum CurrentRanges {
            TWENTY_mA,
            HIGH
        };
        public void SetCurrentRange(CurrentRanges range) {
            switch (range)
            {
                case CurrentRanges.TWENTY_mA: dev.WriteString("SENS:CURR:RANG MIN"); break;
                case CurrentRanges.HIGH: dev.WriteString("SENS:CURR:RANG MAX"); break;
            }
        }
        [Flags]
        public enum OperationStatusEnum
        {
            Calibration = 1,
            WaitingForTrigger = 32,
            CV = 256,
            CV2 = 512,
            CCPositive = 1024,
            CCNegative = 2048,
            CC2 = 4096
        }
        [Flags]
        public enum QuestionableStatusEnum
        {
            OV = 1,
            OCP = 2,
            FP_Local = 8, // frontpanel local was pressed
            OverTemperature = 16,
            OpenSenseLead = 32,
            Unregulated2 = 256,
            RemoteInhibit = 512,
            Unregulated = 1024,
            OverCurrent2 = 4096,
            MeasurementOverload = 16384
        }
        [Flags]
        public enum StatusByteEnum
        {
            QuestionableStatusSummary = 8,
            MesasgeAvailable = 16,
            EventSTB = 32,
            MasterStatusSummary = 64,
            OperationStatusSummary = 128
        }
        public struct StatusFlags
        {
            public QuestionableStatusEnum Questionable;
            public OperationStatusEnum Operation;

        }
        public struct InstrumentState {
            public StatusFlags Flags;
            public double IRange;
            public double V, I, V2, I2, DVM;
            public double duration;
            public bool OutputEnabled;
            public bool OutputEnabled2;
            public bool OVP;
            public bool OCP;
        }
        public struct ProgramDetails {
            public bool Enabled;
            public bool OCP;
            public bool OVP;
            public double OVPVal;
            public double V1, I1, V2, I2;
            public bool HasDVM, HasOutput2;
            public string ID;
            public double MaxV1, MaxI1, MaxV2, MaxI2;
        }
        public ProgramDetails ReadProgramDetails() {

            string response = Query("OUTP?;VOLT?;CURR?;"
                + ":VOLT:PROT:STAT?;:VOLT:PROT?;:CURR:PROT:STAT?" +
                (HasOutput2? ";:VOLT2?;CURR2?":"")).Trim();
            string[] parts = response.Split(new char[] { ';' });
            ProgramDetails details = new ProgramDetails() {
                Enabled = (parts[0] == "1"),
                V1 = double.Parse(parts[1]),
                I1 = double.Parse(parts[2]),
                OVP = (parts[3] == "1"),
                OVPVal = double.Parse(parts[4]),
                OCP = (parts[5] == "1"),
                V2 = HasOutput2? double.Parse(parts[6]):double.NaN,
                I2 = HasOutput2 ? double.Parse(parts[7]) : double.NaN,
                HasDVM = HasDVM,
                HasOutput2 = HasOutput2,
                ID = ID
            };
            // Maximums
            parts = Query("VOLT? MAX; CURR? MAX").Trim().Split(new char[] {';'});
            details.MaxV1 = double.Parse(parts[0]);
            details.MaxI1 = double.Parse(parts[0]);
            if (HasOutput2) {
                parts = Query("VOLT2? MAX; CURR2? MAX").Trim().Split(new char[] { ';' });
                details.MaxV2 = double.Parse(parts[0]);
                details.MaxI2 = double.Parse(parts[0]);

            }

            return details;
        }
        public InstrumentState ReadState(bool measureCh2=true, bool measureDVM=true) {
            InstrumentState ret = new InstrumentState();
            DateTime start = DateTime.Now;
            // ~23 ms
            string statusStr = Query("stat:oper:cond?;:stat:ques:cond?;:sense:curr:range?;" +
                ":OUTP1?;VOLTage:PROTection:STAT?;:CURR:PROT:STAT?").Trim();
            string[] statuses = statusStr.Split(new char[] { ';' });
            ret.Flags = new StatusFlags();
            ret.Flags.Operation = (OperationStatusEnum)int.Parse(statuses[0]);
            ret.Flags.Questionable = (QuestionableStatusEnum)int.Parse(statuses[1]);
            ret.IRange = double.Parse(statuses[2]);
            ret.OutputEnabled = statuses[3] == "1";
            ret.OVP = statuses[4] == "1";
            ret.OCP = statuses[5] == "1";
            // Must measure each thing individually
            // Default is 2048 points, with 46.8us rate
            // This is 95.8 ms; about 6 PLC in America, or 5 in other places.
            // But, might be better to do one PLC?
            // For CH1:
            // Setting  time
            //      1    30
            //  2048/46.8    230
            //   4096    168
            // 
            dev.WriteString("TRIG:ACQ:SOUR INT;COUNT:VOLT 1;:TRIG:ACQ:COUNT:CURR 1");
            dev.WriteString("SENS:SWE:POIN 2048; TINT 46.8e-6");
            dev.WriteString("SENS:SWE:OFFS:POIN 0;:SENS:WIND HANN");
            // Channel is about 30 ms
            ret.V = Double.Parse(Query("MEAS:VOLT?"));
            ret.I = Double.Parse(Query("MEAS:CURR?"));
            // Ch2 is about 100 ms
            if (measureCh2 && HasOutput2) {
                ret.V2 = Double.Parse(Query("MEAS:VOLT2?"));
                ret.I2 = Double.Parse(Query("MEAS:CURR2?")); // Fixed at 2048*(15.6us)
            } else {
                ret.V2 = double.NaN;
                ret.I2 = double.NaN;
            }

            // RMS is also available using MEAS:DVM:ACDC
            if(measureDVM && HasDVM)
                ret.DVM = Double.Parse(Query("MEAS:DVM?")); // 2048*(15.6us) => 50 ms
            else
                ret.DVM = Double.NaN;
            ret.duration = DateTime.Now.Subtract(start).TotalMilliseconds;
            return ret;
        }
        public StatusFlags GetStatusFlags()
        {
            StatusFlags flags = new StatusFlags();
            string val = Query("stat:oper:cond?;:stat:ques:cond?");
            int[] statuses = val.Split(new char[] { ';' }).Select(x => int.Parse(x)).ToArray();
            flags.Operation = (OperationStatusEnum)statuses[0];
            flags.Questionable = (QuestionableStatusEnum)statuses[1];
            return flags;
        }
        public OperationStatusEnum GetOperationStatus()
        {
            return (OperationStatusEnum)int.Parse(Query("STAT:OPER:COND?"));
        }
        public QuestionableStatusEnum GetQuestionableStatus()
        {
            return (QuestionableStatusEnum)int.Parse(Query("STAT:QUES:COND?"));
        }
        public enum SenseModeEnum {
            CURRENT,
            VOLTAGE,
            DVM
        }
        string Query(string cmd)
        {
            dev.WriteString(cmd);
            return dev.ReadString();
        }
        public void ClearErrors()
        {
            string msg;
            while(!( (msg = Query("SYSTem:ERRor?")).StartsWith("+0,"))) {
            }
        }
        public struct MeasArray
        {
            public SenseModeEnum Mode;
            public double TimeInterval;
            public double[][] Data;
        }
        public enum TriggerSlopeEnum {
            Immediate,
            Positive,
            Negative,
            Either
        }
        public MeasArray MakeTransientMeasurement(
            SenseModeEnum mode,
            int numPoints = 4096,
            double interval = 15.6e-6,
            double level = double.NaN,
            int triggerCount = 1,
            TriggerSlopeEnum triggerEdge = TriggerSlopeEnum.Positive,
            int triggerOffset = 0
            )
        {
            if (triggerCount * numPoints > 4096) {
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
            dev.WriteString("SENSe:FUNCtion \"" + modeString + "\"");
            if (numPoints < 1 || numPoints > 4096)
                throw new InvalidOperationException("Number of points must be betweer 1 and 4096");
            // Immediate always has a trigger count of 1
            if (triggerEdge == TriggerSlopeEnum.Immediate)
                triggerCount = 1;
            if (interval < 15.6e-6)
                interval = 15.6e-6;
            if (interval > 1e4)
                interval = 1e4;
            dev.WriteString("SENSe:SWEEP:POINTS " + numPoints.ToString() + "; " +
                "TINTerval " + interval.ToString() + ";" +
                "OFFSET:POINTS " + triggerOffset.ToString());
            if(triggerEdge== TriggerSlopeEnum.Immediate || double.IsNaN(level)) {
                dev.WriteString("TRIG:ACQ:SOURCE BUS");
                dev.WriteString("ABORT;*OPC");
                dev.WriteString("INIT:NAME ACQ;:TRIG:ACQ;*OPC?");
            } else {
                string slopeStr = "EITH";
                switch (triggerEdge) {
                    case TriggerSlopeEnum.Either: slopeStr = "EITH"; break;
                    case TriggerSlopeEnum.Positive: slopeStr = "POS"; break;
                    case TriggerSlopeEnum.Negative: slopeStr = "NEG"; break;
                }
                dev.WriteString("TRIG:ACQ:COUNT:" + modeString + " " + triggerCount.ToString() + ";" +
                    ":TRIG:ACQ:LEVEL:" + modeString + " " + level.ToString() + ";" +
                    ":TRIG:ACQ:SLOPE:" + modeString + " " + slopeStr + ";");
                dev.WriteString("TRIG:ACQ:SOURCE INT");
                dev.WriteString("ABORT;*OPC");
                dev.WriteString("INIT:NAME ACQ;*OPC?");
            }
            StatusByteEnum stb;
            do {
                System.Threading.Thread.Sleep(50);
                stb = (StatusByteEnum)dev.IO.ReadSTB();
            } while (!stb.HasFlag(StatusByteEnum.MesasgeAvailable));
            dev.ReadString(); // read the +1 from *OPC?

            if (mode == SenseModeEnum.CURRENT)
                dev.WriteString("FETCH:ARRay:CURRent?");
            else
                dev.WriteString("FETCH:ARRay:VOLTAGE?");

            float[] data = (float[])dev.ReadIEEEBlock(IEEEBinaryType.BinaryType_R4);
            MeasArray res = new MeasArray();
            res.Mode = mode;
            res.Data = new double[triggerCount][];
            for (int i = 0; i < triggerCount; i++) {
                res.Data[i] = data.Skip(numPoints*i)
                    .Take(numPoints)
                    .Select(x => (double)x)
                    .ToArray();

            }
            res.TimeInterval = double.Parse(Query("SENSE:SWEEP:TINT?"));
            return res;
        }
        public void ClearProtection() {
            dev.WriteString("OUTPut:PROTection:CLEar");
        }
        public void EnableOutput(bool enabled)
        {
            dev.WriteString("OUTPUT  " + (enabled?"ON":"OFF"));
        }
        public void SetIV(int channel, double voltage, double current) {
            dev.WriteString("VOLT" + 
                (channel==2?"2 ":" ") + voltage.ToString() +
                ";:CURR" + 
                (channel==2?"2 ":" ") + current.ToString() 
                );
        }
        public void SetVoltage(double voltage)
        {
            dev.WriteString("VOLT " + voltage.ToString());
        }
        public void SetCurrent(double current)
        {
            dev.WriteString("CURRENT " + current.ToString());
        }
        /// <summary>
        /// Set to Double.NaN to disable OVP
        /// </summary>
        /// <param name="ovp"></param>
        public void SetOVP(double ovp) {
            if (double.IsNaN(ovp))
                dev.WriteString("VOLTage:PROTection:STATe OFF");
            else {
                dev.WriteString("VOLTAGE:PROTECTION " + ovp.ToString());
                dev.WriteString("VOLTage:PROTection:STATe ON");
            }
        }
        public void SetOCP(bool enabled) {
            dev.WriteString("CURR:PROT:STAT " + (enabled ? "1":"0"));
        }
        // PSC causes too much writing to non-volatile RAM. Automatically disable it, if active.
        // People _probably_ won't depend on it....
        void EnsurePSCOne()
        {
            int psc = int.Parse(Query("*PSC?"));
            if (psc == 0)
                dev.WriteString("*PSC 1"); ;
        }
        public HP663xx(string addr)
        {
            dev.IO = (IMessage)rm.Open(addr, mode: AccessMode.NO_LOCK);
            dev.IO.Clear(); // clear I/O buffer
            dev.IO.Timeout = 3000; // 3 seconds

            dev.WriteString("*IDN?");
            ID = dev.ReadString();
            if (ID.Contains(",66309B,") || ID.Contains(",66319B,")) {
                HasDVM = false; HasOutput2 = true;
            } else if (ID.Contains(",66309D,") || ID.Contains(",66319D,")) {
                HasDVM = true; HasOutput2 = true;
            } else if (ID.Contains(",66311B,") || ID.Contains(",66321B,")) {
                HasDVM = false; HasOutput2 = false;
            } else if (ID.Contains(",66311D,") || ID.Contains(",66321D,")) {
                HasDVM = true; HasOutput2 = true;
            } else  {
                dev.IO.Close();
                dev.IO = null;
                throw new InvalidOperationException("Not a 66309 supply!");
            }
            dev.WriteString("STATUS:PRESET"); // Clear PTR/NTR/ENABLE register
            EnsurePSCOne();
            dev.WriteString("*CLS"); // clear status registers
            dev.WriteString("ABORT");
            ClearErrors();
            dev.WriteString("FORMAT REAL");
            dev.WriteString("FORMat:BORDer NORMAL");
            // Detector only applies to the HIGH current range
            dev.WriteString("SENSe:CURRent:DETector ACDC");
            // Enable the detection of open sense leads
            dev.WriteString("SENSe:PROTection:STAT ON");
            
        }
        public enum OutputCompensationEnum {
            HighCap,
            LowCap
        }
        // Usually use low capacitance mode, so it's always stable. Manual says high requires C_in >5uF
        public void SetOutputCompensation(OutputCompensationEnum comp) {
            switch (comp) {
                case OutputCompensationEnum.HighCap:
                    dev.WriteString("OUTPUT:TYPE HIGH");
                    break;
                case OutputCompensationEnum.LowCap:
                    dev.WriteString("OUTPUT:TYPE LOW");
                    break;
            }
        }
        public void Close(bool goToLocal = true)
        {
            if (dev != null && dev.IO != null) {
                if (goToLocal) {
                    IGpib gpibdev = dev.IO as IGpib;
                    if (gpibdev != null)
                        gpibdev.ControlREN(RENControlConst.GPIB_REN_GTL);
                }
                dev.IO.Close();
                dev.IO = null;
            }
            if (dev != null)
                dev = null;
        }
    }
}
