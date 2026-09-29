namespace BitacoraEvidencias.Web.Security;

public static class AuditEvents
{
    public const string Login = "LOGIN";
    public const string Logout = "LOGOUT";
    public const string LoginFail = "LOGIN_FAIL";
    public const string Lockout = "LOCKOUT";

    public const string Create = "CREATE";
    public const string Update = "UPDATE";
    public const string Delete = "DELETE";
    public const string Import = "IMPORT";

    public const string UploadEvidence = "UPLOAD_EVIDENCE";
    public const string DeleteEvidence = "DELETE_EVIDENCE";
    public const string ViewEvidence = "VIEW_EVIDENCE";
    public const string AuditCase = "AUDIT_CASE";
    public const string ResetCaseAudit = "RESET_CASE_AUDIT";
    public const string ViewAuditLog = "VIEW_AUDIT_LOG";
}
