using Salaros.Configuration;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Text.Json;

namespace PVM
{
    class Program
    {
        static Dictionary<string, string> versions = new Dictionary<string, string>();
        static string pvmPath = "";
        static string laragonPath = "";
        static string phpPath = "";
        static string symPath = "";

        public Program()
        {
            // kurulduğu bilgisayardaki appdata pathini al
            pvmPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PVM");

            // pvmPath eğer PATH de yoksa ekle 
            string path = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User);
            if (!path.Contains(pvmPath))
            {
                path += ";" + pvmPath;
                Environment.SetEnvironmentVariable("PATH", path, EnvironmentVariableTarget.User);
            }

            string iniPath = Path.Combine(pvmPath, "pvm.ini");
            if (!Directory.Exists(pvmPath))
            {
                Directory.CreateDirectory(pvmPath);
            }

            // eğer içinde pvm.ini adında bir dosya yoksa yarat
            if (!File.Exists(iniPath))
            {
                File.Create(iniPath).Close();
            }

            // pvm.ini dosyasını oku ve laragon path'ini al eğer yoksa kullanıcdan laragon path'ini al
            /*
             * 
             * [laragon]
             * path = C:\laragon
             */
            ConfigParser configParser = new ConfigParser(iniPath);


            laragonPath = configParser.GetValue("laragon", "path");

            if (laragonPath == null || laragonPath == "")
            {
                Console.WriteLine("Laragon path not found. Please enter laragon path:");
                laragonPath = Console.ReadLine();
                configParser.SetValue("laragon", "path", laragonPath);
                configParser.Save();
            }

            // laragon path'ini kullanarak php path'ini al
            phpPath = Path.Combine(laragonPath, "bin", "php");
            symPath = Path.Combine(pvmPath, "php");

            string[] dirs = Directory.GetDirectories(phpPath);
            foreach (string dir in dirs)
            {
                string[] parts = dir.Split('\\');
                //php-7.4.33-Win32-vc15-x64 "-" lerden ayırarak version al
                string dirName = parts[parts.Length - 1];
                // php- ile başlayan ve -x64 ile bitenleri al
                if (dirName.StartsWith("php-") && dirName.EndsWith("-x64"))
                {
                    // - den parçala ve 1. indexi al
                    string[] exps = dirName.Split("-");
                    string version = exps[1];
                    versions.Add(version, dir);
                }
            }
        }

        static void Main(string[] args)
        {
            Program program = new Program();

            if (args.Length == 0)
            {
                Console.WriteLine("Usage: pvm <command>");
                return;
            }

            string command = args[0];
            string[] parameters = args.Length > 1 ? args[1].Trim().Split(' ') : new string[0];

            switch (command)
            {
                case "install":
                    program.Install(parameters);
                    break;
                case "use":
                    program.Use(parameters);
                    break;
                case "list":
                    program.List();
                    break;
                case "remove":
                    program.Remove(parameters);
                    break;
                case "help":
                    program.Help();
                    break;
                default:
                    Console.WriteLine("Command not found");
                    break;
            }
        }

        private void Help()
        {
            Console.WriteLine("Usage: pvm <command>");
            Console.WriteLine("Commands:");
            Console.WriteLine("  install <version>   Install a specific version of Python");
            Console.WriteLine("  use <version>       Use a specific version of Python");
            Console.WriteLine("  list                List all installed versions of Python");
            Console.WriteLine("  remove <version>    Remove a specific version of Python");
        }

        private void Remove(string[] parameters)
        {
            if (parameters.Length == 0)
            {
                Console.WriteLine("Usage: pvm remove <version>");
                return;
            }

            string version = parameters[0];

            List<string> versionList = new List<string>(versions.Keys);
            versionList.Sort();

            // girilen versiyon ile başlayanı bul
            string selectedVersion = "";
            foreach (string ver in versionList)
            {
                if (ver.StartsWith(version))
                {
                    selectedVersion = ver;
                    break;
                }
            }

            if (selectedVersion == "")
            {
                Console.WriteLine("Version not found");
                return;
            }

            Console.WriteLine("Removing " + selectedVersion);

            // kısayolu sil
            if (Directory.Exists(symPath))
            {
                Directory.Delete(symPath);
            }

            // versiyonu sil

            if (Directory.Exists(versions[selectedVersion]))
            {
                Directory.Delete(versions[selectedVersion], true);
            }

            Console.WriteLine("Removed " + selectedVersion);
            versions.Remove(selectedVersion);

            Console.WriteLine("Installed versions:");
            foreach (KeyValuePair<string, string> ver in versions)
            {
                Console.WriteLine(" * " + ver.Key);
            }
            Console.WriteLine("Please pvm use <version> to use a version.");
        }

        private void List()
        {
            Console.WriteLine("Installed versions:");
            foreach (KeyValuePair<string, string> version in versions)
            {
                Console.WriteLine(" * " + version.Key);
            }
        }

        private void Use(string[] parameters)
        {
            if (parameters.Length == 0)
            {
                Console.WriteLine("Usage: pvm use <version>");
                return;
            }

            string version = parameters[0];

            List<string> versionList = new List<string>(versions.Keys);
            versionList.Sort();

            // girilen versiyon ile başlayanı bul
            string selectedVersion = "";
            foreach (string ver in versionList)
            {
                if (ver.StartsWith(version))
                {
                    selectedVersion = ver;
                    break;
                }
            }


            if (selectedVersion == "")
            {
                Console.WriteLine("Version not found");
                return;
            }

            // varsa kısayolu sil
            if (Directory.Exists(symPath))
            {
                Directory.Delete(symPath);
            }


            string cmd = "mklink /D " + symPath + " " + versions[selectedVersion];
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = "/C " + cmd;
            startInfo.Verb = "runas";
            startInfo.UseShellExecute = true;
            Process.Start(startInfo);

            // eğer kısayol path de yoksa pathe ekle
            string path = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User);

            if (!path.Contains(symPath))
            {
                path += ";" + symPath;
                Environment.SetEnvironmentVariable("PATH", path, EnvironmentVariableTarget.User);
            }

            bool laragonRunning = Process.GetProcessesByName("laragon").Length > 0;

            if (laragonRunning)
            {
                // laragonu kapat
                Process[] processes = Process.GetProcessesByName("laragon");
                foreach (Process process in processes)
                {
                    process.Kill();
                }
            }

            // laragon.ini dosyasındaki versiyonu değiştir
            string laragonIniPath = Path.Combine(laragonPath, "usr", "laragon.ini");
            ConfigParser configParser = new ConfigParser(laragonIniPath);
            string laragonPHPVer = configParser.GetValue("php", "version");

            // latest index
            string[] exps = versions[selectedVersion].Split("\\");
            string latest = exps[exps.Length - 1];

            if (laragonPHPVer != latest)
            {
                configParser.SetValue("php", "version", latest);
                configParser.Save();
            }

            if (laragonRunning)
            {
                // laragonu başlat
                Process.Start(Path.Combine(laragonPath, "laragon.exe"));
            }
        }

        private void Install(string[] parameters)
        {
            if (parameters.Length == 0)
            {
                Console.WriteLine("Usage: pvm install <version>");
                return;
            }

            string version = parameters[0];

            string jsonUrl = "https://windows.php.net/downloads/releases/releases.json";

            // load json custom user-agent
            WebClient client = new WebClient();
            client.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.3");
            string json = client.DownloadString(jsonUrl);
            client.Dispose();

            // parse json
            JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            // jsondaki tüm keyleri al örn: "7.4" : { } ise 7.4
            List<string> keys = new List<string>(root.EnumerateObject().Select(x => x.Name));
            keys.Sort();

            // girilen versiyon ile başlayanı bul
            string selectedVersion = "";
            foreach (string ver in keys)
            {
                if (ver.StartsWith(version))
                {
                    selectedVersion = ver;
                    break;
                }
            }

            if (selectedVersion == "")
            {
                Console.WriteLine("Version not found");
                return;
            }

            // seçilen versiyon içinde ts- ile başlayan ve -x64 ile biten keyi bul
            string selectedKey = "";
            foreach (string key in root.GetProperty(selectedVersion).EnumerateObject().Select(x => x.Name))
            {
                if (key.StartsWith("ts-") && key.EndsWith("-x64"))
                {
                    selectedKey = key;
                    break;
                }
            }

            if (selectedKey == "")
            {
                Console.WriteLine("Thread safe version not found.");
                return;
            }

            // seçilen keyin içindeki zip içindeki pathi al
            string zipPath = root.GetProperty(selectedVersion).GetProperty(selectedKey).GetProperty("zip").GetProperty("path").GetString();
            string zipUrl = "https://windows.php.net/downloads/releases/" + zipPath;

            Console.WriteLine("Downloading " + zipPath);

            try
            {
                HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(zipUrl);
                // header 
                httpWebRequest.Method = "GET";
                httpWebRequest.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.3";
                HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                long fileSize = httpWebResponse.ContentLength;
                string fileName = zipPath;
                string filePath = Path.Combine(phpPath, fileName);

                using (Stream stream = httpWebResponse.GetResponseStream())
                {
                    using (FileStream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        byte[] buffer = new byte[1024];
                        int bytesRead;
                        long totalBytesRead = 0;
                        int percentage = 0;
                        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            fileStream.Write(buffer, 0, bytesRead);
                            totalBytesRead += bytesRead;
                            int newPercentage = (int)((totalBytesRead * 100) / fileSize);
                            if (newPercentage > percentage)
                            {
                                percentage = newPercentage;
                                Console.Write("\r" + percentage + "%");
                                if (percentage == 100)
                                {
                                    Console.WriteLine();
                                }
                            }
                        }

                        Console.WriteLine("\nDownload complete");
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            // zipi çıkart
            string zipFilePath = Path.Combine(phpPath, zipPath);
            string extractPath = Path.Combine(phpPath, zipPath.Replace(".zip", ""));

            Console.WriteLine("Extracting " + zipPath);
            ZipFile.ExtractToDirectory(zipFilePath, extractPath);
            File.Delete(zipFilePath);
            Console.WriteLine("Extract complete");

            // içindeki php.ini-development dosyasını php.ini olarak kopyala
            string phpIniPath = Path.Combine(extractPath, "php.ini-development");
            string phpIniDestPath = Path.Combine(extractPath, "php.ini");
            File.Copy(phpIniPath, phpIniDestPath, true);

            // https://curl.se/ca/cacert.pem dosyasını indir
            string cacertPath = Path.Combine(extractPath, "cacert.pem");
            WebClient webClient = new WebClient();
            webClient.DownloadFile("https://curl.se/ca/cacert.pem", cacertPath);
            webClient.Dispose();
            Console.WriteLine("cacert.pem downloaded");

            // curl fileinfo gd2 intl mbstring exif mysqli openssl pdo_mysql soap xsl zip extensionları aç
            string[] extensions = { "curl", "fileinfo", "gd2", "intl", "mbstring", "exif", "mysqli", "openssl", "pdo_mysql", "soap", "xsl", "zip" };
            // php.ini dosyasını aç ve extensionları aç
            string[] phpIniLines = File.ReadAllLines(phpIniDestPath);
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
                    {
                        phpIniLines[i] = phpIniLines[i].Replace(";", "");
                    }
                }

                if (phpIniLines[i].StartsWith(";curl.cainfo"))
                {
                    phpIniLines[i] = "curl.cainfo=" + cacertPath;
                }

                if (phpIniLines[i].StartsWith(";openssl.cafile"))
                {
                    phpIniLines[i] = "openssl.cafile=" + cacertPath;
                }
            }

            File.WriteAllLines(phpIniDestPath, phpIniLines);

            Console.WriteLine("php.ini updated");

            Console.WriteLine("Installed " + selectedVersion);
        }
    }
}