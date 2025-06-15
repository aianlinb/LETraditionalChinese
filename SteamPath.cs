using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace LETraditionalChinese;
public static partial class SteamPath {
	public static string? GetSteamPath() {
		string? result;
		if (OperatingSystem.IsWindows()) {
			try {
				result = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam", RegistryKeyPermissionCheck.ReadSubTree,
					System.Security.AccessControl.RegistryRights.QueryValues)?.GetValue("InstallPath", null) as string;
				if (Directory.Exists(result))
					return result;

				result = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam", RegistryKeyPermissionCheck.ReadSubTree,
					System.Security.AccessControl.RegistryRights.QueryValues)?.GetValue("InstallPath", null) as string;
				if (Directory.Exists(result))
					return result;
			} catch { /*ignore*/ }
			result = nint.Size == sizeof(int) ? @"C:\Program Files\Steam" : @"C:\Program Files (x86)\Steam";
			if (Directory.Exists(result))
				return result;
		} else {
			var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify);
			if (OperatingSystem.IsMacOS()) {
				result = home + "/Library/Application Support/Steam";
				if (Directory.Exists(result))
					return result;
			} else if (OperatingSystem.IsLinux()) {
				result = home + "/.steam/steam";
				if (Directory.Exists(result))
					return result;

				result = home + "/.local/share/Steam";
				if (Directory.Exists(result))
					return result;
			}
		}
		return null;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string? GetSteamAppsPath(string? steamPath = null) {
		steamPath ??= GetSteamPath();
		return steamPath is null ? null :
			$"{steamPath}{Path.DirectorySeparatorChar}{(OperatingSystem.IsWindows() ? "steamapps" : "SteamApps")}";
	}

	[GeneratedRegex("\"path\"\\s*\"([^\"]+)\"")]
	private static partial Regex LibraryRegex();
	public static IEnumerable<string> GetSteamLibraryFoldersPath() {
		var steamapps = GetSteamAppsPath();
		if (steamapps is null)
			yield break;

		if (Directory.Exists(steamapps))
			yield return steamapps;

		var vdf = steamapps + "/libraryfolders.vdf";
		if (!File.Exists(vdf))
			yield break;
		foreach (var match in LibraryRegex().Matches(File.ReadAllText(vdf)) as IList<Match>) {
			var path = GetSteamAppsPath(Regex.Unescape(match.Groups[1].Value));
			if (path != steamapps && Directory.Exists(path))
				yield return path;
		}
	}

	[GeneratedRegex("\"installdir\"\\s*\"([^\"]+)\"")]
	private static partial Regex GameInstallDirRegex();
	public static string? FindGamePath(string appId) {
		foreach (var path in GetSteamLibraryFoldersPath()) {
			var path2 = $"{path}/appmanifest_{appId}.acf";
			if (File.Exists(path2)) {
				var match = GameInstallDirRegex().Match(File.ReadAllText(path2));
				if (match.Success) {
					path2 = $"{path}{Path.DirectorySeparatorChar}common{Path.DirectorySeparatorChar}{Regex.Unescape(match.Groups[1].Value)}";
					if (Directory.Exists(path2))
						return path2;
				}
			}
		}
		return null;
	}
}