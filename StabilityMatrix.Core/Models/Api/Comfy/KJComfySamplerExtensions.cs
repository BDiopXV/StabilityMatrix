using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace StabilityMatrix.Core.Models.Api.Comfy;

public static class KJComfySamplerExtensions
{
    public static ComfySampler ToComfySampler(this KJComfySampler kj)
    {
        if (kj.Equals(default(KJComfySampler)) || string.IsNullOrWhiteSpace(kj.Name))
            return ComfySampler.Euler;

        var target = Normalize(kj.Name);

        var lookup = ComfySampler.Defaults.ToDictionary(x => Normalize(x.Name), x => x);

        // Exact normalized match
        if (lookup.TryGetValue(target, out var exact))
            return exact;

        // Common transformations to help map popular differences
        var variants = new[]
        {
            target,
            target.Replace("plusplus", "pp"),
            target.Replace("++", "pp"),
            target.Replace("uni_pc", "unipc"),
            target.Replace("_", ""),
            target.Replace("-", ""),
            target.Replace("beta", ""),
        }.Distinct();

        foreach (var v in variants)
            if (lookup.TryGetValue(v, out exact))
                return exact;

        // fallback: pick the closest by Levenshtein distance
        var best = lookup
            .Keys.Select(k => new { Key = k, Dist = LevenshteinDistance(k, target) })
            .OrderBy(x => x.Dist)
            .FirstOrDefault();

        if (best != null && best.Dist <= 3 && lookup.TryGetValue(best.Key, out var bestSampler))
            return bestSampler;

        // last resort - Euler default
        return ComfySampler.Euler;
    }

    private static string Normalize(string s) =>
        Regex.Replace(s ?? string.Empty, "[^a-z0-9]+", "", RegexOptions.IgnoreCase).ToLowerInvariant();

    // Levenshtein distance (simple implementation)
    private static int LevenshteinDistance(string a, string b)
    {
        if (string.IsNullOrEmpty(a))
            return b?.Length ?? 0;
        if (string.IsNullOrEmpty(b))
            return a.Length;

        var da = a.ToCharArray();
        var db = b.ToCharArray();
        var d = new int[da.Length + 1, db.Length + 1];

        for (var i = 0; i <= da.Length; i++)
            d[i, 0] = i;
        for (var j = 0; j <= db.Length; j++)
            d[0, j] = j;

        for (var i = 1; i <= da.Length; i++)
        {
            for (var j = 1; j <= db.Length; j++)
            {
                var cost = da[i - 1] == db[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }

        return d[da.Length, db.Length];
    }
}
