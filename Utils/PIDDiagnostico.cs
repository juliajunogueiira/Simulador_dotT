using System;
using System.Collections.Generic;

namespace Simulador_dot.Utils;

/// <summary>
/// Fornece sugestões de ajustes PID baseadas em diagnóstico
/// </summary>
public class PIDDiagnostico
{
    public enum Parametro { Kp, Ki, Kd, VelBase }
    public enum Acao { Aumentar, Reduzir }

    public class Sugestao
    {
        public Parametro Parametro { get; set; }
        public Acao Acao { get; set; }
        public string Motivo { get; set; } = "";
        public double PercentualSugerido { get; set; } // 10, 20, 50 (%)
    }

    private readonly GraficoDataCollector grafico;

    public PIDDiagnostico(GraficoDataCollector grafico)
    {
        this.grafico = grafico;
    }

    /// <summary>
    /// Analisa o comportamento e sugere ajustes
    /// </summary>
    public List<Sugestao> AnalisarComportamento(int voltasCompletas)
    {
        var sugestoes = new List<Sugestao>();
        var pontos = grafico.ObterPontos();

        if (pontos.Count < 10)
            return sugestoes;

        var erroMedio = Math.Abs(grafico.CalcularErroMedio());
        var oscilacao = grafico.CalcularOscilacao();

        // Diagnóstico 1: Erro muito alto
        if (erroMedio > 20)
        {
            sugestoes.Add(new Sugestao
            {
                Parametro = Parametro.Kp,
                Acao = Acao.Aumentar,
                Motivo = $"Erro médio muito alto ({erroMedio:F1}). Aumentar proporcional.",
                PercentualSugerido = 20
            });
        }

        // Diagnóstico 2: Oscilação (comportamento oscilatório)
        if (oscilacao > 15)
        {
            sugestoes.Add(new Sugestao
            {
                Parametro = Parametro.Kp,
                Acao = Acao.Reduzir,
                Motivo = $"Oscilação alta ({oscilacao:F1}). Sistema está overtuned.",
                PercentualSugerido = 10
            });

            sugestoes.Add(new Sugestao
            {
                Parametro = Parametro.Kd,
                Acao = Acao.Aumentar,
                Motivo = "Adicionar amortecimento para reduzir oscilação.",
                PercentualSugerido = 15
            });
        }

        // Diagnóstico 3: Erro em rampas (derivada alta)
        var derivadas = CalcularDerivadas(pontos);
        if (derivadas.Any(d => Math.Abs(d) > 5))
        {
            sugestoes.Add(new Sugestao
            {
                Parametro = Parametro.Kd,
                Acao = Acao.Aumentar,
                Motivo = "Resposta lenta em mudanças. Aumentar derivativo.",
                PercentualSugerido = 20
            });
        }

        // Diagnóstico 4: Erro persistente (integral)
        if (erroMedio > 10 && oscilacao < 5)
        {
            sugestoes.Add(new Sugestao
            {
                Parametro = Parametro.Ki,
                Acao = Acao.Aumentar,
                Motivo = "Erro persistente com comportamento estável. Aumentar integral.",
                PercentualSugerido = 10
            });
        }

        // Diagnóstico 5: Sugestão de velocidade
        if (erroMedio < 5 && oscilacao < 8)
        {
            // Se o controle está bom, aumentar velocidade
            sugestoes.Add(new Sugestao
            {
                Parametro = Parametro.VelBase,
                Acao = Acao.Aumentar,
                Motivo = "Comportamento estável. Aumentar velocidade para voltas mais rápidas.",
                PercentualSugerido = 15
            });
        }
        else if (erroMedio > 20 || oscilacao > 20)
        {
            // Se o controle está instável, reduzir velocidade
            sugestoes.Add(new Sugestao
            {
                Parametro = Parametro.VelBase,
                Acao = Acao.Reduzir,
                Motivo = "Comportamento instável. Reduzir velocidade para melhor controle.",
                PercentualSugerido = 10
            });
        }

        return sugestoes;
    }

    /// <summary>
    /// Calcula derivadas (taxa de mudança do erro)
    /// </summary>
    private List<double> CalcularDerivadas(List<GraficoDataCollector.GraficoPoint> pontos)
    {
        var derivadas = new List<double>();
        for (int i = 1; i < pontos.Count; i++)
        {
            var dErro = pontos[i].Erro - pontos[i - 1].Erro;
            var dTempo = pontos[i].Tempo - pontos[i - 1].Tempo;
            if (dTempo > 0)
                derivadas.Add(dErro / dTempo);
        }
        return derivadas;
    }

    /// <summary>
    /// Gera relatório de diagnóstico
    /// </summary>
    public string GerarRelatorioDiagnostico()
    {
        var erroMedio = grafico.CalcularErroMedio();
        var oscilacao = grafico.CalcularOscilacao();
        var velocidadeMedia = grafico.CalcularVelocidadeMedia();

        var relatorio = $@"
=== DIAGNÓSTICO DO CONTROLADOR PID ===

📊 MÉTRICAS:
  • Erro Médio: {erroMedio:F2}
  • Oscilação: {oscilacao:F2}
  • Velocidade Média: {velocidadeMedia:F2} px/ms

📈 ANÁLISE:
";

        if (erroMedio < 5)
            relatorio += "  ✅ Erro muito baixo - Excelente controle\n";
        else if (erroMedio < 10)
            relatorio += "  ✓ Erro aceitável - Bom controle\n";
        else if (erroMedio < 20)
            relatorio += "  ⚠ Erro médio - Melhorias necessárias\n";
        else
            relatorio += "  ❌ Erro alto - Reajustes urgentes\n";

        if (oscilacao < 3)
            relatorio += "  ✅ Comportamento estável\n";
        else if (oscilacao < 10)
            relatorio += "  ✓ Comportamento aceitavelmente suave\n";
        else
            relatorio += "  ⚠️ Comportamento oscilatório\n";

        return relatorio;
    }
}
