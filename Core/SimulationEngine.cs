using Simulador_dot.Models;
using Simulador_dot.Controllers;
using Simulador_dot.Utils;

namespace Simulador_dot.Core;

/// <summary>
/// Motor de simulação que orquestra robô, sensores, pista e controle PID
/// </summary>
public class SimulationEngine
{
    public enum OperationMode
    {
        Standby,
        MotorTestLeft,
        MotorTestRight,
        Calibration,
        FreeRace,
        Test3Laps,
        Official
    }

    // Componentes
    public Robot Robot { get; private set; }
    public PIDController PIDController { get; private set; }
    public Track Track { get; private set; }
    public LapManager LapManager { get; private set; }
    public GraficoDataCollector DataCollector { get; private set; }
    public PWMSimulator PWMSimulator { get; private set; }

    // Configurações
    public double BaseVelocity { get; set; } = 200; // pixels/ms (velocidade base)
    public double UpdateRate { get; set; } = 50;    // ms entre updates

    // Estado da simulação
    public bool IsRunning { get; set; } = false;
    public bool IsPaused { get; set; } = false;

    // Telemetria
    public List<double> ErrorHistory { get; private set; } = new();
    public List<double> CorrectionHistory { get; private set; } = new();
    public List<double> LeftVelHistory { get; private set; } = new();
    public List<double> RightVelHistory { get; private set; } = new();

    // Máximo de pontos no histórico
    private const int MaxHistoryPoints = 500;

    // Progresso na pista (0 a 1)
    public double TrackProgress { get; private set; } = 0;

    // Armazenamento de tempos de voltas para salvar no ranking
    private List<double> lapTimes = new();

    public OperationMode Mode { get; private set; } = OperationMode.Standby;
    public bool PidAdjustmentsLocked { get; private set; } = false;
    public double MotorTestSpeed { get; set; } = 120;
    public double LeftPWMDuty { get; private set; } = 0;
    public double RightPWMDuty { get; private set; } = 0;
    public bool AutoTuneEnabled { get; set; } = true;
    public int AutoTuneIntervalTicks { get; set; } = 10;

    private int autoTuneTickCounter = 0;

    public event EventHandler? SimulationUpdated;

    public SimulationEngine(double canvasWidth = 800, double canvasHeight = 600)
    {
        Robot = new Robot();
        PIDController = new PIDController();
        Track = new Track(canvasWidth, canvasHeight, Track.TrackStyle.Custom);
        LapManager = new LapManager();
        DataCollector = new GraficoDataCollector();
        PWMSimulator = new PWMSimulator();

        // Evento quando uma volta é completada
        LapManager.LapCompleted += (record) =>
        {
            lapTimes.Add(record.LapTime);
        };

        // Evento quando a corrida termina
        LapManager.RaceFinished += async () =>
        {
            // PRIMEIRO: Parar o robô IMEDIATAMENTE (evita movimento extra)
            IsRunning = false;
            IsPaused = false;
            Robot.VelLeft = 0;
            Robot.VelRight = 0;
            Robot.LinearVel = 0;
            Robot.AngularVel = 0;

            // DEPOIS: Posicionar o robô EXATAMENTE na linha vermelha (T=0.0)
            PositionRobotAtStartLine();

            Console.WriteLine($"🏁 Robô parado automaticamente na linha vermelha (T=0.0) após completar {LapManager.CurrentLap} voltas.");
            await SaveRaceRecord();
        };

        // Posicionar robô corretamente no início da pista
        PositionRobotAtStartLine();
    }

    public void SetMode(OperationMode mode)
    {
        Mode = mode;

        LapManager.Reset();

        // Configurar limites de voltas por modo
        switch (Mode)
        {
            case OperationMode.FreeRace:
                LapManager.LapLimit = 5;  // Até 5 voltas na corrida livre
                break;
            case OperationMode.Test3Laps:
                LapManager.LapLimit = 3;  // Exatamente 3 voltas
                break;
            case OperationMode.Official:
                LapManager.LapLimit = 1;  // 1 volta oficial
                break;
            default:
                LapManager.LapLimit = 0;  // Ilimitado para outros modos
                break;
        }

        PidAdjustmentsLocked = Mode == OperationMode.Official;
        AutoTuneEnabled = Mode == OperationMode.FreeRace || Mode == OperationMode.Test3Laps;
        autoTuneTickCounter = 0;

        if (Mode == OperationMode.Standby)
        {
            Robot.VelLeft = 0;
            Robot.VelRight = 0;
        }
    }

    public void ResizeCanvas(double width, double height)
    {
        if (width <= 0 || height <= 0)
            return;

        Track.UpdateCanvas(width, height);

        if (!IsRunning)
        {
            // Reposicionar robô corretamente na pista após redimensionar
            PositionRobotAtStartLine();
        }
    }

    /// <summary>
    /// Inicia a simulação
    /// </summary>
    public void Start()
    {
        IsRunning = true;
        IsPaused = false;
        LapManager.Reset();
        DataCollector.Resetar();
        lapTimes.Clear();
    }

    /// <summary>
    /// Pausa a simulação
    /// </summary>
    public void Pause()
    {
        IsPaused = true;
    }

    /// <summary>
    /// Retoma a simulação
    /// </summary>
    public void Resume()
    {
        IsPaused = false;
    }

    /// <summary>
    /// Pausa a simulação (robô continua de onde parou)
    /// </summary>
    /// (Já existe um Pause() acima)

    /// <summary>
    /// Para a simulação (reseta velocidades mas não reposiciona)
    /// </summary>
    public void Stop()
    {
        IsRunning = false;
        IsPaused = false;
        Robot.VelLeft = 0;
        Robot.VelRight = 0;
        LeftPWMDuty = 0;
        RightPWMDuty = 0;
        Robot.LinearVel = 0;
        Robot.AngularVel = 0;
        PIDController.Reset();
        ClearHistory();
    }


    /// <summary>
    /// Atualiza um ciclo de simulação
    /// </summary>
    public void Update()
    {
        if (!IsRunning || IsPaused)
            return;

        if (Mode == OperationMode.Standby)
        {
            Robot.VelLeft = 0;
            Robot.VelRight = 0;
            LeftPWMDuty = 0;
            RightPWMDuty = 0;
            SimulationUpdated?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (Mode == OperationMode.Calibration)
        {
            UpdateSensorDetections();
            SimulationUpdated?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (Mode == OperationMode.MotorTestLeft)
        {
            ApplyMotorCommand(MotorTestSpeed, 0);
            Robot.Update(UpdateRate);
            SimulationUpdated?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (Mode == OperationMode.MotorTestRight)
        {
            ApplyMotorCommand(0, MotorTestSpeed);
            Robot.Update(UpdateRate);
            SimulationUpdated?.Invoke(this, EventArgs.Empty);
            return;
        }

        // 1. Encontrar ponto mais próximo na pista
        var (closestPoint, closestIndex, distance) = Track.FindClosestPoint(Robot.X, Robot.Y);
        TrackProgress = Track.FindContinuousT(Robot.X, Robot.Y);

        // 2. Calcular erro de rastreamento
        // O erro é a distância perpendicular à pista
        double error = distance;

        // Determinar se está à esquerda ou à direita
        // (ajustar sinal do erro baseado na orientação)
        if (!IsPointToLeftOfTrack(closestPoint, closestIndex))
            error = -error;

        // Limitar erro sensorial
        if (Math.Abs(error) > 100)
            error = Math.Sign(error) * 100;

        Robot.TrackingError = error;

        // 3. Atualizar sensores virtuais
        UpdateSensorDetections();

        // 4. Calcular correção PID
        double pidCorrection = PIDController.Calculate(error, UpdateRate);

        // 4.1 Autoajuste opcional dos ganhos PID durante corrida
        TryAutoTune(error);

        // 5. Aplicar aos motores (motores diferenciais)
        double leftMotorCorrection = pidCorrection * (BaseVelocity / 100.0);
        double rightMotorCorrection = pidCorrection * (BaseVelocity / 100.0);

        var targetLeftSpeed = Math.Clamp(BaseVelocity - leftMotorCorrection, 0, BaseVelocity * 1.5);
        var targetRightSpeed = Math.Clamp(BaseVelocity + rightMotorCorrection, 0, BaseVelocity * 1.5);

        ApplyMotorCommand(targetLeftSpeed, targetRightSpeed);

        // 6. Atualizar física do robô
        Robot.Update(UpdateRate);

        // 7. Atualizar gerenciador de voltas
        LapManager.Update(TrackProgress, UpdateRate);

        // 8. Coletar dados para gráficos
        DataCollector.AdicionarPonto(
            LapManager.TotalElapsedTime,
            error,
            pidCorrection,
            Robot.VelLeft,
            Robot.VelRight
        );

        // 9. Atualizar telemetria
        RecordTelemetry(error, pidCorrection);

        // 10. Notificar listeners
        SimulationUpdated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Determina se um ponto está à esquerda da pista
    /// </summary>
    private bool IsPointToLeftOfTrack(TrackPoint trackPoint, int trackIndex)
    {
        // Obter o próximo ponto na pista para determinar direção
        int nextIndex = (trackIndex + 1) % Track.Points.Count;
        var nextPoint = Track.Points[nextIndex];

        // Vetor tangente da pista
        double tx = nextPoint.X - trackPoint.X;
        double ty = nextPoint.Y - trackPoint.Y;

        // Vetor do ponto da pista para o robô
        double rx = Robot.X - trackPoint.X;
        double ry = Robot.Y - trackPoint.Y;

        // Produto vetorial para determinar lado
        double cross = tx * ry - ty * rx;
        return cross > 0;
    }

    /// <summary>
    /// Atualiza detecções dos sensores virtuais
    /// </summary>
    private void UpdateSensorDetections()
    {
        for (int i = 0; i < Robot.Sensors.Length; i++)
        {
            var sensor = Robot.Sensors[i];
            var (point, index, distance) = Track.FindClosestPoint(sensor.X, sensor.Y);

            sensor.DistanceToLine = distance;
            sensor.DetectsLine = distance <= Track.LineWidth / 2 + 5; // Margem pequena
        }
    }

    /// <summary>
    /// Registra dados de telemetria
    /// </summary>
    private void RecordTelemetry(double error, double correction)
    {
        ErrorHistory.Add(error);
        CorrectionHistory.Add(correction);
        LeftVelHistory.Add(Robot.VelLeft);
        RightVelHistory.Add(Robot.VelRight);

        // Manter histórico limitado
        if (ErrorHistory.Count > MaxHistoryPoints)
        {
            ErrorHistory.RemoveAt(0);
            CorrectionHistory.RemoveAt(0);
            LeftVelHistory.RemoveAt(0);
            RightVelHistory.RemoveAt(0);
        }
    }

    /// <summary>
    /// Converte velocidade alvo em PWM e aplica resposta de motor com inércia.
    /// </summary>
    private void ApplyMotorCommand(double targetLeftSpeed, double targetRightSpeed)
    {
        var maxSpeed = Math.Max(1, BaseVelocity * 1.5);

        LeftPWMDuty = PWMSimulator.SpeedToPWM(targetLeftSpeed, maxSpeed);
        RightPWMDuty = PWMSimulator.SpeedToPWM(targetRightSpeed, maxSpeed);

        var leftTargetFromPwm = PWMSimulator.PWMToSpeed(LeftPWMDuty, maxSpeed);
        var rightTargetFromPwm = PWMSimulator.PWMToSpeed(RightPWMDuty, maxSpeed);

        Robot.VelLeft = PWMSimulator.ApplyInertia(Robot.VelLeft, leftTargetFromPwm, UpdateRate);
        Robot.VelRight = PWMSimulator.ApplyInertia(Robot.VelRight, rightTargetFromPwm, UpdateRate);
    }

    /// <summary>
    /// Aplica autoajuste gradual de ganhos PID durante corrida.
    /// </summary>
    private void TryAutoTune(double error)
    {
        if (!AutoTuneEnabled || PidAdjustmentsLocked)
            return;

        if (Mode != OperationMode.FreeRace && Mode != OperationMode.Test3Laps)
            return;

        autoTuneTickCounter++;
        if (autoTuneTickCounter < Math.Max(1, AutoTuneIntervalTicks))
            return;

        autoTuneTickCounter = 0;

        bool derrapagem = Math.Abs(error) > 5;
        PIDController.AutoAjustar(error, derrapagem);

        PIDController.KP = Math.Clamp(PIDController.KP, 0, 5.0);
        PIDController.KI = Math.Clamp(PIDController.KI, 0, 5.0);
        PIDController.KD = Math.Clamp(PIDController.KD, 0, 5.0);
        PIDController.KSLIP = Math.Clamp(PIDController.KSLIP, 0, 5.0);
    }

    /// <summary>
    /// Limpa histórico de telemetria
    /// </summary>
    public void ClearHistory()
    {
        ErrorHistory.Clear();
        CorrectionHistory.Clear();
        LeftVelHistory.Clear();
        RightVelHistory.Clear();
    }

    /// <summary>
    /// Salva o registro da corrida no ranking (como no React)
    /// </summary>
    private async Task SaveRaceRecord()
    {
        try
        {
            var recorde = new RecordeCorrida
            {
                Data = DateTime.Now,
                Modo = Mode.ToString(),
                TempoTotal = LapManager.TotalElapsedTime,
                TemposVoltas = new List<double>(lapTimes),
                PIDConfig = new PIDConfigRecord
                {
                    Kp = PIDController.KP,
                    Ki = PIDController.KI,
                    Kd = PIDController.KD
                },
                ErroMedio = DataCollector.CalcularErroMedio(),
                VelocidadeMedia = DataCollector.CalcularVelocidadeMedia(),
                Erros = ErrorHistory.ToList(),
                Posicoes = new List<(double x, double y)>()
            };

            await RankingStorage.SalvarCorrida(recorde);
            System.Diagnostics.Debug.WriteLine($"Corrida salva: {recorde.TempoTotal:F2}ms em {recorde.VoltasCompletas} voltas");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Erro ao salvar corrida: {ex.Message}");
        }
    }

    /// <summary>
    /// Modo teste de motor esquerdo
    /// </summary>
    public void TestMotorLeft(double velocity)
    {
        Robot.VelLeft = velocity;
        Robot.VelRight = 0;
        Robot.Update(UpdateRate);
        SimulationUpdated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Modo teste de motor direito
    /// </summary>
    public void TestMotorRight(double velocity)
    {
        Robot.VelLeft = 0;
        Robot.VelRight = velocity;
        Robot.Update(UpdateRate);
        SimulationUpdated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Retorna diagnóstico do sistema
    /// </summary>
    public string GetDiagnostic()
    {
        return PIDController.GetDiagnostic(Robot.TrackingError);
    }

    /// <summary>
    /// Retorna informações de status
    /// </summary>
    public string GetStatusInfo()
    {
        return $"Pos: ({Robot.X:F1}, {Robot.Y:F1}) | " +
               $"Ângulo: {Robot.Angle * 180 / Math.PI:F1}° | " +
               $"Vel: L={Robot.VelLeft:F1} R={Robot.VelRight:F1} | " +
               $"Erro: {Robot.TrackingError:F2}";
    }

    /// <summary>
    /// Retorna status das voltas
    /// </summary>
    public string GetLapStatus()
    {
        return LapManager.GetStatus();
    }

    public string GetModeStatus()
    {
        return Mode switch
        {
            OperationMode.Standby => "Standby",
            OperationMode.MotorTestLeft => "Teste motores (Esq)",
            OperationMode.MotorTestRight => "Teste motores (Dir)",
            OperationMode.Calibration => "Calibracao",
            OperationMode.FreeRace => "Corrida livre",
            OperationMode.Test3Laps => "Teste 3 voltas",
            OperationMode.Official => "Oficial",
            _ => "Standby"
        };
    }

    /// <summary>
    /// Posiciona o robô corretamente no início da pista (sobre a linha vermelha)
    /// </summary>
    public void PositionRobotAtStartLine()
    {
        // A linha vermelha está em t=0
        // Posicionar robô NO CENTRO DA LINHA VERMELHA (t=0)

        var pointRobot = Track.GetPointAtParameter(0.0);      // No centro da linha vermelha
        var pointNext = Track.GetPointAtParameter(0.01);      // Próximo ponto para calcular direção

        // Calcular a direção da tangente da pista (em qual direção ir)
        double dx = pointNext.X - pointRobot.X;
        double dy = pointNext.Y - pointRobot.Y;
        double tangentLength = Math.Sqrt(dx * dx + dy * dy);

        // Normalizar tangente
        if (tangentLength > 0)
        {
            dx /= tangentLength;
            dy /= tangentLength;
        }

        // Posicionar robô exatamente no ponto inicial da pista (sem offset)
        Robot.X = pointRobot.X;
        Robot.Y = pointRobot.Y;

        // Orientar robô para SEGUIR a tangente (apontar para a linha vermelha)
        // Adicionar 180 graus (Math.PI) para fazer meia volta
        Robot.Angle = Math.Atan2(dy, dx) + Math.PI;

        // Atualizar posição dos sensores
        Robot.RefreshSensorPositions();
    }

    /// <summary>
    /// Diagnóstico da pista - removido (não mais necessário)
    /// </summary>
}
