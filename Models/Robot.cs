namespace Simulador_dot.Models;

/// <summary>
/// Representa o robô com suas propriedades físicas e dinâmicas
/// </summary>
public class Robot
{
    // Posição e orientação
    public double X { get; set; } = 400;     // Coordenada X (pixels)
    public double Y { get; set; } = 300;     // Coordenada Y (pixels)
    public double Angle { get; set; } = 0;   // Ângulo em radianos (0 = direita)

    // Dinâmica
    public double VelLeft { get; set; } = 0;   // Velocidade motor esquerdo (pixels/ms)
    public double VelRight { get; set; } = 0;  // Velocidade motor direito (pixels/ms)
    public double LinearVel { get; set; } = 0; // Velocidade linear (v)
    public double AngularVel { get; set; } = 0; // Velocidade angular (ω)

    // Propriedades físicas
    public double Radius { get; set; } = 15;   // Raio do corpo (pixels)
    public double AxleLength { get; set; } = 30; // Distância entre rodas (L - pixels)

    // Sensores virtuais (9 sensores na frente)
    public VirtualSensor[] Sensors { get; private set; } = new VirtualSensor[9];

    // Erro de rastreamento (para diagnóstico)
    public double TrackingError { get; set; } = 0;

    public Robot()
    {
        InitializeSensors();
        // Atualizar posição dos sensores na inicialização
        UpdateSensorPositions();
    }

    /// <summary>
    /// Calcula posição da linha usando média ponderada dos sensores (doc técnica)
    /// Retorna NaN se nenhum sensor detectar linha
    /// </summary>
    public double CalcularPosicaoLinha()
    {
        int[] pesos = { -4, -3, -2, -1, 0, 1, 2, 3, 4 };
        double somaPos = 0;
        int somaAtivos = 0;

        for (int i = 0; i < Sensors.Length; i++)
        {
            if (Sensors[i].DetectsLine)
            {
                somaPos += pesos[i];
                somaAtivos++;
            }
        }

        if (somaAtivos == 0) return double.NaN; // linha perdida
        return somaPos / somaAtivos;
    }

    private void InitializeSensors()
    {
        // 9 sensores alinhados na frente do robô
        // -4 até +4 ângulos de offset
        for (int i = 0; i < 9; i++)
        {
            Sensors[i] = new VirtualSensor(index: i, totalSensors: 9);
        }
    }

    /// <summary>
    /// Atualiza posição e orientação baseado em cinemática diferencial
    /// </summary>
    public void Update(double deltaTime) // deltaTime em ms
    {
        // Cinemática diferencial
        // v = (velEsq + velDir) / 2
        LinearVel = (VelLeft + VelRight) / 2.0;

        // ω = (velDir - velEsq) / L
        AngularVel = (VelRight - VelLeft) / AxleLength;

        // Converter deltaTime de ms para segundos para cálculo físico
        double dt = deltaTime / 1000.0;

        // Atualizar ângulo
        Angle += AngularVel * dt;

        // Atualizar posição (movimento em coordenadas globais)
        X += LinearVel * Math.Cos(Angle) * dt;
        Y += LinearVel * Math.Sin(Angle) * dt;

        // Atualizar posições dos sensores
        UpdateSensorPositions();
    }

    private void UpdateSensorPositions()
    {
        for (int i = 0; i < Sensors.Length; i++)
        {
            Sensors[i].UpdatePosition(X, Y, Angle);
        }
    }

    /// <summary>
    /// Método público para atualizar posição dos sensores
    /// </summary>
    public void RefreshSensorPositions()
    {
        UpdateSensorPositions();
    }

    /// <summary>
    /// Retorna a posição de um sensor em coordenadas globais
    /// </summary>
    public (double sensorX, double sensorY) GetSensorPosition(int index)
    {
        if (index < 0 || index >= Sensors.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        return (Sensors[index].X, Sensors[index].Y);
    }

    public void Reset()
    {
        X = 400;
        Y = 300;
        Angle = 0;
        VelLeft = 0;
        VelRight = 0;
        LinearVel = 0;
        AngularVel = 0;
        TrackingError = 0;
        // Atualizar posição dos sensores após reset
        UpdateSensorPositions();
    }
}
