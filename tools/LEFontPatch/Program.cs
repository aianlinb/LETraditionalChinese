using System.Buffers;
using System.IO.Compression;
#if DEBUG
using System.Runtime.ExceptionServices;
#endif
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LEFontPatch {
	public static class Program {
		public static void Main(string[] args) {
            if (args.Length == 3 && args[0] == "--perma-only") {
                PermaFontPatch.Run(args[1], args[2]);
                return;
            }
            if (args.Length == 3 && args[0] == "--bundle-only") {
                BundleFontPatch.Run(args[1], args[2]);
                return;
            }
			if (args.Length != 2) {
				Console.WriteLine("Usage: LEFontPatch <gamePath> <zipPath|folderPath>");
				Console.WriteLine("   or: LEFontPatch --dump <gamePath>");
				if (args.Length == 0)
					goto pause;
				return;
			}
			try {
				if (args[0].Equals("--dump", StringComparison.OrdinalIgnoreCase))
					DumpFallbacks(args[1] + "/Last Epoch_Data");
				else {
					Run(args[0] + "/Last Epoch_Data", args[1]);
					return; // Do not pause if success
				}
			} catch (Exception ex) {
				var tmp = Console.ForegroundColor;
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("Error");
				Console.Error.WriteLine(ex);
				Console.ForegroundColor = tmp;
#if DEBUG
				ExceptionDispatchInfo.Capture(ex).Throw(); // Throw to the debugger
#endif
			}
		pause:
			Console.WriteLine();
			Console.Write("Enter to exit . . .");
			Console.ReadLine();
		}

		public static void Run(string gameDataPath, string zipOrFolderPath) {
			using var zip = Directory.Exists(zipOrFolderPath) ? null : ZipFile.OpenRead(zipOrFolderPath);
			var zipEntries = zip?.Entries.ToDictionary(e => e.FullName.ToLowerInvariant(), e => e);

			byte[]? buffer = null;
			try {
				byte[] GetFile(string fileName) {
					if (zip is null)
						return File.ReadAllBytes(Path.Combine(zipOrFolderPath, fileName));
					if (!zipEntries!.TryGetValue(fileName.ToLowerInvariant(), out var e))
						throw new FileNotFoundException(fileName + " not found in the zip file");
					Console.WriteLine("Zip: Getting " + fileName);
					var len = (int)e.Length;
					if (buffer is not null && buffer.Length < len) {
						ArrayPool<byte>.Shared.Return(buffer);
						buffer = null;
					}
					buffer ??= ArrayPool<byte>.Shared.Rent(len);
					using (var s = e.Open())
						s.ReadExactly(buffer, 0, len);
					return buffer.Length == len ? buffer : buffer[..len];
				}
#nullable disable  // Json DOM
				var json = JsonNode.Parse(GetFile("manifest.json"), null, new() {
					AllowTrailingCommas = true,
					CommentHandling = JsonCommentHandling.Skip
				}).AsObject();

				Console.WriteLine("Reading TMP_FontAssets");
				LibCpp2IL.Logging.LibLogger.ShowVerbose = false;
				using var manager = new LEFontManager(gameDataPath);
				Console.WriteLine();

				// Reapplication refreshes only the source font, without appending duplicate atlas/material assets.
                if (json["cancelIfNoFontReplaced"]?.GetValue<bool>() == true && json["fontReplacements"] is JsonObject planned &&
                    !manager.TMPFonts.Values.Any(f => planned.ContainsKey(f["m_Name"].AsString))) {
                    foreach(var item in json["sourceFontFiles"]?.AsArray() ?? [])manager.TryAddFontFile(GetFile((string)item["path"]),out _);
                    manager.Save();
                    Console.WriteLine("Existing Powder UI fonts retained; shared source refreshed.");
                    return;
                }
                var sourceFonts = json["sourceFontFiles"]?.AsArray().Select(n => {
					manager.TryAddFontFile(GetFile((string)n["path"]), out var i);
					return i;
				}).ToArray() ?? [];

				var atlases = json["atlases"]?.AsArray().Select(
					n => manager.AddAtlas(GetFile((string)n["path"]))).ToArray() ?? [];

				var materials = json["materials"]?.AsArray().Select(n => {
					var atlas = atlases[(int)n["atlas"]];
					return (manager.AddMaterial(GetFile((string)n["path"]), atlas), atlas);
				}).ToArray() ?? [];

				var fonts = json["fonts"]?.AsArray() ?? [];
				var fontDic = new Dictionary<string, List<int>>(manager.TMPFonts.Count);
				BuildFontDic();
				void BuildFontDic() {
					foreach (var kvp in manager.TMPFonts) {
						var name = kvp.Value["m_Name"].AsString;
						if (!fontDic.TryGetValue(name, out var list))
							fontDic.Add(name, list = new(1));
						list.Add(kvp.Key);
					}
				}
				var excludes = new HashSet<int>();
				if (json["fontReplacements"] is JsonObject replacement) {
					Console.WriteLine("Replacing fonts:");
					var rCache = new Dictionary<int, int>(replacement.Count);
					var sCache = new Dictionary<int, int>();
					foreach (var (fontName, fi) in replacement) {
						var fontIndex = (int)fi;
						if (!fontDic.TryGetValue(fontName, out var list)) {
							var tmp = Console.ForegroundColor;
							Console.ForegroundColor = ConsoleColor.Yellow;
							Console.WriteLine("\tWarning: Font not found: " + fontName);
							Console.ForegroundColor = tmp;
							continue;
						}
						foreach (var i in list) {
							if ((i < 0 ? rCache : sCache).TryGetValue(fontIndex, out var j)) {
								var data = manager.TMPFonts[j];
								manager.GetAsset(i).SetNewData(data);
								manager.TMPFonts[i] = data;
							} else {
								var font = fonts[fontIndex].AsObject();
								var (material, atlas) = materials[(int)font["material"]];
								var sourceFont = font.TryGetPropertyValue("sourceFont", out var sf) ? sourceFonts[(int)sf] : -1;
								var fontData = JsonNode.Parse(GetFile((string)font["path"])).AsObject();
								manager.ReplaceTMPFont(i, fontData, atlas, material, sourceFont);
								(i < 0 ? rCache : sCache)[fontIndex] = i;
							}
							excludes.Add(i);
							Console.Write('\t');
							Console.WriteLine("Replaced: " + fontName);
						}
					}
				}

				if (excludes.Count == 0 && json["cancelIfNoFontReplaced"] is JsonValue v && (bool)v) {
					manager.Save(); // A font update can still refresh the shared source TTF.
					Console.WriteLine("No font replaced, cancelling . . .");
					return;
				}

				if (json["removeCharacters"] is JsonObject rC) {
					Console.WriteLine("Start removing characters . . .");
					var characters = new HashSet<int>();
					if (rC["fromCharacters"] is JsonArray from1) {
						characters.EnsureCapacity(from1.Count);
						foreach (var c in from1)
							characters.Add((int)c);
					}
					BuildFontDic();
					if (rC["fromFont"] is JsonArray from2) {
						foreach (var f in from2) {
							foreach (var fi in fontDic[(string)f]) {
								var chars = manager.GetCharacters(fi, out var count);
								characters.EnsureCapacity(characters.Count + count);
								characters.UnionWith(chars);
							}
						}
					}

					if (rC["excludeReplaced"] is not JsonValue v2 || !(bool)v2)
						excludes.Clear();
					if (rC["excludeFonts"] is JsonArray efs)
						foreach (var ef in efs) {
							if (!fontDic.TryGetValue((string)ef, out var fs)) {
								var tmp = Console.ForegroundColor;
								Console.ForegroundColor = ConsoleColor.Yellow;
								Console.WriteLine("\tWarning: Font not found: " + (string)ef);
								Console.ForegroundColor = tmp;
								continue;
							}

							excludes.EnsureCapacity(excludes.Count + fs.Count);
							excludes.UnionWith(fs);
						}

					var excludedFonts = manager.TMPFonts.Keys.Except(excludes).ToArray();
					Console.WriteLine($"Removing {characters.Count} characters in {excludedFonts.Length} fonts");
					if (characters.Count != 0)
						manager.RemoveCharacters(excludedFonts, characters);
				}
#nullable restore
                manager.NormalizePowderFallbacks();
				Console.WriteLine("Saving . . . (may takes minutes)");
				manager.Save();
				Console.WriteLine("Done!");
			} finally {
				if (buffer is not null)
					ArrayPool<byte>.Shared.Return(buffer);
			}
		}

		public static void DumpFallbacks(string gameDataPath) {
			Console.WriteLine("Reading TMP_FontAssets");
			using var manager = new LEFontManager(gameDataPath);
			Console.WriteLine();

			Console.WriteLine(manager.DumpFontFallbacks());
		}
	}
}
