using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Simulador_dot.Models;

/// <summary>
/// Registra uma corrida completa com todos os dados
/// </summary>
public class RecordeCorrida
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("data")]
    public DateTime Data { get; set; } = DateTime.Now;

    [JsonPropertyName("modo")]
    public string Modo { get; set; } = "FreeRace"; // FreeRace, Test3Laps, Oficial

    [JsonPropertyName("tempoTotal")]
    public double TempoTotal { get; set; } // em ms

    [JsonPropertyName("temposVoltas")]
    public List<double> TemposVoltas { get; set; } = new();

    [JsonPropertyName("voltasCompletas")]
    public int VoltasCompletas => TemposVoltas.Count;

    [JsonPropertyName("pidConfig")]
    public PIDConfigRecord PIDConfig { get; set; } = new();

    [JsonPropertyName("erroMedio")]
    public double ErroMedio { get; set; }

    [JsonPropertyName("velocidadeMedia")]
    public double VelocidadeMedia { get; set; }

    [JsonPropertyName("erros")]
    public List<double> Erros { get; set; } = new();

    [JsonPropertyName("posicoes")]
    public List<(double x, double y)> Posicoes { get; set; } = new();

    /// <summary>
    /// Melhor volta desta corrida
    /// </summary>
    public double MelhorVolta => TemposVoltas.Any() ? TemposVoltas.Min() : 0;

    /// <summary>
    /// Pior volta desta corrida
    /// </summary>
    public double PiorVolta => TemposVoltas.Any() ? TemposVoltas.Max() : 0;

    /// <summary>
    /// Tempo médio das voltas
    /// </summary>
    public double TempoMedio => TemposVoltas.Any() ? TemposVoltas.Average() : 0;

    /// <summary>
    /// Verifica se é um recorde (melhor tempo total)
    /// </summary>
    public bool EhRecorde { get; set; }

    public override string ToString()
    {
        return $"[{Data:dd/MM HH:mm}] {Modo}: {TemposVoltas.Count} voltas - {TempoTotal:F0}ms - Melhor: {MelhorVolta:F0}ms";
    }
}

/// <summary>
/// Cópia dos parâmetros PID para histórico
/// </summary>
public class PIDConfigRecord
{
    [JsonPropertyName("kp")]
    public double Kp { get; set; }

    [JsonPropertyName("ki")]
    public double Ki { get; set; }

    [JsonPropertyName("kd")]
    public double Kd { get; set; }

    [JsonPropertyName("velocidadeBase")]
    public double VelocidadeBase { get; set; }

    public PIDConfigRecord() { }

    public PIDConfigRecord(double kp, double ki, double kd, double velocidadeBase)
    {
        Kp = kp;
        Ki = ki;
        Kd = kd;
        VelocidadeBase = velocidadeBase;
    }
}
