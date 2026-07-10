using static Defra.Cdp.Backend.Api.Utils.CdpEnvironments;

namespace Defra.Cdp.Backend.Api.Services.Grafana.Models;

public static class PromotionEnvironments
{

    public static readonly string[] Environments =
    [
        InfraDev,
        Dev
    ];
}