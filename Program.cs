using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;

internal class Program
{
    private const int SPI_SETDESKWALLPAPER = 20;
    private const int SPIF_UPDATEINIFILE = 0x01;
    private const int SPIF_SENDCHANGE = 0x02;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SystemParametersInfo(
        int uAction,
        int uParam,
        string lpvParam,
        int fuWinIni);

    static async Task<int> Main()
    {
        try
        {
            Console.Write("Enter wallpaper search subject: ");
            var query = Console.ReadLine()?.Trim();
            
            if (string.IsNullOrWhiteSpace(query))
                query = "random";

            var encodedQuery = Uri.EscapeDataString(query);

            Console.Write("Would you like 1920 X 1080 for the reolution? (Y or N):");
            var resolutionInput = Console.ReadLine()?.Trim().ToLower();
            var searchRes = "1920X1080";
            if (resolutionInput == "y" || resolutionInput == "yes")
            {
                searchRes = "1920x1080";

            }
            else
            {
                Console.Write("Enter your desired resolution (e.g., 2560x1440): ");
                var customRes = Console.ReadLine()?.Trim().ToLower();
                if (!string.IsNullOrWhiteSpace(customRes))
                {
                    searchRes = customRes;
                }
                else
                {
                    searchRes = "1920x1080";
                }
            }

            var encodedRes = Uri.EscapeDataString(searchRes);

            var apiUrl =
                "https://wallhaven.cc/api/v1/search" +
                $"?q={encodedQuery}" +
                "&sorting=random" +
                "&purity=100" +
                "&categories=111" +
                $"&atleast={encodedRes}" +
                "&ratios=16x9" +
                "&page=1";

            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("WallhavenWallpaper/1.0");

            var json = await http.GetStringAsync(apiUrl);
            using var doc = JsonDocument.Parse(json);

            var data = doc.RootElement.GetProperty("data");
            if (data.GetArrayLength() == 0)
                throw new Exception("No wallpapers found for that search.");

            var rand = new Random();
            var chosen = data[rand.Next(data.GetArrayLength())];
            var imageUrl = chosen.GetProperty("path").GetString();

            if (string.IsNullOrWhiteSpace(imageUrl))
                throw new Exception("Invalid image URL.");

            var ext = Path.GetExtension(new Uri(imageUrl).AbsolutePath);
            if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";

            var filePath = Path.Combine(
                Path.GetTempPath(),
                $"wallhaven_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");

            var bytes = await http.GetByteArrayAsync(imageUrl);
            await File.WriteAllBytesAsync(filePath, bytes);

            SetWallpaperStyleFill();

            var success = SystemParametersInfo(
                SPI_SETDESKWALLPAPER,
                0,
                filePath,
                SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

            if (!success)
                throw new Exception("Failed to set wallpaper.");

            Console.WriteLine("Wallpaper set (Fill).");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
            Console.ReadKey();
            return 1;
        }
    }

    static void SetWallpaperStyleFill()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);
        if (key == null) return;

        key.SetValue("WallpaperStyle", "10"); // Fill
        key.SetValue("TileWallpaper", "0");
    }
}
