using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Web.Script.Serialization;

namespace BrawlCrate.Studio
{
    // Per-library presentation labels. Never changes the catalogue, native
    // motion identity, a saved project, or retarget binding.
    internal sealed class Bt3ActionAliases
    {
        private const int MaximumBytes=32*1024*1024;
        private const int MaximumLabels=262144;
        private readonly object gate=new object();
        private readonly string path;
        private readonly Dictionary<string,string> labels=new Dictionary<string,string>(StringComparer.Ordinal);
        internal string LastError{get;private set;}
        public sealed class Row{public string Key{get;set;}public string Label{get;set;}}
        public sealed class Data{public int Schema{get;set;}public Row[] Labels{get;set;}}
        internal Bt3ActionAliases(string libraryDirectory)
        {
            path=Path.Combine(libraryDirectory,"bt3-action-labels.json");
            try
            {
                AnimationLibrary.NoLinks(path);
                if(!File.Exists(path))return;
                using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
                {
                    if(file.Length>MaximumBytes)throw new InvalidDataException("BT3 labels exceed the read bound.");
                    using(var reader=new StreamReader(file,Encoding.UTF8))
                    {
                        var data=new JavaScriptSerializer{MaxJsonLength=MaximumBytes}.Deserialize<Data>(reader.ReadToEnd());
                        if(data?.Schema!=1||data.Labels==null||data.Labels.Length>MaximumLabels)
                            throw new InvalidDataException("Unsupported BT3 labels.");
                        foreach(var row in data.Labels)
                        {
                            if(row==null||!ValidKey(row.Key)||!ValidLabel(row.Label)||labels.ContainsKey(row.Key))
                                throw new InvalidDataException("Invalid BT3 label entry.");
                            labels.Add(row.Key,row.Label);
                        }
                    }
                }
            }
            catch(Exception error)when(StorageError(error)||error is ArgumentException||error is InvalidOperationException)
            {labels.Clear();LastError="BT3 labels could not be read; exact action IDs remain available.";}
        }
        private static bool StorageError(Exception error)=>error is IOException||error is UnauthorizedAccessException||
            error is SecurityException||error is NotSupportedException;
        private static string Key(string sourceId,int form,int action)=>sourceId+":"+form.ToString("D3")+":"+action.ToString("D4");
        private static bool ValidKey(string key)
        {
            if(key==null||key.Length!=41)return false;
            return Guid.TryParseExact(key.Substring(0,32),"N",out _)&&key[32]==':'&&
                int.TryParse(key.Substring(33,3),out int form)&&form>=0&&form<161&&key[36]==':'&&
                int.TryParse(key.Substring(37,4),out int action)&&action>=0&&action<2048;
        }
        private static bool ValidLabel(string label)=>!string.IsNullOrWhiteSpace(label)&&label.Length<=80&&
            label.Trim()==label&&!label.Any(char.IsControl);
        internal string Get(string sourceId,int form,int action)
        {
            if(!Guid.TryParseExact(sourceId,"N",out _)||form<0||form>=161||action<0||action>=2048)return null;
            lock(gate)return labels.TryGetValue(Key(sourceId,form,action),out var value)?value:null;
        }
        internal void Set(string sourceId,int form,int action,string label)
        {
            string key=Key(sourceId,form,action);
            if(!ValidKey(key)||label!=null&&!ValidLabel(label))
                throw new ArgumentException("Choose an exact BT3 form/action and a label of at most 80 printable characters.");
            lock(gate)
            {
                if(label!=null&&!labels.ContainsKey(key)&&labels.Count>=MaximumLabels)
                    throw new InvalidOperationException("The BT3 label preference limit has been reached.");
                if(label==null)labels.Remove(key);else labels[key]=label;
                string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
                try
                {
                    AnimationLibrary.NoLinks(path);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    var data=new Data{Schema=1,Labels=labels.OrderBy(x=>x.Key,StringComparer.Ordinal)
                        .Select(x=>new Row{Key=x.Key,Label=x.Value}).ToArray()};
                    byte[] bytes=Encoding.UTF8.GetBytes(new JavaScriptSerializer{MaxJsonLength=MaximumBytes}.Serialize(data));
                    if(bytes.Length>MaximumBytes)throw new InvalidDataException("BT3 labels exceed the write bound.");
                    using(var file=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                    {file.Write(bytes,0,bytes.Length);file.Flush(true);}
                    if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
                    LastError=null;
                }
                catch(Exception error)when(StorageError(error)||error is ArgumentException)
                {LastError="BT3 label is available in this session but could not be saved to disk.";}
                finally
                {
                    try{if(File.Exists(temporary))File.Delete(temporary);}
                    catch(Exception error)when(StorageError(error)){}
                }
            }
        }
    }
}
