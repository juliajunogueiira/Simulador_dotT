namespace Simulador_dot.Utils;

/// <summary>
/// Simula conversão PWM -> velocidade de motor DC com dead zone e inércia.
/// </summary>
public class PWMSimulator
{
    public double PWMFrequencyHz { get; set; } = 1000;
    public double DeadZonePercent { get; set; } = 15;
    public double MaxDutyCyclePercent { get; set; } = 100;
    public double MotorCurveExponent { get; set; } = 1.2;

    // Limite de variação de velocidade por ms (simulação de aceleração/inércia)
    public double AccelerationPerMs { get; set; } = 3.0;

    /// <summary>
    /// Converte duty cycle PWM (%) em velocidade efetiva do motor.
    /// </summary>
    public double PWMToSpeed(double dutyCyclePercent, double maxSpeed)
    {
        dutyCyclePercent = Math.Clamp(dutyCyclePercent, 0, MaxDutyCyclePercent);

        if (dutyCyclePercent < DeadZonePercent)
            return 0;

        var normalizedDuty = (dutyCyclePercent - DeadZonePercent) / (MaxDutyCyclePercent - DeadZonePercent);
        return maxSpeed * Math.Pow(normalizedDuty, MotorCurveExponent);
    }

    /// <summary>
    /// Converte velocidade desejada para duty cycle PWM (%).
    /// </summary>
    public double SpeedToPWM(double targetSpeed, double maxSpeed)
    {
        if (maxSpeed <= 0)
            return 0;

        var normalized = Math.Clamp(targetSpeed / maxSpeed, 0, 1);
        return normalized * MaxDutyCyclePercent;
    }

    /// <summary>
    /// Aplica inércia com limite de aceleração.
    /// </summary>
    public double ApplyInertia(double currentSpeed, double targetSpeed, double deltaTimeMs)
    {
        var maxChange = AccelerationPerMs * deltaTimeMs;
        var diff = targetSpeed - currentSpeed;

        if (Math.Abs(diff) <= maxChange)
            return targetSpeed;

        return currentSpeed + Math.Sign(diff) * maxChange;
    }
}
