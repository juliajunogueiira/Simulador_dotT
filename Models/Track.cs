namespace Simulador_dot.Models;

/// <summary>
/// Representa um ponto na pista com posição e dados relacionados
/// </summary>
public class TrackPoint
{
    public double X { get; set; }
    public double Y { get; set; }
    public double T { get; set; } // Parâmetro de progresso (0 a 1)

    public TrackPoint(double x, double y, double t = 0)
    {
        X = x;
        Y = y;
        T = t;
    }

    public double DistanceTo(double px, double py)
    {
        double dx = X - px;
        double dy = Y - py;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

/// <summary>
/// Gera e mantém a pista fechada com suporte a vários estilos
/// </summary>
public class Track
{
    public List<TrackPoint> Points { get; private set; } = new();
    public int LineWidth { get; set; } = 10; // Espessura da linha
    public double TotalLength { get; private set; } = 0;

    // Parâmetros da pista
    private double canvasWidth;
    private double canvasHeight;
    private TrackStyle style;

    public enum TrackStyle
    {
        Simple,  // Linha horizontal simples
        Oval,    // Oval/elipse
        Custom   // Baseado em pontos de controle
    }

    public Track(double width, double height, TrackStyle style = TrackStyle.Custom)
    {
        canvasWidth = width;
        canvasHeight = height;
        this.style = style;
        GenerateTrack();
    }

    public void UpdateCanvas(double width, double height)
    {
        if (width <= 0 || height <= 0)
            return;

        canvasWidth = width;
        canvasHeight = height;
        GenerateTrack();
    }

    private void GenerateTrack()
    {
        Points.Clear();

        switch (style)
        {
            case TrackStyle.Simple:
                GenerateSimpleTrack();
                break;
            case TrackStyle.Oval:
                GenerateOvalTrack();
                break;
            case TrackStyle.Custom:
                GenerateCustomTrack();
                break;
        }

        CalculateTotalLength();
    }

    /// <summary>
    /// Gera uma pista simples (linha horizontal)
    /// </summary>
    private void GenerateSimpleTrack()
    {
        int centerY = (int)(canvasHeight / 2);
        int steps = 100;

        for (int i = 0; i < steps; i++)
        {
            double t = (double)i / steps;
            double x = (canvasWidth - 100) * t + 50; // 50 a width-50
            double y = centerY;
            Points.Add(new TrackPoint(x, y, t));
        }
    }

    /// <summary>
    /// Gera uma pista em formato oval/elipse
    /// </summary>
    private void GenerateOvalTrack()
    {
        double centerX = canvasWidth / 2;
        double centerY = canvasHeight / 2;
        double radiusX = canvasWidth / 3;
        double radiusY = canvasHeight / 3;
        int steps = 200;

        for (int i = 0; i < steps; i++)
        {
            double t = (double)i / steps;
            double angle = t * 2 * Math.PI; // 0 a 2π

            double x = centerX + radiusX * Math.Cos(angle);
            double y = centerY + radiusY * Math.Sin(angle);

            Points.Add(new TrackPoint(x, y, t));
        }
    }

    /// <summary>
    /// Gera pista personalizada com pontos de controle (da especificação do usuário)
    /// </summary>
    private void GenerateCustomTrack()
    {
        // Pontos de controle da pista conforme especificacao (base 800x500)
        // Estes pontos sao escalados e centralizados para ocupar o canvas
        var controlPoints = new (double x, double y)[]
        {
            (120, 380), // P0  - Linha de largada
            (80,  300),
            (90,  180),
            (160, 120),
            (300, 110),
            (450, 110),
            (550, 150),
            (600, 120),
            (680, 180),
            (650, 250),
            (580, 300),
            (650, 380),
            (550, 420),
            (450, 420),
            (350, 350),
            (280, 420),
            (180, 420),
            (120, 380), // Fechar em P0
        };

        // Escalar e centralizar os pontos para o tamanho do canvas
        double minX = controlPoints.Min(p => p.x);
        double maxX = controlPoints.Max(p => p.x);
        double minY = controlPoints.Min(p => p.y);
        double maxY = controlPoints.Max(p => p.y);

        double refWidth = maxX - minX;
        double refHeight = maxY - minY;

        // Margem interna para nao encostar nas bordas
        double margin = 0.02; // 2% de margem
        double targetWidth = canvasWidth * (1.0 - 2.0 * margin);
        double targetHeight = canvasHeight * (1.0 - 2.0 * margin);

        double scaleX = targetWidth / refWidth;
        double scaleY = targetHeight / refHeight;
        double scale = Math.Min(scaleX, scaleY);

        // Centralizar no canvas
        double scaledWidth = refWidth * scale;
        double scaledHeight = refHeight * scale;
        double offsetX = (canvasWidth - scaledWidth) / 2.0;
        double offsetY = (canvasHeight - scaledHeight) / 2.0;

        var pixelPoints = controlPoints.Select(p => new TrackPoint(
            (p.x - minX) * scale + offsetX,
            (p.y - minY) * scale + offsetY
        )).ToList();

        // Gerar spline suave através dos pontos usando interpolação Catmull-Rom
        // Aumentar amostras para melhor qualidade
        int samplesPerSegment = 50; // Quantas amostras entre cada ponto de controle
        int totalSamples = (pixelPoints.Count - 1) * samplesPerSegment; // Excluir ponto duplicado de fechamento

        for (int i = 0; i <= totalSamples; i++)
        {
            double t = (double)i / totalSamples;
            var point = InterpolateSpline(pixelPoints, t);
            point.T = t;
            Points.Add(point);
        }
    }

    /// <summary>
    /// Interpolação Catmull-Rom para suavização de pontos
    /// Implementação melhorada com suporte a loop fechado
    /// </summary>
    private TrackPoint InterpolateSpline(List<TrackPoint> controlPoints, double t)
    {
        // t ∈ [0, 1] - parâmetro global da pista
        // n é o número de segmentos (excluindo o ponto duplicado ao final)
        int n = controlPoints.Count - 1; // -1 porque o último ponto é duplicado

        // Encontrar em qual segmento estamos
        double scaledT = t * n;
        int segmentIndex = (int)Math.Floor(scaledT);
        double localT = scaledT - segmentIndex;

        // Garantir que estejamos dentro dos limites
        if (segmentIndex >= n)
        {
            segmentIndex = n - 1;
            localT = 1.0;
        }

        // Pontos para interpolação Catmull-Rom
        // Precisamos de 4 pontos: P0, P1, P2, P3
        // onde queremos interpolar entre P1 e P2
        int p0_idx = segmentIndex == 0 ? controlPoints.Count - 2 : segmentIndex - 1;
        int p1_idx = segmentIndex;
        int p2_idx = segmentIndex + 1;
        int p3_idx = segmentIndex + 2;

        // Garantir índices válidos com wrap-around
        p1_idx = p1_idx % controlPoints.Count;
        p2_idx = p2_idx % controlPoints.Count;
        p3_idx = p3_idx % controlPoints.Count;

        TrackPoint p0 = controlPoints[p0_idx];
        TrackPoint p1 = controlPoints[p1_idx];
        TrackPoint p2 = controlPoints[p2_idx];
        TrackPoint p3 = controlPoints[p3_idx];

        // Coeficientes Catmull-Rom
        // Q(t) = 0.5 * [1 t t² t³] * M * [P0 P1 P2 P3]ᵀ
        double t2 = localT * localT;
        double t3 = t2 * localT;

        // Matriz Catmull-Rom
        double a0 = -0.5 * t3 + t2 - 0.5 * localT;
        double a1 = 1.5 * t3 - 2.5 * t2 + 1.0;
        double a2 = -1.5 * t3 + 2.0 * t2 + 0.5 * localT;
        double a3 = 0.5 * t3 - 0.5 * t2;

        double x = a0 * p0.X + a1 * p1.X + a2 * p2.X + a3 * p3.X;
        double y = a0 * p0.Y + a1 * p1.Y + a2 * p2.Y + a3 * p3.Y;

        return new TrackPoint(x, y, t);
    }

    /// <summary>
    /// Calcula o comprimento total da pista
    /// </summary>
    private void CalculateTotalLength()
    {
        TotalLength = 0;
        for (int i = 0; i < Points.Count - 1; i++)
        {
            double dx = Points[i + 1].X - Points[i].X;
            double dy = Points[i + 1].Y - Points[i].Y;
            TotalLength += Math.Sqrt(dx * dx + dy * dy);
        }
    }

    /// <summary>
    /// Encontra o ponto mais próximo na pista dado uma posição
    /// </summary>
    public (TrackPoint point, int index, double distance) FindClosestPoint(double x, double y)
    {
        double minDistance = double.MaxValue;
        int closestIndex = 0;
        TrackPoint? closest = null;

        for (int i = 0; i < Points.Count; i++)
        {
            double distance = Points[i].DistanceTo(x, y);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestIndex = i;
                closest = Points[i];
            }
        }

        return (closest ?? Points[0], closestIndex, minDistance);
    }

    /// <summary>
    /// Calcula o parâmetro T contínuo (0 a 1) para uma posição na pista
    /// </summary>
    public double FindContinuousT(double x, double y)
    {
        var (closestPoint, closestIndex, distance) = FindClosestPoint(x, y);
        
        // Usar o índice do ponto mais próximo para calcular T contínuo
        // T = index / (Points.Count - 1)
        if (Points.Count <= 1)
            return 0;
            
        return (double)closestIndex / (Points.Count - 1);
    }

    /// <summary>
    /// Verifica se o ponto está dentro da "zona de pista" (dentro de uma margem)
    /// </summary>
    public bool IsOnTrack(double x, double y, double tolerance = 40)
    {
        var (point, index, distance) = FindClosestPoint(x, y);
        return distance <= tolerance;
    }

    /// <summary>
    /// Retorna o ponto na pista em um parâmetro t específico (0 a 1)
    /// </summary>
    public TrackPoint GetPointAtParameter(double t)
    {
        t = Math.Clamp(t, 0, 1);
        int index = (int)(t * (Points.Count - 1));
        index = Math.Min(index, Points.Count - 1);
        return Points[index];
    }

    /// <summary>
    /// Retorna a posição da linha de largada/chegada
    /// </summary>
    public (double x1, double y1, double x2, double y2) GetStartLine()
    {
        // Linha de largada é perpendicular ao primeiro ponto (t=0)
        // Sem offset para ficar exatamente onde a volta é contabilizada
        var p0 = Points[0];
        var p1 = Points.Count > 1 ? Points[1] : Points[0];

        // Direção da pista
        double dx = p1.X - p0.X;
        double dy = p1.Y - p0.Y;
        double length = Math.Sqrt(dx * dx + dy * dy);

        if (length == 0) length = 1;

        // Perpendicular (rotação 90°)
        double perpX = -dy / length;
        double perpY = dx / length;

        // Tamanho da linha vermelha (perpendicular ao ponto)
        double lineLength = 50; // Comprimento da linha perpendicular

        return (
            p0.X + perpX * lineLength,
            p0.Y + perpY * lineLength,
            p0.X - perpX * lineLength,
            p0.Y - perpY * lineLength
        );
    }
}
