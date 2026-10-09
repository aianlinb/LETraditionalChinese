using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace LEFontPatch;
public static class PermaFontPatch {
 public static void Check(string dataPath) {
  var path=Path.Combine(dataPath,"StreamingAssets","LEAssetBundles","PermaLoad.bundle");
  var manager=new AssetsManager();
  using(var tpk=Assembly.GetExecutingAssembly().GetManifestResourceStream("LEFontPatch.classdata.tpk"))manager.LoadClassPackage(tpk!);
  try {
   var bundle=manager.LoadBundleFile(path);var asset=manager.LoadAssetsFileFromBundle(bundle,0);int count=0;
   foreach(var info in asset.file.AssetInfos.Where(i=>i.TypeId==(int)AssetClassID.MonoBehaviour)) {
    asset.file.Reader.Position=info.GetAbsoluteByteOffset(asset.file)+28;
    var length=asset.file.Reader.ReadInt32();
    if(length<1||length>128)continue;
    var name=System.Text.Encoding.UTF8.GetString(asset.file.Reader.ReadBytes(length));
    if(name.StartsWith("jf-openhuninn-2.1"))count++;
   }
   if(count!=5)throw new InvalidDataException($"PermaLoad Powder slots: expected 5, found {count}.");
   manager.LoadClassDatabaseFromPackage(asset.file.Metadata.UnityVersion);
   ValidateNativeChat(manager,asset);
   Console.WriteLine("Verified 5 Powder font slots in PermaLoad.bundle.");
  }finally{manager.UnloadAll();}
 }
 public static void Run(string dataPath,string folder) {
  var path=Path.Combine(dataPath,"StreamingAssets","LEAssetBundles","PermaLoad.bundle");
  if(!File.Exists(path)){Console.WriteLine("PermaLoad bundle not present; skipped.");return;}
  var manager=new AssetsManager();
  using(var tpk=Assembly.GetExecutingAssembly().GetManifestResourceStream("LEFontPatch.classdata.tpk"))manager.LoadClassPackage(tpk!);
  try {
   var bundle=manager.LoadBundleFile(path);
   var assets=manager.LoadAssetsFileFromBundle(bundle,0);
   manager.LoadClassDatabaseFromPackage(assets.file.Metadata.UnityVersion);
   var fonts=new Dictionary<long,AssetTypeValueField>();
   foreach(var info in assets.file.AssetInfos.Where(i=>i.TypeId==(int)AssetClassID.MonoBehaviour)) {
    var field=manager.GetBaseField(assets,info);
    if(!field["m_CharacterTable"].IsDummy)fonts[info.PathId]=field;
   }
   if(fonts.Values.Any(f=>f["m_Name"].AsString=="jf-openhuninn-2.1 SDF32")) {
    var dynamicFont=fonts.Values.Single(f=>f["m_Name"].AsString.StartsWith("jf-openhuninn-2.1")&&f["m_AtlasPopulationMode"].AsInt==1);
    ValidateNativeChat(manager,assets);
    RefreshDynamicSource(path,bundle,assets,manager,dynamicFont,folder);
    return;
   }
   var nativeFonts=fonts.ToDictionary(p=>p.Key,p=>p.Value.Clone());
   if(nativeFonts.Count!=29)throw new InvalidDataException("Expected 29 original Season 5 font assets before patching.");
   var originals=assets.file.AssetInfos.ToDictionary(i=>i.PathId,i=>Hash(assets,i));
   var expected=new Dictionary<long,byte[]>();
   var nextId=assets.file.AssetInfos.Max(i=>i.PathId)+1;
   long Add(byte[] data,AssetClassID type) {
    var info=AssetFileInfo.Create(assets.file,nextId++,(int)type,ushort.MaxValue,manager.ClassDatabase);
    info.SetNewData(data);assets.file.AssetInfos.Add(info);expected[info.PathId]=data;return info.PathId;
   }
   void Set(long id,byte[] bytes){assets.file.GetAssetInfo(id).SetNewData(bytes);expected[id]=bytes;}
   byte[] FileData(string name)=>File.ReadAllBytes(Path.Combine(folder,"fonts",name));
   JsonObject Pointer(long id)=>new(){["m_FileID"]=0,["m_PathID"]=id};
   var source=FileData("jf-openhuninn-2.1.dat");
   var nameLength=BitConverter.ToInt32(source);
   var offset=(4+nameLength+4+3)&~3;
   source.AsSpan(offset,12).Clear();source.AsSpan(offset+16,12).Clear();
   var sourceId=Add(source,AssetClassID.Font);
   var atlasIds=new[]{Add(FileData("jf-openhuninn-2.1 Atlas.dat"),AssetClassID.Texture2D),Add(FileData("jf-openhuninn-2.1 Atlas (Dynamic).dat"),AssetClassID.Texture2D)};
   var firstFont=fonts.First().Value;
   var oldMat=manager.GetExtAsset(assets,firstFont["m_Material"]);
   var materialIds=new long[3];
   var materialNames=new[]{"jf-openhuninn-2.1 Material.dat","jf-openhuninn-2.1 Material (Bold).dat","jf-openhuninn-2.1 Material (Dynamic).dat"};
   for(int i=0;i<3;i++) {
    var raw=FileData(materialNames[i]);
    var mat=oldMat.baseField.TemplateField.MakeValue(new AssetsFileReader(new MemoryStream(raw)),0);
    if(!raw.AsSpan().SequenceEqual(mat.WriteToByteArray()))throw new InvalidDataException("Perma material layout mismatch.");
    mat.Children[mat.Children.FindIndex(f=>f.FieldName=="m_Shader")]=oldMat.baseField["m_Shader"].Clone();
    if(oldMat.file!=assets)throw new InvalidDataException("Perma shader dependency needs explicit mapping.");
    var texture=mat["m_SavedProperties"]["m_TexEnvs"]["Array"].Children.Single(f=>f[0].AsString=="_MainTex")[1]["m_Texture"];
    texture["m_FileID"].AsInt=0;texture["m_PathID"].AsLong=atlasIds[i==2?1:0];
    materialIds[i]=Add(mat.WriteToByteArray(),AssetClassID.Material);
   }
   var definitions=new[]{"jf-openhuninn-2.1 SDF32.json","jf-openhuninn-2.1 SDF32 (Bold).json","jf-openhuninn-2.1 SDFAA (Dynamic).json"};
   var targets=new Dictionary<string,int>{{"Forum-Thin SDF16 (Cyrillic)",0},{"Forum-SemiBold SDF (Cyrillic)",1},{"Forum-Bold SDF (Cyrillic)",1},{"Caladea-Bold SDF",1},{"Forum-Regular SDF (Cyrillic, Cinzel Adapted)",2}};
   var replaced=new Dictionary<long,int>();
   foreach(var (id,field) in fonts.ToArray()) {
    if(!targets.TryGetValue(field["m_Name"].AsString,out int kind))continue;
    var json=JsonNode.Parse(File.ReadAllText(Path.Combine(folder,"fonts",definitions[kind])))!.AsObject();
    json["m_Script"]=new JsonObject{["m_FileID"]=field["m_Script"]["m_FileID"].AsInt,["m_PathID"]=field["m_Script"]["m_PathID"].AsLong};
    json["m_Material"]=Pointer(materialIds[kind]);
    json["m_AtlasTextures"]!["Array"]=new JsonArray(Pointer(atlasIds[kind==2?1:0]));
    json["m_SourceFontFile"]=Pointer(kind==2?sourceId:0);
    var bytes=LEFontManager.JsonToBytes(json,field.TemplateField);
    var replacement=field.TemplateField.MakeValue(new AssetsFileReader(new MemoryStream(bytes)),0);
    fonts[id]=replacement;replaced[id]=kind;Set(id,bytes);
   }
   if(replaced.Count!=5)throw new InvalidDataException($"Expected 5 Perma font slots, found {replaced.Count}.");
   var regularId=replaced.First(p=>p.Value==0).Key;
   var boldId=replaced.First(p=>p.Value==1).Key;
   var dynamicId=replaced.First(p=>p.Value==2).Key;
   var covered=fonts[regularId]["m_CharacterTable"]["Array"].Children.Select(c=>c["m_Unicode"].AsUInt).ToHashSet();
   foreach(var (id,field) in fonts) {
    if(id==dynamicId)continue;
    long target;
    if(replaced.ContainsKey(id))target=dynamicId;
    else {
     field["m_AtlasPopulationMode"].AsInt=0;
     var chars=field["m_CharacterTable"]["Array"];
     chars.Children.RemoveAll(c=>{var u=c["m_Unicode"].AsUInt;return covered.Contains(u)||u is >=0x3000 and <=0x303f or >=0x3400 and <=0x9fff or >=0xf900 and <=0xfaff or >=0xff00 and <=0xffef;});
     chars.AsArray=new(chars.Children.Count);
     target=field["m_Name"].AsString.Contains("Bold",StringComparison.OrdinalIgnoreCase)?boldId:regularId;
    }
    var array=field["m_FallbackFontAssetTable"]["Array"];
    var ptr=ValueBuilder.DefaultValueFieldFromArrayTemplate(array);ptr["m_FileID"].AsInt=0;ptr["m_PathID"].AsLong=target;
    array.Children.Insert(0,ptr);array.AsArray=new(array.Children.Count);Set(id,field.WriteToByteArray());
   }
   // Snapshot fonts before UI normalization; private chat copies must never inherit Powder fallbacks.
   var nativeIds=nativeFonts.Keys.ToDictionary(id=>id,id=>nextId++);
   void RemapNative(AssetTypeValueField field) {
    if(!field["m_FileID"].IsDummy&&!field["m_PathID"].IsDummy&&field["m_FileID"].AsInt==0&&nativeIds.TryGetValue(field["m_PathID"].AsLong,out var mapped))field["m_PathID"].AsLong=mapped;
    foreach(var child in field.Children)RemapNative(child);
   }
   foreach(var (id,original) in nativeFonts) {
    var clone=original.Clone();clone["m_Name"].AsString+=" (Native Chat)";RemapNative(clone);
    if(id==4883444590345449907) {
     var fallback=clone["m_FallbackFontAssetTable"]["Array"];
     var ptr=ValueBuilder.DefaultValueFieldFromArrayTemplate(fallback);ptr["m_FileID"].AsInt=0;ptr["m_PathID"].AsLong=nativeIds[7321910592313900844];
     fallback.Children.Add(ptr);fallback.AsArray=new(fallback.Children.Count);
    }
    var info=AssetFileInfo.Create(assets.file,nativeIds[id],114,assets.file.GetAssetInfo(id).GetScriptIndex(assets.file),manager.ClassDatabase);
    info.TypeIdOrIndex=assets.file.GetAssetInfo(id).TypeIdOrIndex;
    var bytes=clone.WriteToByteArray();info.SetNewData(bytes);assets.file.AssetInfos.Add(info);expected[info.PathId]=bytes;
   }
   var messageInfo=assets.file.GetAssetInfo(5720891894229404983);
   var message=manager.GetBaseField(assets,messageInfo);
   if(message["m_fontAsset"]["m_PathID"].AsLong!=4883444590345449907)throw new InvalidDataException("Unexpected native chat message font; unsupported game layout.");
   message["m_fontAsset"]["m_PathID"].AsLong=nativeIds[4883444590345449907];Set(messageInfo.PathId,message.WriteToByteArray());
   ValidateNativeChat(manager,assets);
   // Check complete font/material/atlas pairing before writing.
   foreach(var (id,kind) in replaced) {
    var font=fonts[id];
    var matBytes=expected[materialIds[kind]];
    var mat=oldMat.baseField.TemplateField.MakeValue(new AssetsFileReader(new MemoryStream(matBytes)),0);
    var gradient=mat["m_SavedProperties"]["m_Floats"]["Array"].Children.Single(f=>f[0].AsString=="_GradientScale")[1].AsFloat;
    if(gradient!=font["m_AtlasPadding"].AsInt+1)throw new InvalidDataException("Perma gradient/padding mismatch.");
   }
   bundle.file.BlockAndDirInfo.DirectoryInfos[0].SetNewData(assets.file);
   var unpacked=path+".unpacked";var temporary=path+".new";
   using(var writer=new AssetsFileWriter(unpacked))bundle.file.Write(writer);
   bundle.file.Close();bundle.file.Read(new AssetsFileReader(File.OpenRead(unpacked)));
   using(var writer=new AssetsFileWriter(temporary))bundle.file.Pack(writer,AssetBundleCompressionType.LZ4);
   var check=new AssetsManager();
   using(var tpk=Assembly.GetExecutingAssembly().GetManifestResourceStream("LEFontPatch.classdata.tpk"))check.LoadClassPackage(tpk!);
   var checkBundle=check.LoadBundleFile(temporary);var output=check.LoadAssetsFileFromBundle(checkBundle,0);
   check.LoadClassDatabaseFromPackage(output.file.Metadata.UnityVersion);ValidateNativeChat(check,output);
   if(output.file.AssetInfos.Count!=originals.Count+35)throw new InvalidDataException("Perma asset count mismatch.");
   foreach(var info in output.file.AssetInfos){var wanted=expected.TryGetValue(info.PathId,out var bytes)?Convert.ToHexString(SHA256.HashData(bytes)):originals[info.PathId];if(Hash(output,info)!=wanted)throw new InvalidDataException("Perma asset payload mismatch.");}
   check.UnloadAll();manager.UnloadAll();File.Move(temporary,path,true);File.Delete(unpacked);
   Console.WriteLine($"PermaLoad verified: {fonts.Count} fonts handled; all other original payloads unchanged; 6 Powder assets and 29 isolated native chat fonts added.");
  } finally{manager.UnloadAll();}
 }
 public static void ValidateNativeChat(AssetsManager manager,AssetsFileInstance assets) {
  var native=new Dictionary<long,AssetTypeValueField>();
  foreach(var info in assets.file.AssetInfos.Where(i=>i.TypeId==114)) {
   var field=manager.GetBaseField(assets,info);
   if(field["m_Name"].AsString.EndsWith(" (Native Chat)"))native[info.PathId]=field;
  }
  if(native.Count!=29)throw new InvalidDataException("Native chat fonts are missing. For an installation patched by an older version, restore with Steam verification before applying this version.");
  var message=manager.GetBaseField(assets,assets.file.GetAssetInfo(5720891894229404983));
  if(!native.TryGetValue(message["m_fontAsset"]["m_PathID"].AsLong,out var font)||font["m_Name"].AsString!="Caladea SDF (Native Chat)")throw new InvalidDataException("Chat does not reference its isolated native font.");
  var chinese=native.Values.Single(f=>f["m_Name"].AsString=="NotoSansSC-Regular SDF (Full Set) (Native Chat)");
  if(chinese["m_CharacterTable"]["Array"].Children.Count!=5046)throw new InvalidDataException("Original native chat Chinese character table was changed.");
  Console.WriteLine("Verified isolated native chat font references and 5,046-character original Chinese table.");
 }
 static void RefreshDynamicSource(string path,BundleFileInstance bundle,AssetsFileInstance assets,AssetsManager manager,AssetTypeValueField dynamicFont,string folder) {
  var source=manager.GetExtAsset(assets,dynamicFont["m_SourceFontFile"]);
  if(source.file!=assets||source.info.TypeId!=(int)AssetClassID.Font)throw new InvalidDataException("Perma dynamic font source is not local.");
  var raw=File.ReadAllBytes(Path.Combine(folder,"fonts","jf-openhuninn-2.1.dat"));
  var field=source.baseField.TemplateField.MakeValue(new AssetsFileReader(new MemoryStream(raw)),0);
  if(!raw.AsSpan().SequenceEqual(field.WriteToByteArray()))throw new InvalidDataException("Updated Perma source font layout mismatch.");
  var nameLength=BitConverter.ToInt32(raw);
  var offset=(4+nameLength+4+3)&~3;
  raw.AsSpan(offset,12).Clear();raw.AsSpan(offset+16,12).Clear();
  var originals=assets.file.AssetInfos.ToDictionary(i=>i.PathId,i=>Hash(assets,i));
  var expected=SHA256.HashData(raw);
  source.info.SetNewData(raw);
  bundle.file.BlockAndDirInfo.DirectoryInfos[0].SetNewData(assets.file);
  var unpacked=path+".unpacked";var temporary=path+".new";
  try {
   using(var writer=new AssetsFileWriter(unpacked))bundle.file.Write(writer);
   bundle.file.Close();using(var reader=File.OpenRead(unpacked)) {
    bundle.file.Read(new AssetsFileReader(reader));
    using var writer=new AssetsFileWriter(temporary);bundle.file.Pack(writer,AssetBundleCompressionType.LZ4);
   }
   var check=new AssetsManager();
   using(var tpk=Assembly.GetExecutingAssembly().GetManifestResourceStream("LEFontPatch.classdata.tpk"))check.LoadClassPackage(tpk!);
   var checkBundle=check.LoadBundleFile(temporary);var output=check.LoadAssetsFileFromBundle(checkBundle,0);
   check.LoadClassDatabaseFromPackage(output.file.Metadata.UnityVersion);ValidateNativeChat(check,output);
   if(output.file.AssetInfos.Count!=originals.Count)throw new InvalidDataException("Perma refresh asset count mismatch.");
   foreach(var info in output.file.AssetInfos){var wanted=info.PathId==source.info.PathId?Convert.ToHexString(expected):originals[info.PathId];if(Hash(output,info)!=wanted)throw new InvalidDataException("Perma refreshed source verification failed.");}
   check.UnloadAll();File.Move(temporary,path,true);Console.WriteLine("PermaLoad dynamic font source updated and verified.");
  } finally { if(File.Exists(unpacked))File.Delete(unpacked);if(File.Exists(temporary))File.Delete(temporary); }
 }
 static string Hash(AssetsFileInstance file,AssetFileInfo info){
  file.file.Reader.Position=info.GetAbsoluteByteOffset(file.file);
  using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);var bytes=new byte[65536];long left=info.ByteSize;
  while(left>0){var count=file.file.Reader.BaseStream.Read(bytes,0,(int)Math.Min(left,bytes.Length));if(count==0)throw new EndOfStreamException();hash.AppendData(bytes,0,count);left-=count;}
  return Convert.ToHexString(hash.GetHashAndReset());
 }
}
