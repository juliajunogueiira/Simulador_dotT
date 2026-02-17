namespace Simulador_dot.Controllers;

/// <summary>
/// Controlador PID com anti-windup para corriger o rastreamento da pista
/// </summary>
public class PIDController
{
    // Parâmetros ajustáveis
    public double KP { get; set; } = 3.2;      // Ganho proporcional
    public double KI { get; set; } = 0.16;     // Ganho integral
    public double KD { get; set; } = 0.85;     // Ganho derivativo
    public double KSLIP { get; set; } = 0.08;  // Fator de derrapagem

    // Histórico para cálculo derivativo
    private double lastError = 0;
    private double accumulatedError = 0;

    // Anti-windup
    public double IntegralLimit { get; set; } = 50; // Limite da integral

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

        // Termo Integral: I = KI * Σ(error * dt) com anti-windup
        accumulatedError += error * dt;

        // Anti-windup: limitar integral
        if (accumulatedError > IntegralLimit)
            accumulatedError = IntegralLimit;
        else if (accumulatedError < -IntegralLimit)
            accumulatedError = -IntegralLimit;

        double integral = KI * accumulatedError;

        // Termo Derivativo: D = KD * (error - lastError) / dt
        double derivative = 0;
        if (dt > 0)
        {
            derivative = KD * (error - lastError) / dt;
        }

        // Saída PID
        LastCorrection = proportional + integral + derivative;

        // Aplicar KSLIP condicionalmente quando erro > 2.5 (doc técnica)
        if (Math.Abs(error) > 2.5)
        {
            LastCorrection *= KSLIP;
        }

        // Armazenar erro para próxima iteração
        lastError = error;

        return LastCorrection;
    }

    /// <summary>
    /// Reseta o controlador (integral e histórico)
    /// </summary>
    public void Reset()
    {
        lastError = 0;
        accumulatedError = 0;
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
    /// Diagnóstico baseado na documentação técnica
    /// </summary>
    public string GetDiagnostic(double error)
    {
        if (Math.Abs(error) < 2)
            return "✓ Rastreamento bom";

        // Regras da doc técnica:
        if (IsOscillating(error, 3))
            return "⚠ Oscilação → reduzir KP, aumentar KD";

        if (Math.Abs(error) > 10)
            return "⚠ Resposta lenta → aumentar KP";

        if (Math.Abs(error) > 5)
            return "⚠ Derrapagem → aumentar KSLIP";

        return "⚠ Ajuste fino necessário";
    }

    /// <summary>
    /// Autoajuste baseado na documentação técnica
    /// </summary>
    public void AutoAjustar(double error, bool derrapagem)
    {
        if (IsOscillating(error, 3))
        {
            KP *= 0.9;  // Reduz oscilação
            KD *= 1.05; // Aumenta amortecimento
        }

        if (derrapagem)
        {
            KSLIP *= 1.2; // Aumenta KSLIP
        }
    }
}
