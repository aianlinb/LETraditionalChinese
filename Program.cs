#if DEBUG
using System.Runtime.ExceptionServices;
#endif

namespace LETraditionalChinese;
public static class Program {
	public static void Main(string[] args) {
        if (args.Length == 2 && args[0] == "--check") {
            using var fonts = new LEFontPatch.LEFontManager(Path.Combine(args[1], "Last Epoch_Data"));
            var count = fonts.TMPFonts.Values.Count(f => f["m_Name"].AsString.StartsWith("jf-openhuninn-2.1"));
            if (count < 5) throw new InvalidDataException("Complete Powder font set is not installed.");
            Console.WriteLine($"Verified {count} Powder font slots. Read-only check completed.");
            LEFontPatch.PermaFontPatch.Check(Path.Combine(args[1], "Last Epoch_Data"));
            return;
        }
        if (args.Length == 2 && args[0] == "--refresh-fonts") {
            if (System.Diagnostics.Process.GetProcessesByName("Last Epoch").Length != 0) {
                Console.Error.WriteLine("Please close Last Epoch before patching.");
                Environment.ExitCode = 1;
                return;
            }
            var fontFolder = Path.Combine(AppContext.BaseDirectory, "LETraditionalChinese");
            if (!Directory.Exists(fontFolder)) {
                Console.Error.WriteLine("The LETraditionalChinese data folder is missing beside this executable.");
                Environment.ExitCode = 1;
                return;
            }
            try {
                var dataPath = Path.Combine(args[1], "Last Epoch_Data");
                LEFontPatch.Program.Run(dataPath, fontFolder);
                LEFontPatch.BundleFontPatch.Run(dataPath, fontFolder);
                LEFontPatch.PermaFontPatch.Run(dataPath, fontFolder);
            } catch (Exception ex) {
                Console.Error.WriteLine(ex);
                Environment.ExitCode = 1;
            }
            return;
        }
        if (System.Diagnostics.Process.GetProcessesByName("Last Epoch").Length != 0) {
            Console.WriteLine("Please close Last Epoch before patching.");
            Environment.ExitCode = 1;
            return;
        }
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
            if (Directory.Exists(path)) LEFontPatch.BundleFontPatch.Run(gamePath + @"/Last Epoch_Data", path);
            if (Directory.Exists(path)) LEFontPatch.PermaFontPatch.Run(gamePath + @"/Last Epoch_Data", path);
			return; // Do not pause if success
		} catch (Exception ex) {
            Environment.ExitCode = 1;
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
}
