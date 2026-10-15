using BitacoraEvidencias.Migration;

if (args.Length < 3 || args[0] is not ("import" or "verify" or "preflight"))
{
    Console.Error.WriteLine("Uso: BitacoraMigration <preflight|import|verify> <copia.db> <copia-evidencias> [--production]");
    return 2;
}

try
{
    await MigrationRunner.RunAsync(args[0], args[1], args[2], args.Contains("--production"));
    return 0;
}
catch (Exception exception)
{
    // Provider messages can contain values or credentials. Report only a safe category.
    Console.Error.WriteLine(exception is MigrationValidationException
        ? exception.Message
        : exception is Microsoft.Data.Sqlite.SqliteException sqlite
            ? $"Proceso SQLite detenido: codigo {sqlite.SqliteErrorCode}, extendido {sqlite.SqliteExtendedErrorCode}."
            : exception is Microsoft.Data.SqlClient.SqlException sql
            ? $"Proceso SQL detenido: codigo {sql.Number}, estado {sql.State}. Revise conexion, TLS, permisos y compatibilidad; no se imprime el mensaje con datos."
            : $"Proceso detenido ({exception.GetType().Name}). No se imprimen datos ni secretos. Revise conexion, TLS, permisos y compatibilidad.");
    return 1;
}
