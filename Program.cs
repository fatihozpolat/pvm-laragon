using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO.Compression;
using System.Net;
using Salaros.Configuration;

namespace ConsoleApp
{
    class Program
    {
        private static readonly HttpClient client = new HttpClient();
        private static string laragonPath;

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            // ilk çalışmada eğer yoksa programı Local/pvm ye kopyala
            var pvmPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "pvm");
            var currentPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            if (pvmPath != currentPath)
            {
                Directory.CreateDirectory(pvmPath);

                foreach (var file in Directory.GetFiles(currentPath))
                {
                    var destFile = Path.Combine(pvmPath, Path.GetFileName(file));
                    // eğer dosya varsa sil 
                    if (File.Exists(destFile))
                        File.Delete(destFile);
                    File.Copy(file, destFile);
                }

                // environment variable ekle
                string pathEnv = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User);
                if (!pathEnv.Contains(pvmPath))
                {
                    Environment.SetEnvironmentVariable("PATH", pathEnv + ";" + pvmPath, EnvironmentVariableTarget.User);
                    Console.WriteLine("🆗 Path updated");
                }
            }


            await SetLaragon();

            if (args.Length == 0)
            {
                Console.WriteLine("Usage: pvm <command> [options]");
                return;
            }

            var command = args[0];

            switch (command)
            {
                case "install":
                    await PInstall(args);
                    break;
                case "use":
                    await PUse(args);
                    break;
                case "list":
                    await PList(args);
                    break;
                case "list-remote":
                    await PListRemote(args);
                    break;
                case "remove":
                    await PRemove(args);
                    break;
                case "apache":
                    await PApache(args);
                    break;
                case "help":
                    Console.WriteLine("Usage: pvm <command> [options]");
                    Console.WriteLine("Commands:");
                    Console.WriteLine("  install <version> - Install a specific version of PHP");
                    Console.WriteLine("  use <version> - Use a specific version of PHP");
                    Console.WriteLine("  list - List all installed versions of PHP");
                    Console.WriteLine("  list-remote - List all available versions of PHP");
                    Console.WriteLine("  remove <version> - Remove a specific version of PHP");

                    Console.WriteLine("  apache list - List all installed versions of Apache");
                    Console.WriteLine("  apache fix - Fix Apache installation");
                    Console.WriteLine("  apache use <version> - Use a specific version of Apache");
                    break;
                default:
                    Console.WriteLine("Unknown command");
                    break;
            }
        }

        private static async Task SetLaragon()
        {
            // config dosyasını oku
            var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "pvm", "config.pvm");
            if (!File.Exists(configPath))
            {
                File.Create(configPath).Close();
            }

            var lines = File.ReadAllLines(configPath);
            foreach (var line in lines)
            {
                if (line.StartsWith("laragon="))
                {
                    laragonPath = line.Replace("laragon=", "");
                    break;
                }
            }

            if (string.IsNullOrEmpty(laragonPath))
            {
                Console.Write("✍️ Enter Laragon path: ");
                laragonPath = Console.ReadLine();
                File.WriteAllText(configPath, "laragon=" + laragonPath);

                return;
            }

            if (!Directory.Exists(laragonPath))
            {
                Console.WriteLine("❌ Laragon path not found");
                return;
            }
        }

        private static async Task PInstall(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: pvm install <version>");
                return;
            }

            var version = args[1];

            // önce https://windows.php.net/downloads/releases/releases.json adresinden version bilgilerini al user agent ekleyerek
            var userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.3";
            client.DefaultRequestHeaders.Add("User-Agent", userAgent);
            var response = await client.GetAsync("https://windows.php.net/downloads/releases/releases.json");
            var content = await response.Content.ReadAsStringAsync();

            var json = System.Text.Json.JsonDocument.Parse(content);
            var root = json.RootElement;

            List<string> versions = new List<string>(root.EnumerateObject().Select(x => x.Name));
            versions.Sort();

            var selectedVersion = versions.FirstOrDefault(x => x.StartsWith(version));

            if (selectedVersion == null)
            {
                Console.WriteLine("❌ Version not found");
                return;
            }

            string selectedKey = root.GetProperty(selectedVersion).EnumerateObject().Select(x => x.Name).FirstOrDefault(x => x.StartsWith("ts-") && x.EndsWith("-x64"));

            if (selectedKey == null)
            {
                Console.WriteLine("❌ Version not found");
                return;
            }

            string zip = root.GetProperty(selectedVersion).GetProperty(selectedKey).GetProperty("zip").GetProperty("path").GetString();
            string zipUrl = "https://windows.php.net/downloads/releases/" + zip;

            var zipPath = Path.Combine(laragonPath, "bin", "php", zip);
            if (!Directory.Exists(Path.GetDirectoryName(zipPath)))
                Directory.CreateDirectory(Path.GetDirectoryName(zipPath));


            await Task.Run(() => DownloadFile(zipUrl, zipPath));

            // zip uzantısı olmadan dosya adını al
            var name = Path.GetFileNameWithoutExtension(zip);
            var extractPath = Path.Combine(laragonPath, "bin", "php", name);
            await Task.Run(() => ExtractZip(zipPath, extractPath));
            await Task.Run(() => SetPhpIni(extractPath));
        }

        private static async Task PUse(string[] args)
        {
            // pvm nin içine bir symlink oluştur ve onu php versionuna yönlendir
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: pvm use <version>");
                return;
            }

            var version = args[1];

            var dirs = Directory.GetDirectories(Path.Combine(laragonPath, "bin", "php"));

            var intersect = dirs.Where(x => x.Contains(version)).ToList();

            if (intersect.Count() == 0)
            {
                Console.WriteLine("❌ Version not found");
                return;
            }

            var path = Path.Combine(laragonPath, "bin", "php", intersect.First());

            var symlinkPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "pvm", "symlink");

            // symlink oluştur
            if (Directory.Exists(symlinkPath))
                Directory.Delete(symlinkPath, true);

            // yönetici olarak çalıştırılmadığında symlink oluşturulamıyor yönetici ol 
            string command = "mklink /D " + symlinkPath + " " + path;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
            {
                FileName = "cmd",
                Arguments = "/c " + command,
                Verb = "runas",
                UseShellExecute = true
            });

            Console.WriteLine("🔗 Symlink created. PHP version set to " + version);

            // php versiyonunu kullanmak için PATH e ekle
            string pathEnv = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User);
            if (!pathEnv.Contains(symlinkPath))
            {
                Environment.SetEnvironmentVariable("PATH", pathEnv + ";" + symlinkPath, EnvironmentVariableTarget.User);
                Console.WriteLine("🆗 Path updated");
            }

            await StopLaragon();

            path = path.Substring(path.LastIndexOf("\\") + 1);

            Console.WriteLine("🚀 PHP version set to " + path);

            await SetLaragonIni("php", "Version", path);

            await StartLaragon();
        }

        private static async Task StopLaragon ()
        {
            bool isLaragonRunning = System.Diagnostics.Process.GetProcessesByName("laragon").Length > 0;
            if (isLaragonRunning)
            {
                Console.WriteLine("🔄 Killing Laragon..");
                System.Diagnostics.Process[] processes = System.Diagnostics.Process.GetProcessesByName("laragon");
                foreach (var process in processes)
                    process.Kill();
            }
        }

        private static async Task StartLaragon ()
        {
            Console.WriteLine("🚀 Starting Laragon..");
            System.Diagnostics.Process.Start(Path.Combine(laragonPath, "laragon.exe"));
        }

        private static async Task PList(string[] args)
        {
            var path = Path.Combine(laragonPath, "bin", "php");
            if (!Directory.Exists(path))
            {
                Console.WriteLine("😥 No versions installed");
                return;
            }

            var directories = Directory.GetDirectories(path);
            if (directories.Length == 0)
            {
                Console.WriteLine("😥 No versions installed");
                return;
            }

            // isimlerin php- den sonrasını -Win den öncesini al
            directories = Array.ConvertAll(directories, x => x.Substring(x.IndexOf("php-") + 4, x.IndexOf("-Win") - x.IndexOf("php-") - 4));

            Console.WriteLine("📦 Installed versions:");
            foreach (var dir in directories)
                Console.WriteLine("  " + dir);
        }

        private static async Task PListRemote(string[] args)
        {
            var userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.3";
            client.DefaultRequestHeaders.Add("User-Agent", userAgent);
            var response = await client.GetAsync("https://windows.php.net/downloads/releases/releases.json");
            var content = await response.Content.ReadAsStringAsync();

            var json = System.Text.Json.JsonDocument.Parse(content);
            var root = json.RootElement;

            List<string> versions = new List<string>(root.EnumerateObject().Select(x => x.Name));
            versions.Sort();

            Console.WriteLine("🤔 Available versions:  ");
            foreach (var version in versions)
                Console.WriteLine("  " + version);
        }

        private static async Task PRemove(string[] args)
        {
            var version = args[1];

            var dirs = Directory.GetDirectories(Path.Combine(laragonPath, "bin", "php"));
            var intersect = dirs.Where(x => x.Contains(version)).ToList();

            if (intersect.Count() == 0)
            {
                Console.WriteLine("❌ Version not found");
                return;
            }

            Directory.Delete(intersect.First(), true);

            Console.WriteLine("🗑️ Version removed");
        }

        private static async Task DownloadFile(string url, string path)
        {
            // indirme işlemi olurken progress bar göster
            if (File.Exists(path))
            {
                File.Delete(path);
                Console.WriteLine("🗑️ File deleted.. Re-downloading..");
            }

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.3";

                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                long totalBytes = response.ContentLength;

                using (Stream responseStream = response.GetResponseStream())
                {
                    using (FileStream fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        byte[] buffer = new byte[4096];
                        int bytesRead = responseStream.Read(buffer, 0, buffer.Length);
                        long bytesReadSum = 0;
                        int percentage = 0;

                        while (bytesRead > 0)
                        {
                            fileStream.Write(buffer, 0, bytesRead);
                            bytesReadSum += bytesRead;
                            bytesRead = responseStream.Read(buffer, 0, buffer.Length);

                            int newPercentage = (int)((bytesReadSum * 100) / totalBytes);
                            if (newPercentage != percentage)
                            {
                                percentage = newPercentage;
                                Console.Write("\r🤤 " + percentage + "%  -  " + bytesReadSum + " / " + totalBytes + " bytes            ");

                                if (percentage == 100)
                                {
                                    Console.WriteLine();
                                }
                            }
                        }

                        Console.WriteLine("👌 Download completed");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Download failed: " + ex.Message);
            }
        }

        private static async Task SetPhpIni(string extractPath)
        {
            // php.ini dosyasını kopyala
            var iniPath = Path.Combine(extractPath, "php.ini-development");
            var iniDestPath = Path.Combine(extractPath, "php.ini");
            File.Copy(iniPath, iniDestPath);

            Console.WriteLine("🆗 php.ini copied");

            string cacert = Path.Combine(extractPath, "cacert.pem");
            if (!File.Exists(cacert))
                await DownloadFile("https://curl.haxx.se/ca/cacert.pem", cacert);

            Console.WriteLine("🆗 cacert.pem downloaded");

            string[] extensions = { "curl", "fileinfo", "gd2", "intl", "mbstring", "exif", "mysqli", "openssl", "pdo_mysql", "soap", "xsl", "zip", "sockets", "sodium" };

            string[] phpIniLines = File.ReadAllLines(iniDestPath);

            for (int i = 0; i < phpIniLines.Length; i++)
            {
                if (phpIniLines[i].StartsWith(";extension_dir = \"ext\""))
                {
                    phpIniLines[i] = "extension_dir = \"ext\"";
                }

                if (phpIniLines[i].StartsWith(";extension="))
                {
                    string extension = phpIniLines[i].Replace(";extension=", "").Trim();
                    if (extensions.Contains(extension))
                        phpIniLines[i] = phpIniLines[i].Replace(";", "");
                }

                if (phpIniLines[i].StartsWith(";curl.cainfo"))
                    phpIniLines[i] = "curl.cainfo=" + cacert;

                if (phpIniLines[i].StartsWith(";openssl.cafile"))
                    phpIniLines[i] = "openssl.cafile=" + cacert;
            }

            File.WriteAllLines(iniDestPath, phpIniLines);

            Console.WriteLine("🆗 php.ini updated");

            Console.WriteLine("🎉 PHP installed successfully");
        }

        private static async Task ExtractZip(string zipPath, string extractPath)
        {
            if (Directory.Exists(extractPath))
                Directory.Delete(extractPath, true);

            Directory.CreateDirectory(extractPath);
            ZipFile.ExtractToDirectory(zipPath, extractPath);
            Console.WriteLine("📤 Extracted to " + extractPath);

            File.Delete(zipPath);
        }


        private static async Task PApache(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: pvm apache <command>");
                return;
            }

            var command = args[1];
            var apachePath = Path.Combine(laragonPath, "bin", "apache");
            var dirs = Directory.GetDirectories(apachePath);

            switch (command)
            {
                case "list":
                    if (dirs.Length == 0)
                    {
                        Console.WriteLine("😥 No versions installed");
                        return;
                    }

                    Console.WriteLine("📦 Installed versions:");
                    foreach (var dir in dirs)
                        Console.WriteLine("  " + dir.Substring(dir.LastIndexOf("\\") + 1));
                    break;
                case "fix":
                    var downloadUrl = "https://www.apachelounge.com/download/VS17/binaries/httpd-2.4.62-240718-win64-VS17.zip";
                    var zipName = "httpd-2.4.62-win64-VS17.zip";
                    var zipPath = Path.Combine(apachePath, zipName);
                    await Task.Run(() => DownloadFile(downloadUrl, zipPath));

                    var extractPath = Path.Combine(apachePath, "httpd-2.4.62-win64-VS17");
                    await Task.Run(() => ExtractZip(zipPath, extractPath));

                    var apache24Path = Path.Combine(extractPath, "Apache24");
                    //apache24 ü laragon bin apache içine taşı
                    if (Directory.Exists(apache24Path))
                    {
                        var destPath = Path.Combine(apachePath, "Apache24");
                        if (Directory.Exists(destPath))
                            Directory.Delete(destPath, true);

                        Directory.Move(apache24Path, destPath);
                        Console.WriteLine("🆗 Apache Installed");

                        // extractPath i sil ve Apache24 ü extractPath ismi yap
                        Directory.Delete(extractPath, true);
                        Directory.Move(destPath, extractPath);

                        var apath = extractPath.Substring(extractPath.LastIndexOf("\\") + 1);

                        await StopLaragon();

                        Console.WriteLine("🚀 Apache version set to " + apath);
                        await SetLaragonIni("apache", "Version", apath);

                        await StartLaragon();
                    }

                    break;
                case "use":
                    if (args.Length < 3)
                    {
                        Console.WriteLine("Usage: pvm apache use <version>");
                        return;
                    }

                    var version = args[2];
                    var intersect = dirs.Where(x => x.Contains(version)).ToList();

                    if (intersect.Count() == 0)
                    {
                        Console.WriteLine("❌ Version not found");
                        return;
                    }

                    var upath = Path.Combine(apachePath, intersect.First());
                    upath = upath.Substring(upath.LastIndexOf("\\") + 1);

                    await StopLaragon();

                    Console.WriteLine("🚀 Apache version set to " + upath);
                    await SetLaragonIni("apache", "Version", upath);

                    await StartLaragon();
                    break;

                default:
                    Console.WriteLine("Unknown command");
                    break;
            }
        }

        private static async Task SetLaragonIni(string c1, string c2, string path)
        {
            string laragonIniPath = Path.Combine(laragonPath, "usr", "laragon.ini");
            ConfigParser configParser = new ConfigParser(laragonIniPath);
            configParser.SetValue(c1, c2, path);
            configParser.Save();
        }
    }
}