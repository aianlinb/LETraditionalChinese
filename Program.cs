using System.IO.Compression;
#if DEBUG
using System.Runtime.ExceptionServices;
#endif
using System.Security.Cryptography;

namespace LETraditionalChinese;

public static class Program {
	public static void Main(string[] args) {
		/*var hash = GenerateHashAsync(@"E:\Steam\steamapps\common\Last Epoch\Last Epoch_Data\StreamingAssets\aa\StandaloneWindows64\localization-string-tables-chinese(simplified)(zh)_assets_all.bundle")
			.AsTask().GetAwaiter().GetResult();
		File.WriteAllBytes("../../../zh-Hant/bundle.md5", hash);
		return;*/

		var zhPath = AppContext.BaseDirectory + "zh-Hant.zip";
		var fontPath = AppContext.BaseDirectory + "fonts.zip";
		if (!File.Exists(zhPath)) {
			zhPath = AppContext.BaseDirectory + "zh-Hant";
			if (!Directory.Exists(zhPath)) {
				Console.WriteLine("Missing file: zh-Hant.zip");
				goto end;
			}
		}
		if (!File.Exists(fontPath)) {
			fontPath = AppContext.BaseDirectory + "fonts";
			if (!Directory.Exists(fontPath)) {
				Console.WriteLine("Missing file: fonts.zip");
				goto end;
			}
		}

		string path;
		if (args.Length == 0) {
			path = SteamPath.FindGamePath("899770")!; // Last Epoch
			if (path is null) {
				Console.WriteLine("Unable to find the game path");
				Console.WriteLine("Usage: LETraditionalChinese <gameInstallationPath>");
				goto end;
			}
		} else if (args.Length != 1) {
			Console.WriteLine("Usage: LETraditionalChinese [gameInstallationPath]");
			goto end;
		} else
			path = args[0];
		Console.WriteLine("Found game path: " + path);

		try {
			Console.WriteLine("Patching localization . . .");
			var bundlePath = path + @"/Last Epoch_Data/StreamingAssets/aa/StandaloneLinux64";
			if (!Directory.Exists(bundlePath))
				bundlePath = path + @"/Last Epoch_Data/StreamingAssets/aa/StandaloneWindows64/localization-string-tables-chinese(simplified)(zh)_assets_all.bundle";

			var info = new FileInfo(bundlePath);
			if (info.Length > 819200) { // > 800 KB (Haven't patched yet)
				Span<byte> expected = stackalloc byte[16];
				using (var za = zhPath.EndsWith(".zip") ? ZipFile.OpenRead(zhPath) : null)
					if (za?.Entries.FirstOrDefault(e => e.Name == "bundle.md5") is ZipArchiveEntry md5) {
						using var s = md5.Open();
						s.ReadExactly(expected);
					} else if (File.Exists(zhPath + "/bundle.md5")) {
						using var s = File.OpenRead(zhPath + "/bundle.md5");
						s.ReadExactly(expected);
					} else
						goto skip; // Allow to skip the check by deleting the file
				using (var fs = File.OpenRead(bundlePath))
					if (!expected.SequenceEqual(MD5.HashDataAsync(fs).AsTask().GetAwaiter().GetResult())) {
						Console.WriteLine();
						Console.WriteLine("The localization has been updated by official. Please update this program.");
						Console.WriteLine("官方已更新簡中翻譯，請更新繁體語系後再套用");
						goto end;
					}
			}
		skip:
			LELocalePatch.Program.Run(bundlePath, zhPath, false, true);
			Console.WriteLine("Patching fonts . . .");
			LEFontPatch.Program.Run(path + @"/Last Epoch_Data", fontPath);
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