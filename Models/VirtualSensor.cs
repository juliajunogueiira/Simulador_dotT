namespace Simulador_dot.Models;

/// <summary>
/// Representa um sensor virtual que detecta a posição relativa à pista
/// </summary>
public class VirtualSensor
{
    public int Index { get; set; }
    public int TotalSensors { get; set; }

    // Posição em coordenadas globais
    public double X { get; set; }
    public double Y { get; set; }

    // Distância do sensor relativamente à pista (para detecção)
    public double DistanceToLine { get; set; } = 0;

    // True se detecta a linha preta
    public bool DetectsLine { get; set; } = false;

    // Raio de detecção
    public double DetectionRadius { get; set; } = 5;

    public VirtualSensor(int index, int totalSensors)
    {
        Index = index;
        TotalSensors = totalSensors;
    }

    /// <summary>
    /// Atualiza a posição do sensor baseado na posição e ângulo do robô
    /// </summary>
    public void UpdatePosition(double robotX, double robotY, double robotAngle)
    {
        // Sensores são distribuídos em um arco na frente do robô
        // -4 a +4 índices mapeados para -60° a +60°
        double sensorOffset = (Index - (TotalSensors - 1) / 2.0) * 15; // ±15° entre sensores
        double sensorAngle = robotAngle + (sensorOffset * Math.PI / 180.0);

        // Distância da frente do robô para o sensor (pixels)
        double distanceFromRobot = 25;

        X = robotX + distanceFromRobot * Math.Cos(sensorAngle);
        Y = robotY + distanceFromRobot * Math.Sin(sensorAngle);
    }

    /// <summary>
    /// Verifica detecção baseado em imagem (deve ser chamado com a imagem da pista)
    /// </summary>
    public void UpdateDetection(int pixelColor)
    {
        // Se o pixel é preto (linha da pista), detecta
        // Vamos considerar preto como RGB < (50, 50, 50)
        int r = (pixelColor >> 16) & 0xFF;
        int g = (pixelColor >> 8) & 0xFF;
        int b = pixelColor & 0xFF;

        DetectsLine = (r < 50 && g < 50 && b < 50);
    }
}
