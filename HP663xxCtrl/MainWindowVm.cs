using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Text;
using System.Windows.Input;
using GalaSoft.MvvmLight.CommandWpf;
using GalaSoft.MvvmLight;
using System.Windows;
using Microsoft.Win32;

namespace HP663xxCtrl  {
    public class MainWindowVm : ViewModelBase {

        bool _HasDVM = true;
        public bool HasDVM {
            get { return _HasDVM; }
            set { this.Set(ref _HasDVM, value, "HasDVM"); }
        }
        Current[] _I1Ranges = new Current[0];
        public Current[] I1Ranges {
            get { return _I1Ranges; }
            set { this.Set(ref _I1Ranges, value, "I1Ranges"); }
        }
        bool _HasChannel2 = true;
        public bool HasChannel2 {
            get { return _HasChannel2; }
            set { Set(ref _HasChannel2, value, "HasChannel2"); }
        }

        private double _OVPLevel = 20;
        public double OVPLevel {
            get { return _OVPLevel; }
            set { Set(ref _OVPLevel, value, "OVPLevel"); }
        }

        private double _V1 = 0.0;
        public double V1 {
            get { return _V1; }
            set { Set(ref _V1, value, "V1"); }
        }

        private double _I1 = 0.02;
        public double I1 {
            get { return _I1; }
            set { Set(ref _I1, value, "I1"); }
        }


        private double _V2 = 0.0;
        public double V2 {
            get { return _V2; }
            set { Set(ref _V2, value, "V2"); }
        }

        private double _I2 = 0.02;
        public double I2 {
            get { return _I2; }
            set { Set(ref _I2, value, "I2"); }
        }
        private double _AcqDuration = 0.1;
        public double AcqDuration {
            get { return _AcqDuration; }
            set { Set(ref _AcqDuration, value, "AcqDuration"); }
        }
        private int _AcqNumPoints = 1024;
        public int AcqNumPoints {
            get { return _AcqNumPoints; }
            set { Set(ref _AcqNumPoints, value, "AcqNumPoints"); }
        }

        private int _AcqSegments = 1;
        public int AcqSegments {
            get { return _AcqSegments; }
            set { Set(ref _AcqSegments, value, "AcqNumSegments"); }
        }

        private double _TriggerHysteresis = 0.0;
        public double TriggerHysteresis {
            get { return _TriggerHysteresis; }
            set { Set(ref _TriggerHysteresis, value, "TriggerHysteresis"); }
        }
        private double _TriggerLevel = 0.0;
        public double TriggerLevel {
            get { return _TriggerLevel; }
            set { Set(ref _TriggerLevel, value, "TriggerLevel"); }
        }
        private int _TriggerOffset = 0;
        public int TriggerOffset {
            get { return _TriggerOffset; }
            set { Set(ref _TriggerOffset, value, "TriggerOffset"); }
        } 
        
        public ICommand DLFirmwareCommand { get; private set; }
        void DLFirmware() {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Binary file (*.bin)|*.bin|All Files (*.*)|*.*";
            var result = sfd.ShowDialog();
            if (!result.HasValue || result == false)
                return;
            if (Window.InstWorker == null) {
                MessageBox.Show("No device connected. Cannot download firmware");
                return;
            }
            Window.InstWorker.RequestDLFirmware(sfd.FileName);
        }

        bool CanDownloadFirmware() {
            return Window.DisconnectButton.IsEnabled && Window.InstWorker != null;
        }
        MainWindow Window;
        public MainWindowVm(MainWindow w) {
            this.Window = w;
            DLFirmwareCommand = new RelayCommand(DLFirmware, CanDownloadFirmware);
        }
    }
}
