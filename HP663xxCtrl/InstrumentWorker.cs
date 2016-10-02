﻿using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HP663xxCtrl {
    class InstrumentWorker {
        HP663xx dev = null;
        public string VisaAddress {
           get ; private set;
        }
        BlockingCollection<Command> EventQueue;

        public volatile uint refreshDelay_ms = 1000;

        volatile bool StopRequested = false;
        public volatile bool StopAcquireRequested = false;

        public enum StateEnum {
            Disconnected,
            Connected,
            Measuring
        }
        enum CommandEnum {
            IRange,
            Acquire,
            Program
        }
        struct Command {
            public CommandEnum cmd;
            public object arg;
        }
        public struct AcquireDetails {
            public int NumPoints;
            public double Interval;
            public HP663xx.SenseModeEnum SenseMode;
            public double Level;
            public HP663xx.TriggerSlopeEnum triggerEdge;
            public int SegmentCount;
            public int SampleOffset;
        }
        public struct ProgramDetails {
            public bool Enabled;
            public bool OCP;
            public double V1, I1, V2, I2;
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
        public event EventHandler<HP663xx.InstrumentState> NewState;
        public event EventHandler<StateEnum> StateChanged;
        public void ThreadMain() {
            DateTime LastRefresh;

            dev = new HP663xx(VisaAddress);
            if (StateChanged != null) StateChanged(this, StateEnum.Connected);
            RefreshDisplay();
            LastRefresh = DateTime.Now;

            while (!StopRequested) {
                Command cmd;
                int timeout = (int)LastRefresh.AddMilliseconds(refreshDelay_ms).Subtract(DateTime.Now).TotalMilliseconds;
                while (EventQueue.TryTake(out cmd, timeout<10?30:timeout)) {
                    switch (cmd.cmd) {
                        case CommandEnum.IRange:
                            dev.SetCurrentRange((HP663xx.CurrentRanges)cmd.arg);
                            break;
                        case CommandEnum.Acquire:
                            DoAcquisition((AcquireDetails)cmd.arg);
                            break;
                        case CommandEnum.Program:
                            DoProgram((ProgramDetails)cmd.arg);
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
        public event EventHandler<HP663xx.MeasArray> DataAcquired;
        public void RequestIRange(HP663xx.CurrentRanges range) {
            EventQueue.Add(new Command() { cmd = CommandEnum.IRange, arg = range });
        }
        // Must set StopAcquireRequested to false before starting acquisition
        void DoAcquisition(AcquireDetails arg) {
            if (StateChanged != null) StateChanged(this, StateEnum.Measuring);

            int remaining = arg.SegmentCount;
            while (remaining > 0 && !StopRequested && !StopAcquireRequested) {
                int count=0;
                if (arg.triggerEdge == HP663xx.TriggerSlopeEnum.Immediate)
                    count = 1;
                else
                    count = Math.Min(remaining, 4096 / arg.NumPoints);
                var data = dev.MakeTransientMeasurement(
                    mode: arg.SenseMode,
                    numPoints: arg.NumPoints,
                    interval: arg.Interval,
                    triggerEdge: arg.triggerEdge,
                    level: arg.Level,
                    triggerCount: count,
                    triggerOffset: arg.SampleOffset);
                if (DataAcquired != null)
                    DataAcquired(this, data);
                remaining -= count;
            }
            if (StateChanged != null) StateChanged(this, StateEnum.Connected);
        }
        // Must set StopAcquireRequested to false before starting acquisition
        public void RequestAcquire(AcquireDetails details) {
            if (StopAcquireRequested == true)
                return;
            EventQueue.Add(new Command() {
                cmd = CommandEnum.Acquire,
                arg = details
            });
        }
        void DoProgram(ProgramDetails details) {
            dev.SetOCP(details.OCP);
        }
        public void RequestProgram(ProgramDetails details) {
            EventQueue.Add(new Command() {
                cmd = CommandEnum.Program,
                arg = details
            });
        }
        public void RequestShutdown() {
            StopRequested = true;
        }
    }
}