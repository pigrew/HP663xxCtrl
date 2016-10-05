using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ZedGraph;

namespace HP663xxCtrl {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {

        MainWindowVm VM;

        public MainWindow() {
            InitializeComponent();
            ZedGraphControl zgc = (ZedGraphControl)ZedGraphHost.Child;
            float zgcSizeScale = 1.5f;
            zgc.GraphPane.IsFontsScaled = false;
            zgc.GraphPane.IsPenWidthScaled = false;

            zgc.GraphPane.Margin.All = zgc.GraphPane.Margin.Top * zgcSizeScale;

            zgc.GraphPane.XAxis.Scale.FontSpec.Size *= zgcSizeScale;
            zgc.GraphPane.XAxis.Title.FontSpec.Size *= zgcSizeScale;

            zgc.GraphPane.YAxis.Title.FontSpec.Size *= zgcSizeScale;
            zgc.GraphPane.YAxis.Scale.FontSpec.Size *= zgcSizeScale;

            zgc.GraphPane.Title.IsVisible = false;
            zgc.GraphPane.XAxis.Title.Text = "Time (s)";

            VM = new MainWindowVm();
            this.DataContext = VM;
        }

        System.Drawing.Color[] CurveColors = new System.Drawing.Color[] {
            System.Drawing.Color.Black, System.Drawing.Color.Red,
            System.Drawing.Color.Blue, System.Drawing.Color.Green
        };
        InstrumentWorker InstWorker;
        Thread InstThread;
        private void GoButton_Click(object sender, RoutedEventArgs e) {
            if (InstThread != null || InstWorker != null)
                return;
            ConnectButton.IsEnabled = false;
            DisconnectButton.IsEnabled = true;
            InstWorker = new InstrumentWorker(AddressComboBox.Text);
            InstWorker.WorkerDone += delegate(object sender2, EventArgs args)
            {
                Dispatcher.BeginInvoke((Action)(() =>
                {
                    InstThread = null;
                    InstWorker = null;
                    ConnectButton.IsEnabled = true;
                    DisconnectButton.IsEnabled = false;
                }));
            };
            InstWorker.NewState += delegate(object sender2, HP663xx.InstrumentState state)
            {
                Dispatcher.BeginInvoke((Action)(() =>
                {
                    UpdateStateLabels(sender2, state);
                }));
            };
            InstWorker.DataAcquired += delegate(object sender2, HP663xx.MeasArray measArray)
            {
                Dispatcher.BeginInvoke((Action)(() => { HandleDataAcquired(sender2, measArray); }));
            };
            InstWorker.ProgramDetailsReadback += delegate(object sender2, HP663xx.ProgramDetails details)
            {
                Dispatcher.BeginInvoke((Action)(() => { HandleProgramDetailsReadback(sender2, details); }));
            };
            InstWorker.LogerDatapointAcquired += delegate(object sender2, HP663xx.LoggerDatapoint point)
            {
                Dispatcher.BeginInvoke((Action)(() => { HandleLogDatapoint(sender2, point); }));
            };
            InstWorker.StateChanged += delegate(object sender2, InstrumentWorker.StateEnum state)
            {
                Dispatcher.BeginInvoke((Action)(() =>
                {
                    switch (state) {
                        case InstrumentWorker.StateEnum.Connected:
                            ConnectionStatusBarItem.Content = "CONNECTED";
                            AcquireButton.IsEnabled = true;
                            ApplyProgramButton.IsEnabled = true;
                            StopAcquireButton.IsEnabled = false;
                            ClearProtectionButton.IsEnabled = true;
                            LogButton.IsEnabled = true;
                            StopLoggingButton.IsEnabled = false;
                            AddressComboBox.IsEnabled = false;
                            break;
                        case InstrumentWorker.StateEnum.Disconnected:
                            ConnectionStatusBarItem.Content = "DISCONNECTED";
                            AcquireButton.IsEnabled = false;
                            ApplyProgramButton.IsEnabled = false;
                            StopAcquireButton.IsEnabled = false;
                            ClearProtectionButton.IsEnabled = false;
                            LogButton.IsEnabled = false;
                            ModelStatusBarItem.Content = "-----";
                            AddressComboBox.IsEnabled = true;
                            break;
                        case InstrumentWorker.StateEnum.Measuring:
                            ConnectionStatusBarItem.Content = "MEASURING"; break;
                    }
                }));
            };
            InstThread = new Thread(InstWorker.ThreadMain);
            InstThread.IsBackground = true;

            InstThread.Name = "Instrument Worker";
            InstThread.Start();
        }
        private void DisconnectButton_Click(object sender, RoutedEventArgs e) {
            ConnectionStatusBarItem.Content = "DISCONNECTING";
            InstWorker.RequestShutdown();
        }
        private void UpdateStateLabels(object sender, HP663xx.InstrumentState state) {
            NumberFormatInfo nfi = (NumberFormatInfo)CultureInfo.CurrentCulture.NumberFormat.Clone();
            nfi.NumberNegativePattern = 1;
            Ch1VLabel.Text = state.V.ToString("N3",nfi).PadLeft(7) + " V";
            if (state.IRange > 1.1) {
                Ch1ILabel.Text = state.I.ToString("N4", nfi).PadLeft(7) + "  A";
            } else {
                Ch1ILabel.Text = (state.I * 1000).ToString("N3",nfi).PadLeft(6) + " mA";
            }
            Ch1StatusLabel.Text =
                 ((state.Flags.Questionable.HasFlag(HP663xx.QuestionableStatusEnum.Unregulated)) ? "UNR" : "  ") +
                " " +
                (state.OVP? "OVP" : "   ") +
                " " +
                (state.OCP ? "OCP" : "   ") +
                " " +
                ((state.Flags.Operation.HasFlag(HP663xx.OperationStatusEnum.CV)) ? "CV" : "  ") +
                " " +
                (
                    state.Flags.Operation.HasFlag(HP663xx.OperationStatusEnum.CCPositive) ? "CC+" : (
                       (state.Flags.Operation.HasFlag(HP663xx.OperationStatusEnum.CCNegative) ? "CC-" : "   ")
                    )
                );
            if (!double.IsNaN(state.V2)) {
                Ch2VLabel.Text = state.V2.ToString("N3", nfi).PadLeft(7) + " V";
                Ch2ILabel.Text = state.I2.ToString("N4", nfi).PadLeft(6) + "  A"; // Always in amps
            } else {
                Ch2VLabel.Text = "--.---" + " -";
                Ch2ILabel.Text = "--.----" + "  -";
            }
            Ch2StatusLabel.Text =
                 ((state.Flags.Questionable.HasFlag(HP663xx.QuestionableStatusEnum.Unregulated2)) ? "UNR" : "  ") +
                " " +
                (state.OVP ? "   " : "   ") + // CH2 doesn't have OVP???
                " " +
                (state.OCP ? "OCP" : "   ") +
                " " +
                ((state.Flags.Operation.HasFlag(HP663xx.OperationStatusEnum.CV2)) ? "CV" : "  ") +
                " " +
                (state.Flags.Operation.HasFlag(HP663xx.OperationStatusEnum.CC2) ? "CC " : "   ");

            DVMVLabel.Text = state.DVM.ToString("N3",nfi);
        }
        private void HandleDataAcquired(object sender, HP663xx.MeasArray result) {

            ZedGraphControl zgc = (ZedGraphControl)ZedGraphHost.Child;
            zgc.GraphPane.XAxis.Title.Text = "Time";
            
            zgc.GraphPane.YAxis.Title.Text = "Current";
            double[] xlist = Enumerable.Range(0, result.Data[0].Length).Select(x => (double)((x) * result.TimeInterval)).ToArray();
            for (int i = 0; i < result.Data.Length; i++) {
                zgc.GraphPane.AddCurve("Acq" + i.ToString(),
                    xlist,
                    result.Data[i],
                    CurveColors[i % (CurveColors.Length)], SymbolType.None);
            }
            zgc.AxisChange();
            zgc.Invalidate();
        }

        private void OnMenuItem_Exit(object sender, RoutedEventArgs e) {
            this.Close();
        }
        void HandleProgramDetailsReadback(object sender, HP663xx.ProgramDetails details) {
            EnableOutputCheckbox.IsChecked = details.Enabled;
            OCPCheckbox.IsChecked = details.OCP;
            VM.V1 = details.V1;
            VM.I1 = details.I1;
            VM.V2 = details.V2;
            VM.I2 = details.I2;
            VM.HasChannel2 = details.HasOutput2;
            VM.HasDVM = details.HasDVM;
            
            OVPCheckbox.IsChecked = details.OVP;
            VM.OVPLevel = details.OVPVal;
            var IDSplit = details.ID.Split(new char[] {','});
            ModelStatusBarItem.Content = IDSplit[1] + " (" + IDSplit[2].ToUpper() + ")";

            // Limits for programming
            CH1VTextBox.MaxValue = details.MaxV1;
            CH1ITextBox.MaxValue = details.MaxI1;
            if (details.HasOutput2) {
                CH2VTextBox.MaxValue = details.MaxV2;
                CH2ITextBox.MaxValue = details.MaxI2;
            }
            switch (details.Range) {
                case HP663xx.CurrentRanges.TWENTY_mA: CurrentRangeComboBox.SelectedIndex = 0; break;
                default: CurrentRangeComboBox.SelectedIndex = 1; break;
            }
            switch (details.Detector) {
                case HP663xx.CurrentDetectorEnum.DC: ACDCDetectorComboBox.SelectedIndex = 0; break;
                case HP663xx.CurrentDetectorEnum.ACDC: ACDCDetectorComboBox.SelectedIndex = 1; break;
            }
        }
        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (InstWorker != null) {
                ComboBoxItem item = (ComboBoxItem)e.AddedItems[0];
                switch ((string)item.Tag) {
                    case "LOW":
                        InstWorker.RequestIRange(HP663xx.CurrentRanges.TWENTY_mA); break;
                    case "HIGH":
                        InstWorker.RequestIRange(HP663xx.CurrentRanges.HIGH); break;
                    case "AUTO":
                        InstWorker.RequestIRange(HP663xx.CurrentRanges.HIGH); break;
                }
            }
        }
        bool ZedgraphLoggingMode = false;
        DateTime LogStartTime;
        void HandleLogDatapoint(object sender, HP663xx.LoggerDatapoint dp) {
            if(!double.IsNaN(dp.Min))
                zgc.GraphPane.CurveList[0].AddPoint(
                    dp.time.Subtract(LogStartTime).TotalSeconds,
                    dp.Min);
            if(!double.IsNaN(dp.Mean))
                zgc.GraphPane.CurveList[1].AddPoint(
                    dp.time.Subtract(LogStartTime).TotalSeconds,
                    dp.Mean);
            if(!double.IsNaN(dp.Max))
            zgc.GraphPane.CurveList[2].AddPoint(
                dp.time.Subtract(LogStartTime).TotalSeconds,
                dp.Max);
            if(!double.IsNaN(dp.RMS))
                zgc.GraphPane.CurveList[3].AddPoint(
                    dp.time.Subtract(LogStartTime).TotalSeconds,
                    dp.RMS);
            zgc.AxisChange();
            zgc.Invalidate();
        }
        private void LogButton_Click(object sender, RoutedEventArgs e) {

            if (InstWorker != null) {
                LogStartTime = DateTime.Now;
                InstWorker.StopAcquireRequested = false;
                AcquireButton.IsEnabled = false;
                ApplyProgramButton.IsEnabled = false;
                ClearProtectionButton.IsEnabled = false;
                StopLoggingButton.IsEnabled = true;
                zgc.GraphPane.CurveList.Clear();
                zgc.GraphPane.AddCurve("Min", new double[0], new double[0], System.Drawing.Color.Blue);
                zgc.GraphPane.AddCurve("Mean", new double[0], new double[0], System.Drawing.Color.Black);
                zgc.GraphPane.AddCurve("Max", new double[0], new double[0], System.Drawing.Color.Red);
                zgc.GraphPane.AddCurve("RMS", new double[0], new double[0], System.Drawing.Color.Red);

                zgc.GraphPane.CurveList[0].IsVisible = LoggerMinCheckBox.IsChecked.Value;
                zgc.GraphPane.CurveList[1].IsVisible = LoggerMeanCheckBox.IsChecked.Value;
                zgc.GraphPane.CurveList[2].IsVisible = LoggerMaxCheckBox.IsChecked.Value;
                zgc.GraphPane.CurveList[3].IsVisible = LoggerRMSCheckBox.IsChecked.Value;
                ZedgraphLoggingMode = true;

                HP663xx.SenseModeEnum mode;
                if (LogVoltageRadioButton.IsChecked.Value) {
                    mode = HP663xx.SenseModeEnum.VOLTAGE;
                    zgc.GraphPane.YAxis.Title.Text = "Voltage (V)";
                } else if (LogCurrentRadioButton.IsChecked.Value) {
                    mode = HP663xx.SenseModeEnum.CURRENT;
                    zgc.GraphPane.YAxis.Title.Text = "Current (A)";
                } else {
                    mode = HP663xx.SenseModeEnum.DVM;
                    zgc.GraphPane.YAxis.Title.Text = "Voltage (V)";
                }
                InstWorker.RequestLog(mode);
            }
        }

        private void StopLoggingButton_Click(object sender, RoutedEventArgs e) {
            StopLoggingButton.IsEnabled = false;
            InstWorker.StopAcquireRequested = true;
        }

        private void AcquireButton_Click(object sender, RoutedEventArgs e) {

            if (InstWorker != null) {
                var blah = GetTreeErrors(AcquisitionTabItem);
                InstWorker.StopAcquireRequested = false;
                AcquireButton.IsEnabled = false;
                ApplyProgramButton.IsEnabled = false;
                ClearProtectionButton.IsEnabled = false;
                StopAcquireButton.IsEnabled = true;
                LogButton.IsEnabled = false;
                zgc.GraphPane.CurveList.Clear();
                ZedgraphLoggingMode = false;

                InstrumentWorker.AcquireDetails details = new InstrumentWorker.AcquireDetails();
                details.NumPoints = VM.AcqNumPoints;
                double duration = VM.AcqDuration;
                details.Interval = duration / details.NumPoints;
                details.Level = VM.TriggerLevel;
                details.SampleOffset = VM.TriggerOffset;
                details.TriggerHysteresis = VM.TriggerHysteresis;
                if (AcqVoltageRadioButton.IsChecked.Value) {
                    details.SenseMode = HP663xx.SenseModeEnum.VOLTAGE;
                    zgc.GraphPane.YAxis.Title.Text = "Voltage (V)";
                } else if (AcqCurrentRadioButton.IsChecked.Value) {
                    details.SenseMode = HP663xx.SenseModeEnum.CURRENT;
                    zgc.GraphPane.YAxis.Title.Text = "Current (A)";
                } else {
                    details.SenseMode = HP663xx.SenseModeEnum.DVM;
                    zgc.GraphPane.YAxis.Title.Text = "Voltage (V)";
                }

                details.SegmentCount = VM.AcqSegments;

                switch ((string)((ComboBoxItem)TriggerComboBox.SelectedItem).Tag) {
                    case "IMMED": details.triggerEdge = HP663xx.TriggerSlopeEnum.Immediate; break;
                    case "POS": details.triggerEdge = HP663xx.TriggerSlopeEnum.Positive; break;
                    case "NEG": details.triggerEdge = HP663xx.TriggerSlopeEnum.Negative; break;
                    case "EITHER": details.triggerEdge = HP663xx.TriggerSlopeEnum.Either; break;
                    default: throw new Exception();
                }
                InstWorker.RequestAcquire(details);
            }
        }

        private void StopAcquireButton_Click(object sender, RoutedEventArgs e) {
            StopAcquireButton.IsEnabled = false;
            InstWorker.StopAcquireRequested = true;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            if (InstWorker != null) {
                InstWorker.RequestShutdown();
                // Hack... probably some better way to do this
                System.Threading.Thread.Sleep(500);
            }
        }

        private void ApplyProgramButton_Click(object sender, RoutedEventArgs e) {
            HP663xx.ProgramDetails details = new HP663xx.ProgramDetails();
            string ParseError = "";
            var blah = OVPLevelTextBox;
            if (InstWorker == null)
                return;

            details.Enabled = EnableOutputCheckbox.IsChecked.Value;
            details.OCP = OCPCheckbox.IsChecked.Value;
            details.OVP = OVPCheckbox.IsChecked.Value;

            if (Validation.GetHasError(CH1VTextBox) || Validation.GetHasError(CH1ITextBox)) {
                ParseError = ParseError + "Ch 1 Voltage or current is invalid.\n";
            }
            if (VM.HasChannel2 && ( Validation.GetHasError(CH1VTextBox) || Validation.GetHasError(CH1ITextBox))) {
                ParseError = ParseError + "Ch 2 Voltage or current is invalid.\n";
            }

            details.OVPVal = double.NaN;
            if (details.OVP) {
                if (Validation.GetHasError(OVPLevelTextBox)) {
                    ParseError = ParseError + "OVP level is invalid.\n";
                    // 22 V is the max valid value on a 66309D
                }
            }
            // FIXME: Add some more range checking
            if (ParseError != "") {
                MessageBox.Show(ParseError, "Parse Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            InstWorker.RequestProgram(details);
        }

        private void ClearProtectionButton_Click(object sender, RoutedEventArgs e) {
            InstWorker.RequestClearProtection();
        }

        private void LoggerCurveCheckBox_Checked(object sender, RoutedEventArgs e) {
            if (ZedgraphLoggingMode) {
                zgc.GraphPane.CurveList[0].IsVisible = LoggerMinCheckBox.IsChecked.Value;
                zgc.GraphPane.CurveList[1].IsVisible = LoggerMeanCheckBox.IsChecked.Value;
                zgc.GraphPane.CurveList[2].IsVisible = LoggerMaxCheckBox.IsChecked.Value;
                zgc.GraphPane.CurveList[3].IsVisible = LoggerRMSCheckBox.IsChecked.Value;
                zgc.Invalidate();
            }

        }
        // From http://stackoverflow.com/questions/127477/detecting-wpf-validation-errors
        private List<ValidationError> GetTreeErrors(DependencyObject obj, bool childrenOnly=false) {
            // The dependency object is valid if it has no errors and all
            // of its children (that are dependency objects) are error-free.
            List<ValidationError> errors = new List<ValidationError>();
            errors.AddRange(Validation.GetErrors(obj));

            errors.AddRange(LogicalTreeHelper.GetChildren(obj)
            .OfType<DependencyObject>()
            .SelectMany(x => GetTreeErrors(x)));
            return errors;
        }

        private void ACDCDetectorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (InstWorker != null) {
                switch ((string)((ComboBoxItem)e.AddedItems[0]).Tag) {
                    case "DC": InstWorker.RequestACDCDetector(HP663xx.CurrentDetectorEnum.DC); break;
                    case "ACDC": InstWorker.RequestACDCDetector(HP663xx.CurrentDetectorEnum.ACDC); break;
                }

            }
        }
    }
}
