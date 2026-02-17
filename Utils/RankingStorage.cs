using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Simulador_dot.Utils;

/// <summary>
/// Gerencia persistência de ranking em arquivo JSON
/// </summary>
public class RankingStorage
{
    private static readonly string RankingPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Simulador_dot",
        "ranking.json"
    );

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    static RankingStorage()
    {
        var directory = Path.GetDirectoryName(RankingPath);
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory!);
    }

    /// <summary>
    /// Salva uma corrida no ranking
    /// </summary>
    public static async Task SalvarCorrida(Models.RecordeCorrida recorde)
    {
        try
        {
            var ranking = await ObterRanking();

            // Verificar se é recorde
            if (!ranking.Any() || recorde.TempoTotal < ranking.First().TempoTotal)
            {
                recorde.EhRecorde = true;
            }

            ranking.Add(recorde);

            // Ordenar por tempo total
            ranking = ranking.OrderBy(r => r.TempoTotal).ToList();

            var json = JsonSerializer.Serialize(ranking, JsonOptions);
            await File.WriteAllTextAsync(RankingPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Erro ao salvar corrida: {ex.Message}");
        }
    }

    /// <summary>
    /// Obtém todo o ranking
    /// </summary>
    public static async Task<List<Models.RecordeCorrida>> ObterRanking()
    {
        try
        {
            if (!File.Exists(RankingPath))
                return new();

            var json = await File.ReadAllTextAsync(RankingPath);
            var ranking = JsonSerializer.Deserialize<List<Models.RecordeCorrida>>(json, JsonOptions) ?? new();
            return ranking.OrderBy(r => r.TempoTotal).ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Erro ao carregar ranking: {ex.Message}");
            return new();
        }
    }

    /// <summary>
    /// Obtém top N corridas
    /// </summary>
    public static async Task<List<Models.RecordeCorrida>> ObterTopRecordes(int quantidade = 10)
    {
        var ranking = await ObterRanking();
        return ranking.Take(quantidade).ToList();
    }

    /// <summary>
    /// Obtém melhor tempo total
    /// </summary>
    public static async Task<double> ObterMelhorTempo()
    {
        var ranking = await ObterRanking();
        return ranking.Any() ? ranking.First().TempoTotal : 0;
    }

    /// <summary>
    /// Obtém número total de corridas
    /// </summary>
    public static async Task<int> ObterTotalCorridas()
    {
        var ranking = await ObterRanking();
        return ranking.Count;
    }

    /// <summary>
    /// Limpa todo o ranking
    /// </summary>
    public static async Task LimparRanking()
    {
        try
        {
            if (File.Exists(RankingPath))
                File.Delete(RankingPath);

            await File.WriteAllTextAsync(RankingPath, "[]");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Erro ao limpar ranking: {ex.Message}");
        }
    }

    /// <summary>
    /// Formata tempo em milissegundos para string legível
    /// </summary>
    public static string FormatarTempo(double ms)
    {
        if (ms < 1000)
            return $"{ms:F0}ms";

        var segundos = ms / 1000.0;
        if (segundos < 60)
            return $"{segundos:F2}s";

        var minutos = (int)(segundos / 60);
        var secs = segundos % 60;
        return $"{minutos}m {secs:F2}s";
    }

    /// <summary>
    /// Formata data para string legível
    /// </summary>
    public static string FormatarData(DateTime data)
    {
        return data.ToString("dd/MM HH:mm:ss");
    }
}
