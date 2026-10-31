namespace Metabolix.Api.Services;

public class InvalidImageException(string message) : Exception(message);

/// <summary>Картинки лежат на диске (в Docker — в volume) и раздаются как статика по /uploads.</summary>
public class FileStorage(IConfiguration config, IWebHostEnvironment env)
{
    public const string UrlPrefix = "/uploads/";
    public const long MaxBytes = 5 * 1024 * 1024;

    public string Root { get; } = Path.GetFullPath(config["Uploads:Path"] ?? "uploads", env.ContentRootPath);

    public async Task<string> SaveImageAsync(IFormFile file)
    {
        if (file.Length == 0) throw new InvalidImageException("Файл пуст");
        if (file.Length > MaxBytes) throw new InvalidImageException("Файл больше 5 МБ");

        // Тип определяем по первым байтам файла: имени и Content-Type от клиента верить нельзя.
        var header = new byte[12];
        await using var input = file.OpenReadStream();
        await input.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
        var extension = DetectExtension(header) ?? throw new InvalidImageException("Нужна картинка JPEG, PNG или WebP");

        // Имя случайное: так нельзя подобрать чужой файл или перезаписать существующий.
        Directory.CreateDirectory(Root);
        var name = Guid.NewGuid().ToString("N") + extension;
        input.Position = 0;
        await using var output = File.Create(Path.Combine(Root, name));
        await input.CopyToAsync(output);
        return UrlPrefix + name;
    }

    public void Delete(string? url)
    {
        if (url == null || !url.StartsWith(UrlPrefix)) return;
        var path = Path.Combine(Root, Path.GetFileName(url));
        if (File.Exists(path)) File.Delete(path);
    }

    private static string? DetectExtension(byte[] h)
    {
        if (h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return ".jpg";
        if (h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47) return ".png";
        if (h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F' && h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P') return ".webp";
        return null;
    }
}
