namespace Simulador_dot.Models;

/// <summary>
/// Representa um registro de uma volta completada
/// </summary>
public class LapRecord
{
    public int LapNumber { get; set; }
    public double LapTime { get; set; } // em milissegundos
    public double TotalTime { get; set; } // tempo acumulado
    public DateTime RecordedAt { get; set; }

    public LapRecord(int lapNumber, double lapTime, double totalTime)
    {
        LapNumber = lapNumber;
        LapTime = lapTime;
        TotalTime = totalTime;
        RecordedAt = DateTime.Now;
    }

    public override string ToString()
    {
        return $"Volta {LapNumber}: {LapTime:F2}ms (Total: {TotalTime:F2}ms)";
    }
}

/// <summary>
/// Gerenciador de voltas e cronometragem com detecção clara de cruzamento
/// </summary>
public class LapManager
{
    // Histórico de voltas
    public List<LapRecord> Laps { get; private set; } = new();

    // Estado atual
    public int CurrentLap { get; private set; } = 0;
    public double CurrentLapStartTime { get; private set; } = 0;
    public double TotalElapsedTime { get; private set; } = 0;
    public bool HasCrossedStartLine { get; private set; } = false;
    public bool IsRaceFinished { get; private set; } = false;

    // Limite de voltas (0 = ilimitado)
    public int LapLimit { get; set; } = 0;

    // Detecção de cruzamento conforme doc técnica
    private bool crossedStartLine = false;      // Indica se já cruzou
    private double previousT = 0;               // Parâmetro anterior
    private DateTime lapStartTime;              // Tempo de início da volta

    public event Action<LapRecord>? LapCompleted;
    public event Action? RaceStarted;
    public event Action? RaceFinished;

    public LapManager()
    {
        Reset();
    }

    /// <summary>
    /// Atualiza o estado baseado na posição na pista
    /// Detecta início da corrida quando sai de T=0.0 (linha vermelha)
    /// Detecta cruzamento de volta quando faz wraparound
    /// </summary>
    public void Update(double currentT, double deltaTime)
    {
        // Se a corrida já terminou, não processar mais nada
        if (IsRaceFinished)
            return;

        TotalElapsedTime += deltaTime;

        // Normalizar T para intervalo [0, 1]
        currentT = ((currentT % 1.0) + 1.0) % 1.0;

        // PRIMEIRA DETECÇÃO: Robô ESTÁ em T=0.0 (linha vermelha)
        // Detecta quando T está exatamente na linha de partida (T < 0.05 ou T > 0.95)
        // Inicia contagem de volta e tempo IMEDIATAMENTE, sem pausas
        if (!crossedStartLine && (currentT < 0.05 || currentT > 0.95))
        {
            Console.WriteLine("\n╔═══════════════════════════════════════════════════════╗");
            Console.WriteLine("║  🚀 INÍCIO DA CORRIDA - T=0.0 (Linha Vermelha)      ║");
            Console.WriteLine($"║  📍 Posição do robô: T={currentT:F4}                  ║");
            Console.WriteLine("║  ⏱️  Contagem de tempo e volta INICIADA              ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════╝\n");

            crossedStartLine = true;
            HasCrossedStartLine = true;
            CurrentLap = 1;
            CurrentLapStartTime = TotalElapsedTime;
            lapStartTime = DateTime.Now;

            // Disparar evento de início (sem pausar)
            RaceStarted?.Invoke();
        }

        // DETECÇÃO DE VOLTA COMPLETA: Cruzamento EXATO da linha vermelha (T=0.0)
        // TRÊS ESTRATÉGIAS:
        // 1. Wraparound normal: previousT > 0.8 && currentT < 0.2 (estava longe, voltou perto)
        // 2. Salto negativo grande: previousT > 0.95 && currentT < 0.05 (T vai de ~1.0 para ~0.0)
        // 3. Salto positivo grande: previousT < 0.05 && currentT > 0.95 (T vai de ~0.0 para ~1.0)
        //    Ambos os saltos indicam passagem pela linha vermelha (T=0.0 = T=1.0 na pista fechada)

        bool crossedByWraparound = (previousT > 0.8 && currentT < 0.2);
        bool crossedByNegativeJump = (previousT > 0.95 && currentT < 0.05);
        bool crossedByPositiveJump = (previousT < 0.05 && currentT > 0.95);
        bool crossedRedLine = (crossedByWraparound || crossedByNegativeJump || crossedByPositiveJump) && crossedStartLine;

        if (crossedRedLine)
        {
            // Verificar se passou tempo suficiente desde o início da volta
            // Uma volta real não pode ter menos de 3 segundos
            double lapTime = TotalElapsedTime - CurrentLapStartTime;

            if (lapTime < 3000) // Menos de 3 segundos - ignora salto inicial
            {
                // Ignora detecção - é o salto que acontece quando robô sai de T=0.0 no início
                previousT = currentT;
                return;
            }

            // ═══════════════════════════════════════════════════════════════════
            // INTERPOLAÇÃO LINEAR: Calcula EXATAMENTE quando T cruzou 0.0
            // ═══════════════════════════════════════════════════════════════════
            double exactCrossingTime = TotalElapsedTime;

            if (crossedByNegativeJump)
            {
                // T foi de ~0.98 para ~0.02, passou por 0.0
                // Interpolar para encontrar quando T = 0.0
                // t_exato = t_atual - deltaTime * (currentT / (currentT + (1.0 - previousT)))
                double distanceFromZero = currentT;
                double distanceBeforeZero = 1.0 - previousT;
                double totalDistance = distanceFromZero + distanceBeforeZero;
                double fractionAfterCrossing = distanceFromZero / totalDistance;
                exactCrossingTime = TotalElapsedTime - (deltaTime * fractionAfterCrossing);
            }
            else if (crossedByPositiveJump)
            {
                // T foi de ~0.02 para ~0.98, passou por 1.0 (equivalente a 0.0)
                // Interpolar para encontrar quando T = 1.0
                double distanceToOne = 1.0 - currentT;
                double distanceFromPrevious = previousT;
                double totalDistance = distanceToOne + distanceFromPrevious;
                double fractionAfterCrossing = distanceToOne / totalDistance;
                exactCrossingTime = TotalElapsedTime - (deltaTime * fractionAfterCrossing);
            }
            else if (crossedByWraparound)
            {
                // T foi de >0.8 para <0.2, passou por 0.0/1.0
                double distanceFromZero = currentT;
                double distanceBeforeZero = 1.0 - previousT;
                double totalDistance = distanceFromZero + distanceBeforeZero;
                double fractionAfterCrossing = distanceFromZero / totalDistance;
                exactCrossingTime = TotalElapsedTime - (deltaTime * fractionAfterCrossing);
            }

            // Ajustar TotalElapsedTime para o momento EXATO do cruzamento em T=0.0
            double originalTime = TotalElapsedTime;
            TotalElapsedTime = exactCrossingTime;

            // Log de detecção mostrando os valores exatos de T
            string detectionType = crossedByNegativeJump ? "Salto Negativo" :
                                   crossedByPositiveJump ? "Salto Positivo" : "Wraparound";
            Console.WriteLine($"🏁 Volta detectada EXATAMENTE em T=0.0 | Tipo: {detectionType}");
            Console.WriteLine($"   T anterior: {previousT:F4} → T atual: {currentT:F4}");
            Console.WriteLine($"   Tempo interpolado: {exactCrossingTime:F2}ms (ajustado -{(originalTime - exactCrossingTime):F2}ms)");

            // Completou uma volta válida (voltou exatamente à linha vermelha T=0.0)
            CompleteLap(currentT);
            lapStartTime = DateTime.Now;

            // Restaurar tempo real após registro
            TotalElapsedTime = originalTime;
        }

        previousT = currentT;
    }

    /// <summary>
    /// Registra uma volta completada
    /// </summary>
    private void CompleteLap(double currentT)
    {
        // Calcular tempo da volta que acabou de completar
        double lapTime = TotalElapsedTime - CurrentLapStartTime;
        var record = new LapRecord(CurrentLap, lapTime, TotalElapsedTime);
        Laps.Add(record);

        Console.WriteLine($"📊 Volta {CurrentLap} registrada: {lapTime:F2}ms (Total: {TotalElapsedTime:F2}ms)");

        // Notificar volta completada
        LapCompleted?.Invoke(record);

        // Verificar se atingiu o limite de voltas ANTES de incrementar
        if (LapLimit > 0 && CurrentLap >= LapLimit)
        {
            // Marcar corrida como finalizada IMEDIATAMENTE
            IsRaceFinished = true;

            Console.WriteLine($"\n🏁 CORRIDA FINALIZADA! {CurrentLap} voltas completadas.");
            Console.WriteLine("╔═══════════════════════════════════════════════════════╗");
            Console.WriteLine("║  🛑 ROBÔ SERÁ POSICIONADO NA LINHA VERMELHA         ║");
            Console.WriteLine($"║  📍 Posição final obrigatória: T=0.0000              ║");
            Console.WriteLine($"║  ⏱️  Tempo total: {TotalElapsedTime:F2}ms             ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════╝\n");
            RaceFinished?.Invoke();
            return; // Não incrementa mais, corrida acabou
        }

        // Iniciar próxima volta (incrementa para a próxima)
        CurrentLap++;
        CurrentLapStartTime = TotalElapsedTime;
    }

    /// <summary>
    /// Retorna o tempo decorrido da volta atual
    /// </summary>
    public double GetCurrentLapElapsedTime()
    {
        if (!HasCrossedStartLine)
            return 0;

        return TotalElapsedTime - CurrentLapStartTime;
    }

    /// <summary>
    /// Retorna a volta mais rápida
    /// </summary>
    public LapRecord? GetBestLap()
    {
        return Laps.Count > 0 ? Laps.OrderBy(l => l.LapTime).First() : null;
    }

    /// <summary>
    /// Retorna a volta mais lenta
    /// </summary>
    public LapRecord? GetWorstLap()
    {
        return Laps.Count > 0 ? Laps.OrderByDescending(l => l.LapTime).First() : null;
    }

    /// <summary>
    /// Retorna o tempo médio das voltas
    /// </summary>
    public double GetAverageLapTime()
    {
        return Laps.Count > 0 ? Laps.Average(l => l.LapTime) : 0;
    }

    /// <summary>
    /// Reseta o gerenciador de voltas
    /// </summary>
    public void Reset()
    {
        Laps.Clear();
        CurrentLap = 0;
        CurrentLapStartTime = 0;
        TotalElapsedTime = 0;
        HasCrossedStartLine = false;
        IsRaceFinished = false;
        crossedStartLine = false;
        previousT = 0;
        lapStartTime = DateTime.Now;
    }

    /// <summary>
    /// Retorna status formatado
    /// </summary>
    public string GetStatus()
    {
        if (!HasCrossedStartLine)
            return "Aguardando início...";

        string lapInfo = $"Volta {CurrentLap}";
        if (LapLimit > 0)
            lapInfo += $" / {LapLimit}";

        string timeInfo = $"Tempo: {TotalElapsedTime:F1}ms";
        if (Laps.Count > 0)
            timeInfo += $" | Melhor: {GetBestLap()?.LapTime:F1}ms";

        return $"{lapInfo} | {timeInfo}";
    }
}
