using BitacoraEvidencias.Migration;
using Xunit;

namespace BitacoraEvidencias.Web.Tests.Data;

public class SearchDiagnosticsTests
{
    [Fact]
    public void PlanSummary_EmitsMetricsWithoutSqlParametersOrPersonalData()
    {
        const string xml = """
            <ShowPlanXML xmlns="http://schemas.microsoft.com/sqlserver/2004/07/showplan">
              <StmtSimple StatementText="SELECT secret-personal-data">
                <QueryPlan>
                  <QueryTimeStats ElapsedTime="25000" CpuTime="12" />
                  <MemoryGrantInfo GrantWaitTime="24000" RequestedMemory="1024" GrantedMemory="1024" MaxUsedMemory="32" />
                  <WaitStats><Wait WaitType="RESOURCE_SEMAPHORE" WaitTimeMs="24000" WaitCount="1" />
                    <Wait WaitType="SECRET-personal-value" WaitTimeMs="1" /></WaitStats>
                  <ParameterList><ColumnReference ParameterCompiledValue="secret-password" /></ParameterList>
                  <Sort><Warnings><SpillToTempDb /></Warnings></Sort>
                </QueryPlan>
              </StmtSimple>
            </ShowPlanXML>
            """;
        var summary = string.Join('\n', SearchDiagnostics.SummarizePlan(xml));
        Assert.Contains("25000 ms; CPU 12 ms", summary);
        Assert.Contains("espera 24000 ms", summary);
        Assert.Contains("RESOURCE_SEMAPHORE: 24000 ms", summary);
        Assert.Contains("Ordenamientos: 1; avisos de derrame a disco: 1", summary);
        Assert.DoesNotContain("secret", summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT", summary);
    }

    [Fact]
    public void PlanSummary_RejectsNonNumericMetrics()
    {
        const string xml = """
            <ShowPlanXML xmlns="http://schemas.microsoft.com/sqlserver/2004/07/showplan">
              <QueryTimeStats ElapsedTime="secret" CpuTime="NaN" />
            </ShowPlanXML>
            """;
        var summary = string.Join('\n', SearchDiagnostics.SummarizePlan(xml));
        Assert.Contains("transcurrido n/d ms; CPU n/d ms", summary);
        Assert.DoesNotContain("secret", summary);
        Assert.DoesNotContain("NaN", summary);
    }

    [Fact]
    public async Task Profile_RejectsProductionBeforeOpeningAnyConnection()
    {
        var error = await Assert.ThrowsAsync<MigrationValidationException>(() =>
            MigrationRunner.RunAsync("profile", "unused", "unused", true));
        Assert.Contains("solo admite la base de pruebas", error.Message);
    }
}
