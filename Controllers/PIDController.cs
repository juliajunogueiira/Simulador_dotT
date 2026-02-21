namespace Simulador_dot.Controllers;

/// <summary>
/// Controlador PID com anti-windup para corriger o rastreamento da pista
/// </summary>
public class PIDController
{
    // Parâmetros ajustáveis
    public double KP { get; set; } = 3.2;      // Ganho proporcional
    public double KD { get; set; } = 0.85;     // Ganho derivativo

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
}
