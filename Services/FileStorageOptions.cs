namespace BitacoraEvidencias.Web.Services;

public class FileStorageOptions
{
    public const string SectionName = "Storage";

    public string RootPath { get; set; } = "Storage";
    public string RequestPath { get; set; } = "/media";
    public int ThumbnailWidth { get; set; } = 360;
    public int ThumbnailHeight { get; set; } = 360;
    public int MaxUploadMegabytes { get; set; } = 8;
    public int MaxImageDimension { get; set; } = 2000;
    public int JpegQuality { get; set; } = 78;
}
