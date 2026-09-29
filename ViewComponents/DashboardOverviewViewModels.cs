namespace BitacoraEvidencias.Web.ViewComponents;

public sealed class DashboardOverviewViewModel
{
    public required IReadOnlyList<DashboardStatCardModel> StatCards { get; init; }
    public required IReadOnlyList<DashboardOficioItemModel> OficiosRecientes { get; init; }
    public required IReadOnlyList<DashboardCasoItemModel> CasosPendientes { get; init; }
    public required DateTime LastUpdatedLocal { get; init; }
}

public sealed class DashboardStatCardModel
{
    public required string Title { get; init; }
    public required int Value { get; init; }
    public required string AccentClass { get; init; }
}

public sealed class DashboardPanelHeaderModel
{
    public required string Title { get; init; }
    public required string ActionText { get; init; }
    public required string ActionHref { get; init; }
}

public sealed class DashboardOficioItemModel
{
    public required int Id { get; init; }
    public required string NumeroOficio { get; init; }
    public required DateTime FechaOficio { get; init; }
    public required string Asunto { get; init; }
    public required int TotalCasos { get; init; }
}

public sealed class DashboardCasoItemModel
{
    public required int Id { get; init; }
    public required string NumeroOficio { get; init; }
    public required int Consecutivo { get; init; }
    public required string NivelEducativo { get; init; }
    public required string TipoCorreccion { get; init; }
    public required string Estatus { get; init; }
    public string ConsecutivoTexto => Consecutivo.ToString("000");
}
