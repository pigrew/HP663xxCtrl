using System;
using System.Collections.Generic;
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
                            break;
                        case InstrumentWorker.StateEnum.Disconnected:
                            ConnectionStatusBarItem.Content = "DISCONNECTED";
                            AcquireButton.IsEnabled = false;
                            ApplyProgramButton.IsEnabled = false;
                            StopAcquireButton.IsEnabled = false;
                            ClearProtectionButton.IsEnabled = false;
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
            InstWorker.RequestShutdown();
        }
        private void UpdateStateLabels(object sender, HP663xx.InstrumentState state) {
            Ch1VLabel.Text = state.V.ToString("#0.000") + " V";
            if (state.IRange > 1.1) {
                Ch1ILabel.Text = state.I.ToString("0.0000") + "  A";
            } else {
                Ch1ILabel.Text = (state.I * 1000).ToString("#0.000") + " mA";
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
                Ch2VLabel.Text = state.V2.ToString("#0.###") + " V";
                Ch2ILabel.Text = state.I2.ToString("0.0000") + "  A"; // Always in amps
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

            DVMVLabel.Text = state.DVM.ToString("#0.###");
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
            CH1VTextBox.Text = details.V1.ToString();
            CH1ITextBox.Text = details.I1.ToString();
            CH2VTextBox.Text = details.V2.ToString();
            CH2ITextBox.Text = details.I2.ToString();
            DVMGroupBox.Visibility = details.HasDVM ?
                System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            var CH2Visibility = details.HasOutput2 ?
                System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            CH2ReadbackGroupBox.Visibility = CH2Visibility;
            CH2ProgramLabel.Visibility = CH2Visibility;
            CH2VTextBox.Visibility = CH2Visibility;
            CH2ITextBox.Visibility = CH2Visibility;
            OVPCheckbox.IsChecked = details.OVP;
            OVPLevelTextBox.Text = details.OVPVal.ToString();
            var IDSplit = details.ID.Split(new char[] {','});
            ModelStatusBarItem.Content = IDSplit[1] + " (" + IDSplit[2].ToUpper() + ")";

            // Limits for programming
            CH1VTextBox.MaxValue = details.MaxV1;
            CH1ITextBox.MaxValue = details.MaxI1;
            if (details.HasOutput2) {
                CH2VTextBox.MaxValue = details.MaxV2;
                CH2ITextBox.MaxValue = details.MaxI2;
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

        private void AcquireButton_Click(object sender, RoutedEventArgs e) {

            if (InstWorker != null) {
                InstWorker.StopAcquireRequested = false;
                AcquireButton.IsEnabled = false;
                ApplyProgramButton.IsEnabled = false;
                ClearProtectionButton.IsEnabled = false;
                StopAcquireButton.IsEnabled = true;
                zgc.GraphPane.CurveList.Clear();
            
                InstrumentWorker.AcquireDetails details = new InstrumentWorker.AcquireDetails();
                details.NumPoints = int.Parse(NumPtsTextBox.Text);
                double duration = double.Parse(DurationTextBox.Text.Trim());
                details.Interval = duration / details.NumPoints;
                details.Level = double.Parse(TrigLevelTextBox.Text);
                details.SampleOffset = int.Parse(SampleOffsetTextBox.Text);
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

                details.SegmentCount = int.Parse(NumSegmentsTextBox.Text);

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

            if (!Double.TryParse(CH1VTextBox.Text, out details.V1)) {
                ParseError = ParseError + "Cannot parse Ch 1 Voltage.\n";
            }
            if (!Double.TryParse(CH1ITextBox.Text, out details.I1)) {
                ParseError = ParseError + "Cannot parse Ch 1 Current.\n";
            }
            if (!Double.TryParse(CH2VTextBox.Text, out details.V2)) {
                ParseError = ParseError + "Cannot parse Ch 2 Voltage.\n";
            }
            if (!Double.TryParse(CH2ITextBox.Text, out details.I2)) {
                ParseError = ParseError + "Cannot parse Ch 2 Current.\n";
            }

            details.OVPVal = double.NaN;
            if (details.OVP) {
                if (!Double.TryParse(OVPLevelTextBox.Text, out details.OVPVal)) {
                    ParseError = ParseError + "Cannot parse OVP Level.\n";
                    // 22 V is the max valid value on a 66309D
                } else if (details.OVPVal < 0 || details.OVPVal > 22.0)
                    ParseError = ParseError + "OVP is outsize of the valid range (0.0 -> 22.0).\n";
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

    }
}
