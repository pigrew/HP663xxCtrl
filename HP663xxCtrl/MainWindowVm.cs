using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Input;
using GalaSoft.MvvmLight.CommandWpf;
using GalaSoft.MvvmLight;
using System.Windows;
using Microsoft.Win32;

namespace HP663xxCtrl  {
    public class MainWindowVm : ViewModelBase {

        public InstrumentWorker InstWorker;
        public Thread InstThread;

        bool _HasDVM = true;
        public bool HasDVM {
            get { return _HasDVM; }
            set {
                System.Diagnostics.Trace.WriteLine("Hasdvm is " + value);
                Set(ref _HasDVM, value);
            }
        }
        bool _HasOVP = true;
        public bool HasOVP {
            get { return _HasOVP; }
            set { Set(ref _HasOVP, value); }
        }
        Current[] _I1Ranges = new Current[0];
        public Current[] I1Ranges {
            get { return _I1Ranges; }
            set { this.Set(ref _I1Ranges, value); }
        }
        bool _HasChannel2 = true;
        public bool HasChannel2 {
            get { return _HasChannel2; }
            set { Set(ref _HasChannel2, value); }
        }

        private double _OVPLevel = 20;
        public double OVPLevel {
            get { return _OVPLevel; }
            set { Set(ref _OVPLevel, value); }
        }

        private double _V1 = 0.0;
        public double V1 {
            get { return _V1; }
            set { Set(ref _V1, value); }
        }

        private double _I1 = 0.02;
        public double I1 {
            get { return _I1; }
            set { Set(ref _I1, value); }
        }


        private double _V2 = 0.0;
        public double V2 {
            get { return _V2; }
            set { Set(ref _V2, value); }
        }

        private double _I2 = 0.02;
        public double I2 {
            get { return _I2; }
            set { Set(ref _I2, value); }
        }
        private double _AcqDuration = 0.1;
        public double AcqDuration {
            get { return _AcqDuration; }
            set { Set(ref _AcqDuration, value); }
        }
        private int _AcqNumPoints = 1024;
        public int AcqNumPoints {
            get { return _AcqNumPoints; }
            set { Set(ref _AcqNumPoints, value); }
        }

        private int _AcqSegments = 1;
        public int AcqSegments {
            get { return _AcqSegments; }
            set { Set(ref _AcqSegments, value); }
        }

        private double _TriggerHysteresis = 0.0;
        public double TriggerHysteresis {
            get { return _TriggerHysteresis; }
            set { Set(ref _TriggerHysteresis, value); }
        }
        private double _TriggerLevel = 0.0;
        public double TriggerLevel {
            get { return _TriggerLevel; }
            set { Set(ref _TriggerLevel, value); }
        }
        private int _TriggerOffset = 0;
        public int TriggerOffset {
            get { return _TriggerOffset; }
            set { Set(ref _TriggerOffset, value); }
        } 
        
        public ICommand DLFirmwareCommand { get; private set; }
        void DLFirmware() {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Binary file (*.bin)|*.bin|All Files (*.*)|*.*";
            var result = sfd.ShowDialog();
            if (!result.HasValue || result == false)
                return;
            if (InstWorker == null) {
                MessageBox.Show("No device connected. Cannot download firmware");
                return;
            }
            InstWorker.RequestDLFirmware(sfd.FileName);
        }

        bool CanDownloadFirmware() {
            return Window.DisconnectButton.IsEnabled && InstWorker != null;
        }
        public MainWindow Window;
        public MainWindowVm() {
            DLFirmwareCommand = new RelayCommand(DLFirmware, CanDownloadFirmware);
        }
    }
}
