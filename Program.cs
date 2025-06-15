#if DEBUG
using System.IO;
using System.Runtime.ExceptionServices;
#endif
using System.Security.Cryptography;

namespace LETraditionalChinese;

public static class Program {
	public static void Main(string[] args) {
		var path = AppContext.BaseDirectory + "LETraditionalChinese.zip";
		if (!File.Exists(path)) {
			path = AppContext.BaseDirectory + "LETraditionalChinese";
			if (!Directory.Exists(path)) {
				Console.WriteLine("Missing file: LETraditionalChinese.zip");
				goto end;
			}
		}

		string gamePath;
		if (args.Length == 0) {
			gamePath = SteamPath.FindGamePath("899770")!; // Last Epoch
			if (gamePath is null) {
				Console.WriteLine("Unable to find the game path.");
				Console.WriteLine("Usage: LETraditionalChinese <gameInstallationPath>");
				goto end;
			}
		} else if (args.Length != 1) {
			Console.WriteLine("Usage: LETraditionalChinese [gameInstallationPath]");
			goto end;
		} else
			gamePath = args[0];
		Console.WriteLine("Found game path: " + gamePath);

		try {
			var bundlePath = gamePath + @"/Last Epoch_Data/StreamingAssets/aa/StandaloneLinux64/localization-string-tables-chinese(simplified)(zh)_assets_all.bundle";
			if (!File.Exists(bundlePath))
				bundlePath = gamePath + @"/Last Epoch_Data/StreamingAssets/aa/StandaloneWindows64/localization-string-tables-chinese(simplified)(zh)_assets_all.bundle";

			if (!File.Exists(bundlePath)) {
				Console.WriteLine("Bundle file not found: " + bundlePath);
				goto end;
			}

			Console.WriteLine("Patching localization . . .");
			LELocalePatch.Program.Run(bundlePath,
				path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? path : path + "/dictionary.json",
				LELocalePatch.Program.Mode.Translate);
			Console.WriteLine("Patching fonts . . .");
			LEFontPatch.Program.Run(gamePath + @"/Last Epoch_Data", path);
			return; // Do not pause if success
		} catch (Exception ex) {
			var tmp = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine("Error");
			Console.Error.WriteLine(ex);
			Console.ForegroundColor = tmp;
#if DEBUG
			ExceptionDispatchInfo.Capture(ex).Throw(); // Throw	to the debugger
#endif
		}
	end:
		Console.WriteLine();
		Console.Write("Enter to exit . . .");
		Console.ReadLine();
	}

	public static async ValueTask<byte[]> GenerateHashAsync(string bundlePath, CancellationToken cancellationToken = default) {
		using var fs = File.OpenRead(bundlePath);
		return await MD5.HashDataAsync(fs, cancellationToken);
	}
}