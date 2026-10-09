using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.Text.Json.Nodes;
using System.Reflection;

namespace LEFontPatch;
public static class BundleFontPatch {
    public const string BundleName = "assets_a3ec63478769f648.bundle";
    public static void Run(string gameDataPath, string fontFolder) {
        var path = Path.Combine(gameDataPath, "StreamingAssets", "LEAssetBundles", BundleName);
        if (!File.Exists(path)) { Console.WriteLine("Additional UI font bundle not present; skipped."); return; }
        var manager = new AssetsManager();
        using(var tpk=Assembly.GetExecutingAssembly().GetManifestResourceStream("LEFontPatch.classdata.tpk")) manager.LoadClassPackage(tpk!);
        try {
            var bundle = manager.LoadBundleFile(new MemoryStream(File.ReadAllBytes(path)), path);
            var assets = manager.LoadAssetsFileFromBundle(bundle,0);
            manager.LoadClassDatabaseFromPackage(assets.file.Metadata.UnityVersion);
            var replacements = new Dictionary<long,byte[]>();
            foreach(var info in assets.file.AssetInfos.Where(i=>i.TypeId==(int)AssetClassID.MonoBehaviour)) {
                var field=manager.GetBaseField(assets,info);
                var name=field["m_Name"].AsString;
                if(name is not ("NotoSansSC-Regular" or "NotoSansTC-Regular")) continue;
                var font = JsonNode.Parse(File.ReadAllText(Path.Combine(fontFolder,"fonts","jf-openhuninn-2.1 SDFAA (Dynamic).json")))!.AsObject();
                var source=manager.GetExtAsset(assets,field["m_SourceFontFile"]);
                var material=manager.GetExtAsset(assets,field["m_Material"]);
                var atlas=manager.GetExtAsset(assets,field["m_AtlasTextures"]["Array"][0]);
                if(source.file!=assets || material.file!=assets || atlas.file!=assets) throw new InvalidDataException("UI font dependencies must be local.");
                byte[] SourceAsset() {
                    var raw=File.ReadAllBytes(Path.Combine(fontFolder,"fonts","jf-openhuninn-2.1.dat"));
                    var f=source.baseField.TemplateField.MakeValue(new AssetsFileReader(new MemoryStream(raw)),0);
                    if(!raw.AsSpan().SequenceEqual(f.WriteToByteArray())) throw new InvalidDataException("Source font layout mismatch.");
                    foreach(var key in new[]{"m_DefaultMaterial","m_Texture"}) {f[key]["m_FileID"].AsInt=0;f[key]["m_PathID"].AsLong=0;}
                    return f.WriteToByteArray();
                }
                var atlasRaw=File.ReadAllBytes(Path.Combine(fontFolder,"fonts","jf-openhuninn-2.1 Atlas (Dynamic).dat"));
                var atlasField=atlas.baseField.TemplateField.MakeValue(new AssetsFileReader(new MemoryStream(atlasRaw)),0);
                if(!atlasRaw.AsSpan().SequenceEqual(atlasField.WriteToByteArray()))throw new InvalidDataException("Atlas layout mismatch.");
                var materialRaw=File.ReadAllBytes(Path.Combine(fontFolder,"fonts","jf-openhuninn-2.1 Material (Dynamic).dat"));
                var mat=material.baseField.TemplateField.MakeValue(new AssetsFileReader(new MemoryStream(materialRaw)),0);
                if(!materialRaw.AsSpan().SequenceEqual(mat.WriteToByteArray()))throw new InvalidDataException("Material layout mismatch.");
                mat.Children[mat.Children.FindIndex(f=>f.FieldName=="m_Shader")]=material.baseField["m_Shader"].Clone();
                var tex=mat["m_SavedProperties"]["m_TexEnvs"]["Array"].Children.Single(f=>f[0].AsString=="_MainTex")[1]["m_Texture"];
                tex["m_FileID"].AsInt=0;tex["m_PathID"].AsLong=atlas.info.PathId;
                JsonObject Ptr(long id)=>new(){["m_FileID"]=0,["m_PathID"]=id};
                font["m_Name"]=name;
                font["m_Script"]=new JsonObject{["m_FileID"]=field["m_Script"]["m_FileID"].AsInt,["m_PathID"]=field["m_Script"]["m_PathID"].AsLong};
                font["m_SourceFontFile"]=Ptr(source.info.PathId);
                font["m_Material"]=Ptr(material.info.PathId);
                font["m_AtlasTextures"]!["Array"]=new JsonArray(Ptr(atlas.info.PathId));
                var fontRaw=LEFontManager.JsonToBytes(font,field.TemplateField);
                replacements[source.info.PathId]=SourceAsset();
                replacements[atlas.info.PathId]=atlasRaw;
                replacements[material.info.PathId]=mat.WriteToByteArray();
                replacements[info.PathId]=fontRaw;
                Console.WriteLine("Replaced additional UI font: "+name);
            }
            if(replacements.Count!=8)throw new InvalidDataException("Expected two complete UI font sets.");
            var originals=assets.file.AssetInfos.ToDictionary(i=>i.PathId,i=>Read(assets,i));
            foreach(var (id,data) in replacements)assets.file.GetAssetInfo(id).SetNewData(data);
            bundle.file.BlockAndDirInfo.DirectoryInfos[0].SetNewData(assets.file);
            var uncompressed=new MemoryStream();bundle.file.Write(new AssetsFileWriter(uncompressed));bundle.file.Close();bundle.file.Read(new AssetsFileReader(uncompressed));
            var temporary=path+".new";
            using(var writer=new AssetsFileWriter(temporary))bundle.file.Pack(writer,AssetBundleCompressionType.LZ4);
            var verifyManager=new AssetsManager();
            var verifyBundle=verifyManager.LoadBundleFile(temporary);
            var verified=verifyManager.LoadAssetsFileFromBundle(verifyBundle,0);
            if(verified.file.AssetInfos.Count!=originals.Count)throw new InvalidDataException("UI bundle asset count changed.");
            foreach(var info in verified.file.AssetInfos){var expected=replacements.TryGetValue(info.PathId,out var value)?value:originals[info.PathId];if(!Read(verified,info).AsSpan().SequenceEqual(expected))throw new InvalidDataException("UI bundle payload verification failed.");}
            verifyManager.UnloadAll();manager.UnloadAll();
            File.Move(temporary,path,true);
            Console.WriteLine("Additional UI bundle verified: 8 intended assets changed; all other payloads unchanged.");
        } finally { manager.UnloadAll(); }
    }
    static byte[] Read(AssetsFileInstance file,AssetFileInfo info){file.file.Reader.Position=info.GetAbsoluteByteOffset(file.file);return file.file.Reader.ReadBytes((int)info.ByteSize);}
}
