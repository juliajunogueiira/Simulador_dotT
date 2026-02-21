using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Simulador_dot.Core;
using Simulador_dot.Models;
using Simulador_dot.Utils;

namespace Simulador_dotT;

public partial class MainWindow : Window
{
    private const double KpMin = 0;
    private const double KpMax = 5.0;
    private const double KdMin = 0;
    private const double KdMax = 5.0;
    private const double VelMin = 100;
    private const double VelMax = 4000;

    private readonly SimulationEngine simulationEngine;
    private readonly PIDDiagnostico diagnostico;
    private readonly DispatcherTimer simulationTimer;

    private readonly List<PIDDiagnostico.Sugestao> currentSuggestions = new();
    private bool isInitializing;

    public MainWindow()
    {
        simulationEngine = new SimulationEngine(900, 680);
        diagnostico = new PIDDiagnostico(simulationEngine.DataCollector);
        isInitializing = true;

        InitializeComponent();

        simulationEngine.LapManager.LapCompleted += OnLapCompleted;
        simulationEngine.LapManager.RaceFinished += OnRaceFinished;

        simulationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        simulationTimer.Tick += SimulationTimer_Tick;
        simulationTimer.Start();

        InitializeUiState();
    }

    private void InitializeUiState()
    {
        isInitializing = true;

        ModeComboBox.ItemsSource = Enum.GetValues<SimulationEngine.OperationMode>();
        ModeComboBox.SelectedItem = SimulationEngine.OperationMode.Standby;
        simulationEngine.SetMode(SimulationEngine.OperationMode.Standby);

        KpSlider.Value = simulationEngine.PIDController.KP * 100;
        KdSlider.Value = simulationEngine.PIDController.KD * 100;
        BaseVelSlider.Value = simulationEngine.BaseVelocity;

        LapHistoryTextBox.Text = "Histórico de voltas:\n";

        isInitializing = false;
        UpdateModeUi();
        RefreshUi();
        RenderSimulation();
    }

    private void SimulationTimer_Tick(object? sender, EventArgs e)
    {
        simulationEngine.Update();
        RefreshUi();
        RenderSimulation();
    }

    private void OnLapCompleted(LapRecord lapRecord)
    {
        Dispatcher.Invoke(() =>
        {
            LapInfoText.Text = $"✅ Volta {lapRecord.LapNumber} | Tempo: {lapRecord.LapTime:F2}ms";
            UpdateLapHistory();
            UpdateSuggestions(lapRecord.LapNumber);
        });
    }

    private void OnRaceFinished()
    {
        Dispatcher.Invoke(() =>
        {
            LapInfoText.Text = $"🏁 CORRIDA FINALIZADA | {simulationEngine.LapManager.CurrentLap} voltas completadas!";
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            PauseButton.IsEnabled = false;
        });
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (!simulationEngine.IsRunning)
        {
            simulationEngine.Start();
        }
        else
        {
            simulationEngine.Resume();
        }

        StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        PauseButton.IsEnabled = true;
        RefreshUi();
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (!simulationEngine.IsRunning)
        {
            return;
        }

        if (simulationEngine.IsPaused)
        {
            simulationEngine.Resume();
            PauseButton.Content = "Pause";
        }
        else
        {
            simulationEngine.Pause();
            PauseButton.Content = "Resume";
        }

        RefreshUi();
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        simulationEngine.Stop();

        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        PauseButton.IsEnabled = false;
        PauseButton.Content = "Pause";

        RefreshUi();
        RenderSimulation();
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        simulationEngine.Stop();
        simulationEngine.LapManager.Reset();
        simulationEngine.PIDController.Reset();
        simulationEngine.ClearHistory();
        simulationEngine.PositionRobotAtStartLine();

        LapHistoryTextBox.Text = "Histórico de voltas:\n";
        currentSuggestions.Clear();
        RefreshSuggestionList();

        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        PauseButton.IsEnabled = false;
        PauseButton.Content = "Pause";

        RefreshUi();
        RenderSimulation();
    }

    private void ModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModeComboBox.SelectedItem is not SimulationEngine.OperationMode selectedMode)
        {
            return;
        }

        simulationEngine.SetMode(selectedMode);
        UpdateModeUi();
        RefreshUi();
    }

    private void KpSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (isInitializing || simulationEngine.PidAdjustmentsLocked)
        {
            return;
        }

        simulationEngine.PIDController.KP = KpSlider.Value / 100.0;
        UpdatePidLabels();
    }


    private void KdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (isInitializing || simulationEngine.PidAdjustmentsLocked)
        {
            return;
        }

        simulationEngine.PIDController.KD = KdSlider.Value / 100.0;
        UpdatePidLabels();
    }


    private void BaseVelSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (isInitializing || simulationEngine.PidAdjustmentsLocked)
        {
            return;
        }

        simulationEngine.BaseVelocity = BaseVelSlider.Value;
        UpdatePidLabels();
    }

    private void ApplySuggestionsButton_Click(object sender, RoutedEventArgs e)
    {
        if (currentSuggestions.Count == 0)
        {
            MessageBox.Show("Nenhuma sugestão disponível.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        foreach (var sugestao in currentSuggestions)
        {
            var fator = sugestao.Acao == PIDDiagnostico.Acao.Aumentar
                ? 1.0 + sugestao.PercentualSugerido / 100.0
                : 1.0 - sugestao.PercentualSugerido / 100.0;

            switch (sugestao.Parametro)
            {
                case PIDDiagnostico.Parametro.Kp:
                    simulationEngine.PIDController.KP = Math.Clamp(simulationEngine.PIDController.KP * fator, KpMin, KpMax);
                    KpSlider.Value = simulationEngine.PIDController.KP * 100;
                    break;


                case PIDDiagnostico.Parametro.Kd:
                    simulationEngine.PIDController.KD = Math.Clamp(simulationEngine.PIDController.KD * fator, KdMin, KdMax);
                    KdSlider.Value = simulationEngine.PIDController.KD * 100;
                    break;

                case PIDDiagnostico.Parametro.VelBase:
                    simulationEngine.BaseVelocity = Math.Clamp(simulationEngine.BaseVelocity * fator, VelMin, VelMax);
                    BaseVelSlider.Value = simulationEngine.BaseVelocity;
                    break;
            }
        }

        currentSuggestions.Clear();
        RefreshSuggestionList();
        UpdatePidLabels();

        MessageBox.Show("Sugestões aplicadas com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SimulationCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width <= 0 || e.NewSize.Height <= 0)
        {
            return;
        }

        simulationEngine.ResizeCanvas(e.NewSize.Width, e.NewSize.Height);
        RenderSimulation();
    }

    private void RefreshUi()
    {
        UpdatePidLabels();

        ModeValueText.Text = simulationEngine.GetModeStatus();
        StatusText.Text = simulationEngine.GetStatusInfo();
        DiagnosticText.Text = $"Diagnóstico: {simulationEngine.GetDiagnostic()}";
        LeftVelText.Text = $"Vel Esq: {simulationEngine.Robot.VelLeft:F1} px/ms";
        RightVelText.Text = $"Vel Dir: {simulationEngine.Robot.VelRight:F1} px/ms";
        ErrorText.Text = $"Erro: {simulationEngine.Robot.TrackingError:F2} px";
        CorrectionText.Text = $"Correção PID: {simulationEngine.PIDController.LastCorrection:F2}";
        PWMLeftBar.Value = simulationEngine.LeftPWMDuty;
        PWMRightBar.Value = simulationEngine.RightPWMDuty;
        PWMLeftValue.Text = $"{simulationEngine.LeftPWMDuty:F1}%";
        PWMRightValue.Text = $"{simulationEngine.RightPWMDuty:F1}%";

        LapStatusText.Text = simulationEngine.GetLapStatus();
        ProgressText.Text = $"Progresso: {(int)(simulationEngine.TrackProgress * 100)}%";

        var currentLapTime = simulationEngine.LapManager.GetCurrentLapElapsedTime();
        CurrentLapTimeText.Text = currentLapTime > 0
            ? $"Volta atual: {currentLapTime:F2}ms"
            : "Volta atual: --";

        var bestLap = simulationEngine.LapManager.GetBestLap();
        BestLapText.Text = bestLap is not null
            ? $"Melhor volta: {bestLap.LapTime:F2}ms (Volta {bestLap.LapNumber})"
            : "Melhor volta: --";
    }

    private void UpdatePidLabels()
    {
        KpValueText.Text = $"KP: {simulationEngine.PIDController.KP:F2}";
        KdValueText.Text = $"KD: {simulationEngine.PIDController.KD:F2}";
        BaseVelValueText.Text = $"Vel Base: {simulationEngine.BaseVelocity:F0} px/ms";
    }

    private void UpdateModeUi()
    {
        var pidLocked = simulationEngine.PidAdjustmentsLocked;
        KpSlider.IsEnabled = !pidLocked;
        KdSlider.IsEnabled = !pidLocked;
        BaseVelSlider.IsEnabled = !pidLocked;
    }

    private void UpdateLapHistory()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Histórico de voltas:");
        sb.AppendLine("====================");

        var laps = simulationEngine.LapManager.Laps;
        if (laps.Count == 0)
        {
            sb.AppendLine("Nenhuma volta completada");
        }
        else
        {
            foreach (var lap in laps)
            {
                sb.AppendLine($"Volta {lap.LapNumber}: {lap.LapTime:F2}ms");
            }

            sb.AppendLine("====================");
            var bestLap = simulationEngine.LapManager.GetBestLap();
            var average = simulationEngine.LapManager.GetAverageLapTime();
            if (bestLap is not null)
            {
                sb.AppendLine($"Melhor: {bestLap.LapTime:F2}ms");
                sb.AppendLine($"Média:  {average:F2}ms");
            }
        }

        LapHistoryTextBox.Text = sb.ToString();
        LapHistoryTextBox.ScrollToEnd();
    }

    private void UpdateSuggestions(int voltasCompletas)
    {
        currentSuggestions.Clear();
        currentSuggestions.AddRange(diagnostico.AnalisarComportamento(voltasCompletas));
        RefreshSuggestionList();
    }

    private void RefreshSuggestionList()
    {
        SuggestionsListBox.Items.Clear();

        if (currentSuggestions.Count == 0)
        {
            SuggestionsListBox.Items.Add("Sem sugestões");
            return;
        }

        foreach (var sugestao in currentSuggestions)
        {
            var acao = sugestao.Acao == PIDDiagnostico.Acao.Aumentar ? "↑" : "↓";
            var parametro = sugestao.Parametro switch
            {
                PIDDiagnostico.Parametro.Kp => "KP",
                PIDDiagnostico.Parametro.Kd => "KD",
                PIDDiagnostico.Parametro.VelBase => "VEL",
                _ => "?"
            };

            SuggestionsListBox.Items.Add($"{parametro} {acao} ({sugestao.PercentualSugerido:F0}%) - {sugestao.Motivo}");
        }
    }

    private void RenderSimulation()
    {
        SimulationCanvas.Children.Clear();

        DrawGrid();
        DrawTrack();
        DrawRobot();
        DrawSensors();
        DrawTelemetryGraph();
    }

    private void DrawGrid()
    {
        const int spacing = 50;
        var brush = new SolidColorBrush(Color.FromRgb(230, 230, 230));

        for (var x = 0.0; x < SimulationCanvas.ActualWidth; x += spacing)
        {
            SimulationCanvas.Children.Add(new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = SimulationCanvas.ActualHeight,
                Stroke = brush,
                StrokeThickness = 0.5
            });
        }

        for (var y = 0.0; y < SimulationCanvas.ActualHeight; y += spacing)
        {
            SimulationCanvas.Children.Add(new Line
            {
                X1 = 0,
                Y1 = y,
                X2 = SimulationCanvas.ActualWidth,
                Y2 = y,
                Stroke = brush,
                StrokeThickness = 0.5
            });
        }
    }

    private void DrawTrack()
    {
        if (simulationEngine.Track.Points.Count < 2)
        {
            return;
        }

        var trackPoints = new PointCollection(simulationEngine.Track.Points.Select(p => new Point(p.X, p.Y)));
        if (trackPoints.Count > 0)
        {
            trackPoints.Add(trackPoints[0]);
        }

        SimulationCanvas.Children.Add(new Polyline
        {
            Points = trackPoints,
            Stroke = Brushes.Black,
            StrokeThickness = simulationEngine.Track.LineWidth,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        });

        var (x1, y1, x2, y2) = simulationEngine.Track.GetStartLine();
        SimulationCanvas.Children.Add(new Line
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            Stroke = Brushes.Red,
            StrokeThickness = Math.Max(2, simulationEngine.Track.LineWidth - 2),
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        });

        var midX = (x1 + x2) / 2;
        var midY = (y1 + y2) / 2;
        var highlight = new Rectangle
        {
            Width = 30,
            Height = 30,
            Fill = new SolidColorBrush(Color.FromArgb(140, 255, 0, 0))
        };

        Canvas.SetLeft(highlight, midX - 15);
        Canvas.SetTop(highlight, midY - 15);
        SimulationCanvas.Children.Add(highlight);
    }

    private void DrawRobot()
    {
        var robot = simulationEngine.Robot;
        var robotDiameter = robot.Radius * 2;

        var body = new Ellipse
        {
            Width = robotDiameter,
            Height = robotDiameter,
            Fill = Brushes.Blue,
            Stroke = Brushes.DarkBlue,
            StrokeThickness = 2
        };
        Canvas.SetLeft(body, robot.X - robot.Radius);
        Canvas.SetTop(body, robot.Y - robot.Radius);
        SimulationCanvas.Children.Add(body);

        var arrowLength = robot.Radius + 10;
        var arrowX = robot.X + arrowLength * Math.Cos(robot.Angle);
        var arrowY = robot.Y + arrowLength * Math.Sin(robot.Angle);

        SimulationCanvas.Children.Add(new Line
        {
            X1 = robot.X,
            Y1 = robot.Y,
            X2 = arrowX,
            Y2 = arrowY,
            Stroke = Brushes.White,
            StrokeThickness = 3
        });
    }

    private void DrawSensors()
    {
        var robot = simulationEngine.Robot;

        foreach (var sensor in robot.Sensors)
        {
            var ellipse = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = sensor.DetectsLine ? Brushes.Lime : Brushes.Orange,
                Stroke = Brushes.DarkOrange,
                StrokeThickness = 1.5
            };

            Canvas.SetLeft(ellipse, sensor.X - 5);
            Canvas.SetTop(ellipse, sensor.Y - 5);
            SimulationCanvas.Children.Add(ellipse);
        }
    }

    private void DrawTelemetryGraph()
    {
        const double graphWidth = 240;
        const double graphHeight = 120;
        var graphLeft = Math.Max(10, SimulationCanvas.ActualWidth - 250);
        const double graphTop = 10;

        var graphBackground = new Rectangle
        {
            Width = graphWidth,
            Height = graphHeight,
            Fill = new SolidColorBrush(Color.FromArgb(230, 240, 250, 255)),
            Stroke = Brushes.Gray,
            StrokeThickness = 1
        };
        Canvas.SetLeft(graphBackground, graphLeft);
        Canvas.SetTop(graphBackground, graphTop);
        SimulationCanvas.Children.Add(graphBackground);

        DrawHistoryPolyline(simulationEngine.ErrorHistory, Brushes.Red, graphLeft + 5, graphTop + 5, graphWidth - 10, graphHeight - 30, -50, 50);
        DrawHistoryPolyline(simulationEngine.CorrectionHistory, Brushes.Blue, graphLeft + 5, graphTop + 5, graphWidth - 10, graphHeight - 30, -50, 50);

        var label = new TextBlock
        {
            Text = "Erro(R) Corr(B)",
            FontSize = 11,
            Foreground = Brushes.Black
        };
        Canvas.SetLeft(label, graphLeft + 5);
        Canvas.SetTop(label, graphTop + graphHeight - 20);
        SimulationCanvas.Children.Add(label);
    }

    private void DrawHistoryPolyline(
        List<double> history,
        Brush stroke,
        double x,
        double y,
        double width,
        double height,
        double minValue,
        double maxValue)
    {
        if (history.Count < 2)
        {
            return;
        }

        var points = new PointCollection();
        var valueRange = maxValue - minValue;

        for (var i = 0; i < history.Count; i++)
        {
            var px = x + (i / (double)history.Count) * width;
            var normalized = (history[i] - minValue) / valueRange;
            var py = y + height - normalized * height;
            py = Math.Clamp(py, y, y + height);
            points.Add(new Point(px, py));
        }

        SimulationCanvas.Children.Add(new Polyline
        {
            Points = points,
            Stroke = stroke,
            StrokeThickness = 1
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        simulationTimer.Stop();
        simulationEngine.LapManager.LapCompleted -= OnLapCompleted;
        simulationEngine.LapManager.RaceFinished -= OnRaceFinished;
        base.OnClosed(e);
    }
}
