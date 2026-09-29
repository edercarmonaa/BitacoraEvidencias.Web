using BitacoraEvidencias.Web.Models;

namespace BitacoraEvidencias.Web.Services;

public interface ICaseAuditService
{
    void MarkCoincidente(CasoCorreccion caso);
    void MarkNoCoincidente(CasoCorreccion caso, string observacion);
    bool ResetToPending(CasoCorreccion caso);
    Task WriteDecisionLogAsync(CasoCorreccion caso, CancellationToken cancellationToken = default);
    Task WriteResetLogAsync(CasoCorreccion caso, string details, CancellationToken cancellationToken = default);
}
