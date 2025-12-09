using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace StabilityMatrix.Core.Models.Api.Comfy;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "StringLiteralTypo")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
public readonly record struct KJComfySampler(string Name)
{
    public static KJComfySampler UniPC { get; } = new("unipc");
    public static KJComfySampler UniPCBeta { get; } = new("unipc/beta");
    public static KJComfySampler Dpmpp { get; } = new("dpm++");
    public static KJComfySampler DpmppBeta { get; } = new("dpm++/beta");
    public static KJComfySampler DpmppSde { get; } = new("dpm++_sde");
    public static KJComfySampler DpmppSdeBeta { get; } = new("dpm++_sde/beta");
    public static KJComfySampler Euler { get; } = new("euler");
    public static KJComfySampler EulerBeta { get; } = new("euler/beta");
    public static KJComfySampler Deis { get; } = new("deis");
    public static KJComfySampler LCM { get; } = new("lcm");
    public static KJComfySampler LcmBeta { get; } = new("lcm/beta");
    public static KJComfySampler ResMultistep { get; } = new("res_multistep");
    public static KJComfySampler FlowmatchCausvid { get; } = new("flowmatch_causvid");
    public static KJComfySampler FlowmatchDistill { get; } = new("flowmatch_distill");
    public static KJComfySampler FlowmatchPusa { get; } = new("flowmatch_pusa");
    public static KJComfySampler Multitalk { get; } = new("multitalk");
    public static KJComfySampler SaOdeStable { get; } = new("sa_ode_stable");

    private static Dictionary<KJComfySampler, string> ConvertDict { get; } =
        new()
        {
            [UniPC] = "UniPC",
            [UniPCBeta] = "UniPC (Beta)",
            [Dpmpp] = "DPM++",
            [DpmppBeta] = "DPM++ (Beta)",
            [DpmppSde] = "DPM++ SDE",
            [DpmppSdeBeta] = "DPM++ SDE (Beta)",
            [Euler] = "Euler",
            [EulerBeta] = "Euler (Beta)",
            [Deis] = "Deis",
            [LCM] = "LCM",
            [LcmBeta] = "LCM (Beta)",
            [ResMultistep] = "Res Multistep",
            [FlowmatchCausvid] = "FlowMatch CausVid",
            [FlowmatchDistill] = "FlowMatch Distill",
            [FlowmatchPusa] = "FlowMatch PUSA",
            [Multitalk] = "MultiTalk",
            [SaOdeStable] = "SA-ODE (Stable)",
        };

    public static IReadOnlyList<KJComfySampler> Defaults { get; } = ConvertDict.Keys.ToImmutableArray();

    public string DisplayName => ConvertDict.GetValueOrDefault(this, Name);

    /// <inheritdoc />
    public bool Equals(KJComfySampler other)
    {
        return Name == other.Name;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return Name.GetHashCode();
    }

    private sealed class NameEqualityComparer : IEqualityComparer<KJComfySampler>
    {
        public bool Equals(KJComfySampler x, KJComfySampler y)
        {
            return x.Name == y.Name;
        }

        public int GetHashCode(KJComfySampler obj)
        {
            return obj.Name.GetHashCode();
        }
    }

    public static IEqualityComparer<KJComfySampler> Comparer { get; } = new NameEqualityComparer();
}
