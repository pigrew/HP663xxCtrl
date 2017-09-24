using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HP663xxCtrl {
    public struct MeasArray {
        public SenseModeEnum Mode;
        public double TimeInterval;
        public double[][] Data;
    }

    public struct LoggerDatapoint {
        public double Min, Mean, Max, RMS;
        public DateTime time;
    }
    public enum OutputCompensationEnum {
        HighCap,
        LowCap
    }
    public enum CurrentDetectorEnum {
        DC,
        ACDC
    }
    public enum SenseModeEnum {
        CURRENT,
        VOLTAGE,
        DVM
    }
    public enum TriggerSlopeEnum {
        Immediate,
        Positive,
        Negative,
        Either
    }
    public struct StatusFlags {
        public bool Calibration,
            WaitingForTrigger,
            CV, CV2, CC, CCPositive, CCNegative, CC2;
        // 663xx Questionable
        public bool OV, OCP,
            FP_Local, // frontpanel local was pressed
            OverTemperature,
            OpenSenseLead,
            Unregulated2,
            RemoteInhibit, Unregulated,
            OverCurrent2,
            MeasurementOverload;
    }
    public struct InstrumentState {
        public StatusFlags Flags;
        public double IRange;
        public double V, I, V2, I2, DVM;
        public double duration;
        public bool OutputEnabled;
        public bool OutputEnabled2;
        public bool OVP; // Is OVP enabled?
        public bool OCP; // IS OCP enabled?
    }
    public struct ProgramDetails {
        public bool Enabled;
        public bool OCP;// IS OCP enabled?
        public bool OVP; // Is OVP enabled?
        public double OVPVal;
        public double V1, I1, V2, I2;

        // And other things that are not actually used during programming, and is implicit to the unit
        public bool HasDVM, HasOutput2;
        public string ID;
        public double MaxV1, MaxI1, MaxV2, MaxI2;
        public double I1Range;
        public double[] I1Ranges;
        public CurrentDetectorEnum Detector;
    }
    interface IFastSMU {
        bool HasOutput2{get;}
        bool HasDVM { get; }
        bool HasOVP { get; }
        void Reset();
        void SetOCP(bool enabled);
        void Close(bool goToLocal = true);
        void SetOutputCompensation(OutputCompensationEnum comp);
        void SetCurrentDetector(CurrentDetectorEnum detector);
        void ClearProtection();
        void SetCurrentRange(double range);
        void SetIV(int channel, double voltage, double current);
        void SetOVP(double ovp);
        void EnableOutput(bool enabled);
        InstrumentState ReadState(bool measureCh2 = true, bool measureDVM = true);
        LoggerDatapoint[] MeasureLoggingPoint(SenseModeEnum mode);
        ProgramDetails ReadProgramDetails();
        // Measurements
        void AbortMeasurement();
        void SetupLogging(
            SenseModeEnum mode, 
            double interval
            );
        void StartTransientMeasurement(
            SenseModeEnum mode,
            int numPoints = 4096,
            double interval = 15.6e-6,
            double level = double.NaN,
            double hysteresis = 0.0,
            int triggerCount = 1,
            TriggerSlopeEnum triggerEdge = TriggerSlopeEnum.Positive,
            int triggerOffset = 0
            );
        MeasArray FinishTransientMeasurement(
            SenseModeEnum mode,
            int triggerCount = 1);
        bool IsMeasurementFinished();
    }
}
