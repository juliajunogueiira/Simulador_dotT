using System;
using System.Collections.Generic;
using System.Linq;

namespace Simulador_dot.Utils;

/// <summary>
/// Coleta dados para gráficos em tempo real
/// </summary>
public class GraficoDataCollector
{
    public class GraficoPoint
    {
        public double Tempo { get; set; }
        public double Erro { get; set; }
        public double Correcao { get; set; }
        public double VelEsquerda { get; set; }
        public double VelDireita { get; set; }
    }

    private readonly List<GraficoPoint> pontos = new();
    private readonly int maxPontos = 500;
    private double tempoInicial = 0;
    private bool correuStartou = false;

    /// <summary>
    /// Adiciona um ponto de dados
    /// </summary>
    public void AdicionarPonto(double tempoDecorrido, double erro, double correcao,
        double velEsquerda, double velDireita)
    {
        if (!correuStartou)
        {
            tempoInicial = tempoDecorrido;
            correuStartou = true;
        }

        var ponto = new GraficoPoint
        {
            Tempo = tempoDecorrido - tempoInicial,
            Erro = erro,
            Correcao = correcao,
            VelEsquerda = velEsquerda,
            VelDireita = velDireita
        };

        pontos.Add(ponto);

        // Manter apenas os últimos N pontos
        if (pontos.Count > maxPontos)
            pontos.RemoveAt(0);
    }

    /// <summary>
    /// Obtém todos os pontos coletados
    /// </summary>
    public List<GraficoPoint> ObterPontos() => new(pontos);

    /// <summary>
    /// Reseta o coletor
    /// </summary>
    public void Resetar()
    {
        pontos.Clear();
        tempoInicial = 0;
        correuStartou = false;
    }

    /// <summary>
    /// Calcula erro médio
    /// </summary>
    public double CalcularErroMedio()
    {
        if (pontos.Count == 0) return 0;
        return pontos.Average(p => Math.Abs(p.Erro));
    }

    /// <summary>
    /// Calcula velocidade média
    /// </summary>
    public double CalcularVelocidadeMedia()
    {
        if (pontos.Count == 0) return 0;
        return pontos.Average(p => (p.VelEsquerda + p.VelDireita) / 2);
    }

    /// <summary>
    /// Calcula oscilação (desvio padrão do erro)
    /// </summary>
    public double CalcularOscilacao()
    {
        if (pontos.Count < 2) return 0;

        var media = CalcularErroMedio();
        var variancia = pontos.Average(p => Math.Pow(Math.Abs(p.Erro) - media, 2));
        return Math.Sqrt(variancia);
    }

    /// <summary>
    /// Exporta dados em formato CSV
    /// </summary>
    public string ExportarCSV()
    {
        var csv = "Tempo(ms),Erro,Correcao,VelEsquerda,VelDireita\n";
        foreach (var ponto in pontos)
        {
            csv += $"{ponto.Tempo:F2},{ponto.Erro:F2},{ponto.Correcao:F2}," +
                   $"{ponto.VelEsquerda:F2},{ponto.VelDireita:F2}\n";
        }
        return csv;
    }
}
