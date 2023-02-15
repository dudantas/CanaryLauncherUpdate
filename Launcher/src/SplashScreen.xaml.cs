using System;
using System.Windows;
using System.IO;
using System.Net;
using System.Windows.Threading;
using System.Net.Http;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Reflection;
using Ionic.Zip;
using Newtonsoft.Json;
using LauncherConfig;

namespace Launcher
{
	public partial class SplashScreen : Window
	{
		static string launcherConfigUrl = "https://raw.githubusercontent.com/gccris/molten_client/main/launcher_config.json";
		static ClientConfig clientConfig = null;
		static string newLauncherVersion = null;
		static string launcherExecutable = null;
		static string newLauncherUrl = null;
		static string packageFolder = null;

		WebClient webClient = new WebClient();
		DispatcherTimer timer = new DispatcherTimer();

		public SplashScreen()
		{
			InitializeComponent();

			 // Load launcher config file if URL is valid and connection is successful
			if (CheckUrlAndConnection(launcherConfigUrl))
			{
				clientConfig = ClientConfig.loadFromFile(launcherConfigUrl);
				newLauncherVersion = clientConfig.launcherVersion;
				launcherExecutable = clientConfig.launcherExecutable;
				newLauncherUrl = clientConfig.newLauncherUrl;
				packageFolder = clientConfig.packageFolder;
			} else {
				StartClientLauncher();
			}

			// Start the client if the versions are the same
			if (File.Exists(GetLauncherPath() + "/launcher_config.json") && GetProgramVersion(GetLauncherPath()) != newLauncherVersion || (!File.Exists(GetLauncherPath() + "/" + launcherExecutable))) {
				TaskDownloadClientLauncher(newLauncherUrl);
			} else if (File.Exists(GetLauncherPath() + "/" + launcherExecutable)) {
				StartClientLauncher();
			}
		}

		public async void TemporizedSplashScreen(object sender, EventArgs e)
		{
			unpackage(GetLauncherPath() + "/launcher.zip", ExtractExistingFileAction.OverwriteSilently);

			string localPath = Path.Combine(GetLauncherPath(), "launcher_config.json");
			await webClient.DownloadFileTaskAsync(launcherConfigUrl, localPath);
			CreateShortcut();
			if (File.Exists(GetLauncherPath() + "/launcher.zip")) {
				File.Delete(GetLauncherPath() + "/launcher.zip");
			}
			StartClientLauncher();
		}

		private bool CheckUrlAndConnection(string url)
		{
			Uri uri;
			if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
			{
				// URL is not valid
				return false;
			}

			using (var client = new HttpClient())
			{
				HttpResponseMessage response;
				try
				{
					response = client.GetAsync(uri).Result;
				}
				catch (AggregateException)
				{
					// Connection error occurred
					return false;
				}

				if (response.IsSuccessStatusCode)
				{
					// URL and connection are valid
					return true;
				}
				else
				{
					// Connection successful, but URL is not valid or file not found
					return false;
				}
			}
		}

		private string GetLauncherPath(bool onlyBaseDirectory = false)
		{
			string launcherPath = "";
			if (string.IsNullOrEmpty(packageFolder) || onlyBaseDirectory) {
				launcherPath = AppDomain.CurrentDomain.BaseDirectory.ToString();
			} else {
				launcherPath = AppDomain.CurrentDomain.BaseDirectory.ToString() + "/" + packageFolder;
			}
			
			return launcherPath;
		}

		string GetProgramVersion(string path)
		{
			string json = GetLauncherPath() + "/launcher_config.json";
			using (StreamReader stream = new StreamReader(json))
			{
				dynamic jsonString = stream.ReadToEnd();
				dynamic jsonDeserialized = JsonConvert.DeserializeObject(jsonString);
				return jsonDeserialized.launcherVersion;
			}
		}

		private bool StartClientLauncher()
		{
			if (File.Exists(GetLauncherPath() + "/" + launcherExecutable)) {
				Process.Start(GetLauncherPath() + "/" + launcherExecutable);
				this.Close();
				return true;
			}
			return false;
		}

		private void unpackage(string path, ExtractExistingFileAction existingFileAction)
		{
			using (ZipFile modZip = ZipFile.Read(path))
			{
				foreach (ZipEntry zipEntry in modZip)
				{
					zipEntry.Extract(GetLauncherPath(), existingFileAction);
				}
			}
		}

		static void CreateShortcut()
		{
			string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
			string shortcutPath = Path.Combine(desktopPath, "Molten.lnk");
			Type t = Type.GetTypeFromProgID("WScript.Shell");
			dynamic shell = Activator.CreateInstance(t);
			var lnk = shell.CreateShortcut(shortcutPath);
			try
			{
				lnk.TargetPath = Assembly.GetExecutingAssembly().Location.Replace(".dll", ".exe");
				lnk.Description = "Molten";
				lnk.Save();
			}
			finally
			{
				System.Runtime.InteropServices.Marshal.FinalReleaseComObject(lnk);
			}
		}

		private void TaskDownloadClientLauncher(string url)
		{
			Directory.CreateDirectory(GetLauncherPath());
			webClient.DownloadFileAsync(new Uri(url), GetLauncherPath() + "/launcher.zip");
			timer.Tick += new EventHandler(TemporizedSplashScreen);
			timer.Interval = new TimeSpan(0, 0, 1);
			timer.Start();
		}
	}
}
