namespace Simulador_dot.Controllers;

/// <summary>
/// Controlador PID com anti-windup para corriger o rastreamento da pista
/// </summary>
public class PIDController
{
    // Parâmetros ajustáveis
    public double KP { get; set; } = 2.0;      // Ganho proporcional
    public double KD { get; set; } = 0.9;      // Ganho derivativo

    // Parâmetros base para escala adaptativa
    public double KP_Base { get; set; } = 2.0;
    public double KD_Base { get; set; } = 0.9;
    public double ReferenceVelocity { get; set; } = 200.0; // velocidade de referência (200 = estável)
    public bool AdaptiveMode { get; set; } = true; // ativa/desativa escala automática

    // Histórico para cálculo derivativo
    private double lastError = 0;

    // Saída do controlador
    public double LastCorrection { get; set; } = 0;

    /// <summary>
    /// Calcula a correção PID baseado no erro de rastreamento
    /// </summary>
    /// <param name="error">Erro atual (distância em relação à pista)</param>
    /// <param name="deltaTime">Tempo desde última atualização (em ms)</param>
    /// <returns>Correção a ser aplicada aos motores</returns>
    public double Calculate(double error, double deltaTime)
    {
        // Converter deltaTime de ms para segundos
        double dt = deltaTime / 1000.0;

        // Termo Proporcional: P = KP * error
        double proportional = KP * error;

        // Termo Derivativo: D = KD * (error - lastError) / dt
        double derivative = 0;
        if (dt > 0)
        {
            // basic PD derivative
            derivative = KD * (error - lastError) / dt;
        }

        // nova lógica PD: limitar magnitude para evitar correções extremas
        var correction = proportional + derivative;
        // opcional: usar limite absoluto razoável
        const double maxCorrection = 1000; // evita valores absurdos
        correction = Math.Clamp(correction, -maxCorrection, maxCorrection);

        LastCorrection = correction;

        // Armazenar erro para próxima iteração (para derivativo)
        lastError = error;

        return LastCorrection;
    }

    /// <summary>
    /// Reseta o controlador (histórico de erro)
    /// </summary>
    public void Reset()
    {
        lastError = 0;
        LastCorrection = 0;
    }

    /// <summary>
    /// Detecta oscilação (variações rápidas de erro)
    /// </summary>
    public bool IsOscillating(double error, double threshold = 5.0)
    {
        return Math.Abs(error - lastError) > threshold;
    }

    /// <summary>
    /// Diagnóstico simplificado para o controlador PD
    /// </summary>
    public string GetDiagnostic(double error)
    {
        if (Math.Abs(error) < 2)
            return "✓ Rastreamento bom";

        if (IsOscillating(error, 3))
            return "⚠ Oscilação → reduzir KP ou aumentar KD";

        if (Math.Abs(error) > 10)
            return "⚠ Resposta lenta → aumentar KP";

        return "⚠ Ajuste fino necessário";
    }

    /// <summary>
    /// Autoajuste leve para PD (mantido para compatibilidade, mas pouco usado)
    /// </summary>
    public void AutoAjustar(double error, bool _) // old parameter kept for signature
    {
        if (IsOscillating(error, 3))
        {
            KP *= 0.9;  // reduzir oscilação
            KD *= 1.05; // aumentar amortecimento
        }
    }

    /// <summary>
    /// Calcula ganhos adaptativos baseado na velocidade usando escala não-linear.
    /// Fórmula: 
    /// KP_adaptativo = KP_base / v^0.6  (reduz com velocidade)
    /// KD_adaptativo = KD_base * v^0.4  (aumenta levemente com velocidade)
    /// </summary>
    public void ApplyAdaptiveScaling(double currentVelocity)
    {
        if (!AdaptiveMode || currentVelocity <= 0)
            return;

        // Normaliza velocidade em relação à velocidade de referência
        double velocidadeRelativa = currentVelocity / ReferenceVelocity;

        // Aplica escala não-linear (mais conservadora em alta velocidade)
        double scalingFactor_KP = 1.0 / Math.Pow(velocidadeRelativa, 0.6);
        double scalingFactor_KD = Math.Pow(velocidadeRelativa, 0.4);

        // Limita os fatores para evitar variações extremas
        scalingFactor_KP = Math.Clamp(scalingFactor_KP, 0.3, 1.5);
        scalingFactor_KD = Math.Clamp(scalingFactor_KD, 0.5, 2.0);

        // Aplica os ganhos adaptativos
        KP = KP_Base * scalingFactor_KP;
        KD = KD_Base * scalingFactor_KD;
    }

    /// <summary>
    /// Retorna um resumo dos ganhos atuais (base vs adaptativo)
    /// </summary>
    public string GetGainsSummary(double currentVelocity)
    {
        if (!AdaptiveMode)
            return $"Modo FIXO: KP={KP:F2} KD={KD:F2}";

        return $"Modo ADAPTATIVO: KP={KP:F2} ({KP_Base:F2}×{KP/Math.Max(0.1, KP_Base):F2}) | " +
               $"KD={KD:F2} ({KD_Base:F2}×{KD/Math.Max(0.1, KD_Base):F2}) @ {currentVelocity:F0}px/ms";
    }
}
