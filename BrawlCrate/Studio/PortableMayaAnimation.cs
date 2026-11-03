using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace BrawlCrate.Studio
{
    // Maya's documented animVersion 1.1 curve file. This writes portable
    // curves from a frozen target-action projection; it never creates a CHR0
    // document or changes the game's native motion member.
    internal static class PortableMayaAnimation
    {
        private const double RadToDeg=180.0/Math.PI;
        private const double DegToRad=Math.PI/180.0;
        private const double MaxSampledRotationDegrees=0.01;
        private const int MaxSamplesPerTrack=65536;
        private struct Sample {internal double Time;internal Vector3 Euler,Position;}
        private static string F(double value)
        {
            if(double.IsNaN(value)||double.IsInfinity(value))throw new InvalidDataException("Nonfinite portable animation value.");
            return value.ToString("R",CultureInfo.InvariantCulture);
        }
        private static string Hash(string value)
        {
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value)))
                .Replace("-","").ToLowerInvariant();
        }
        private static string HashFile(string path)
        {
            using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        }
        private static string Filename(PortableRiggedGltf.Clip clip)
        {
            string name=new string((clip.Name??"Animation").Select(c=>char.IsLetterOrDigit(c)||c=='_'||c=='-'?c:'-').ToArray()).Trim('-');
            if(name.Length==0)name="Animation";
            if(name.Length>48)name=name.Substring(0,48);
            return name+"-"+Hash(clip.Identity).Substring(0,12)+".anim";
        }
        private static Vector3 Euler(Quaternion value)
        {
            if(value.LengthSquared()<1e-12f||!Finite(value))throw new InvalidDataException("Invalid portable quaternion.");
            value=Quaternion.Normalize(value);
            double x=value.X,y=value.Y,z=value.Z,w=value.W;
            // Inverse of qx * qy * qz, the XYZ order used for these joints.
            return new Vector3((float)(Math.Atan2(2*(w*x-y*z),1-2*(x*x+y*y))*RadToDeg),
                (float)(Math.Asin(Math.Max(-1,Math.Min(1,2*(w*y+x*z))))*RadToDeg),
                (float)(Math.Atan2(2*(w*z-x*y),1-2*(y*y+z*z))*RadToDeg));
        }
        private static Quaternion Rotation(Vector3 e)
            =>Quaternion.Normalize(Quaternion.CreateFromAxisAngle(Vector3.UnitX,(float)((e.X%360)*DegToRad))*
                Quaternion.CreateFromAxisAngle(Vector3.UnitY,(float)((e.Y%360)*DegToRad))*
                Quaternion.CreateFromAxisAngle(Vector3.UnitZ,(float)((e.Z%360)*DegToRad)));
        private static bool Finite(Quaternion q)=>!float.IsNaN(q.X)&&!float.IsNaN(q.Y)&&!float.IsNaN(q.Z)&&!float.IsNaN(q.W)&&
            !float.IsInfinity(q.X)&&!float.IsInfinity(q.Y)&&!float.IsInfinity(q.Z)&&!float.IsInfinity(q.W);
        private static bool Finite(Vector3 v)=>!float.IsNaN(v.X)&&!float.IsNaN(v.Y)&&!float.IsNaN(v.Z)&&
            !float.IsInfinity(v.X)&&!float.IsInfinity(v.Y)&&!float.IsInfinity(v.Z);
        private static float Near(float value,float reference)
            =>value+360f*(float)Math.Round((reference-value)/360f,MidpointRounding.AwayFromZero);
        private static Vector3 Near(Vector3 e,Vector3 reference)
            =>new Vector3(Near(e.X,reference.X),Near(e.Y,reference.Y),Near(e.Z,reference.Z));
        private static Vector3 Continuous(Quaternion q,Vector3 reference)
        {
            Vector3 a=Near(Euler(q),reference);
            Vector3 b=Near(new Vector3(a.X+180,180-a.Y,a.Z+180),reference);
            Vector3 chosen=Vector3.DistanceSquared(a,reference)<=Vector3.DistanceSquared(b,reference)?a:b;
            double error=Angle(Rotation(chosen),q);
            if(error>0.002)
                throw new InvalidDataException("The native quaternion could not be represented in XYZ Euler order: "+
                    F(error)+" degrees, q="+q+", Euler="+chosen+".");
            return chosen;
        }
        private static double Angle(Quaternion a,Quaternion b)
        {
            double dot=Math.Abs((double)a.X*b.X+(double)a.Y*b.Y+(double)a.Z*b.Z+(double)a.W*b.W);
            dot/=Math.Sqrt(((double)a.X*a.X+(double)a.Y*a.Y+(double)a.Z*a.Z+(double)a.W*a.W)*
                ((double)b.X*b.X+(double)b.Y*b.Y+(double)b.Z*b.Z+(double)b.W*b.W));
            return 2*Math.Acos(Math.Min(1,Math.Max(0,dot)))*RadToDeg;
        }
        private static void Refine(double a,double b,Quaternion qa,Quaternion qb,Vector3 ea,Vector3 eb,
            Vector3 pa,Vector3 pb,List<Sample> result,int depth,Action check)
        {
            check();
            if(result.Count>=MaxSamplesPerTrack||depth>14)
                throw new InvalidDataException("Maya animation curve exceeded its bounded fitting work; use GLB for this action.");
            double worst=0;
            foreach(double part in new[]{0.125,0.25,0.375,0.5,0.625,0.75,0.875})
            {
                Quaternion actual=Quaternion.Slerp(qa,qb,(float)part);
                Quaternion linear=Rotation(Vector3.Lerp(ea,eb,(float)part));
                worst=Math.Max(worst,Angle(actual,linear));
            }
            if(worst<=MaxSampledRotationDegrees)
            {result.Add(new Sample{Time=b,Euler=eb,Position=pb});return;}
            double mid=(a+b)*0.5;
            Quaternion qm=Quaternion.Slerp(qa,qb,0.5f);
            Vector3 em=Continuous(qm,Vector3.Lerp(ea,eb,0.5f));
            Vector3 pm=Vector3.Lerp(pa,pb,0.5f);
            Refine(a,mid,qa,qm,ea,em,pa,pm,result,depth+1,check);
            Refine(mid,b,qm,qb,em,eb,pm,pb,result,depth+1,check);
        }
        private static List<Sample> Fit(PortableRiggedGltf.Track track,Action check)
        {
            if(track.Frames.Length==0||track.Rotation.Length!=track.Frames.Length||
                track.Translation!=null&&track.Translation.Length!=track.Frames.Length)
                throw new InvalidDataException("Portable animation track arrays have different lengths.");
            if(track.Frames.Length>MaxSamplesPerTrack)throw new InvalidDataException("Portable animation track is too large.");
            if(float.IsNaN(track.Frames[0])||float.IsInfinity(track.Frames[0])||track.Frames[0]<0||
                track.Translation!=null&&!Finite(track.Translation[0]))
                throw new InvalidDataException("Portable first key has invalid timing or translation.");
            var first=Continuous(track.Rotation[0],Vector3.Zero);
            var samples=new List<Sample>{new Sample{Time=track.Frames[0],Euler=first,
                Position=track.Translation?[0]??Vector3.Zero}};
            for(int i=1;i<track.Frames.Length;i++)
            {
                if(!Finite(track.Rotation[i])||track.Frames[i]<=track.Frames[i-1]||
                    float.IsNaN(track.Frames[i])||float.IsInfinity(track.Frames[i]))
                    throw new InvalidDataException("Portable animation key timing is invalid.");
                Vector3 prior=samples[samples.Count-1].Euler;
                Vector3 next=Continuous(track.Rotation[i],prior);
                Vector3 p0=track.Translation?[i-1]??Vector3.Zero,p1=track.Translation?[i]??Vector3.Zero;
                if(!Finite(p0)||!Finite(p1))throw new InvalidDataException("Portable translation is nonfinite.");
                Refine(track.Frames[i-1],track.Frames[i],track.Rotation[i-1],track.Rotation[i],
                    prior,next,p0,p1,samples,0,check);
            }
            return samples;
        }
        private static void Curve(StreamWriter writer,string bone,string attribute,string group,int index,
            IReadOnlyList<Sample> samples,Func<Sample,double> value)
        {
            writer.WriteLine("anim "+group+"."+attribute+" "+attribute+" "+bone+" 0 0 "+index+";");
            writer.WriteLine("animData {");
            writer.WriteLine("  input time;");
            writer.WriteLine("  output "+(group=="rotate"?"angular":"linear")+";");
            writer.WriteLine("  weighted 0;");
            writer.WriteLine("  preInfinity constant;");
            writer.WriteLine("  postInfinity constant;");
            writer.WriteLine("  keys {");
            foreach(var sample in samples)
                writer.WriteLine("    "+F(sample.Time)+" "+F(value(sample))+" linear linear 1 1 0;");
            writer.WriteLine("  }");writer.WriteLine("}");
        }
        private static double Evaluate(List<KeyValuePair<double,double>> keys,double time)
        {
            if(time<=keys[0].Key)return keys[0].Value;
            if(time>=keys[keys.Count-1].Key)return keys[keys.Count-1].Value;
            int lo=0,hi=keys.Count-1;
            while(hi-lo>1){int mid=(lo+hi)/2;if(keys[mid].Key<=time)lo=mid;else hi=mid;}
            double part=(time-keys[lo].Key)/(keys[hi].Key-keys[lo].Key);
            return keys[lo].Value+(keys[hi].Value-keys[lo].Value)*part;
        }
        private static double Verify(string path,PortableRiggedGltf.Clip clip,Action check,out int checkedSamples)
        {
            checkedSamples=0;
            if(new FileInfo(path).Length>128*1024*1024)throw new InvalidDataException("Maya animation exceeds its bounded verification size.");
            var curves=new Dictionary<string,List<KeyValuePair<double,double>>>(StringComparer.Ordinal);
            var lines=File.ReadAllLines(path);string current=null;bool inKeys=false;
            foreach(string raw in lines)
            {
                string line=raw.Trim();
                if(line.StartsWith("anim rotate.",StringComparison.Ordinal)||line.StartsWith("anim translate.",StringComparison.Ordinal))
                {
                    string[] parts=line.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
                    if(parts.Length!=7)throw new InvalidDataException("Fresh Maya curve header is invalid.");
                    current=parts[3]+"/"+parts[2];
                    if(curves.ContainsKey(current))throw new InvalidDataException("Fresh Maya animation contains duplicate curves.");
                    curves.Add(current,new List<KeyValuePair<double,double>>());inKeys=false;
                }
                else if(line=="keys {"){if(current==null)throw new InvalidDataException("Fresh Maya key block has no curve.");inKeys=true;}
                else if(inKeys&&line=="}"){inKeys=false;current=null;}
                else if(inKeys)
                {
                    string[] parts=line.TrimEnd(';').Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
                    if(parts.Length!=7||parts[2]!="linear"||parts[3]!="linear")
                        throw new InvalidDataException("Fresh Maya key row is invalid.");
                    double time=double.Parse(parts[0],CultureInfo.InvariantCulture),
                        value=double.Parse(parts[1],CultureInfo.InvariantCulture);
                    var keys=curves[current];
                    if(double.IsNaN(time)||double.IsInfinity(time)||double.IsNaN(value)||double.IsInfinity(value)||
                        keys.Count>0&&time<=keys[keys.Count-1].Key)
                        throw new InvalidDataException("Fresh Maya curve has nonfinite or unordered keys.");
                    keys.Add(new KeyValuePair<double,double>(time,value));
                }
            }
            if(curves.Count!=clip.Tracks.Sum(track=>track.Translation==null?3:6))
                throw new InvalidDataException("Fresh Maya animation has an unexpected curve count.");
            double maximum=0;
            foreach(var track in clip.Tracks)
            {
                check();
                string prefix=track.Bone+"/";
                string[] channels={"rotateX","rotateY","rotateZ"};
                if(channels.Any(channel=>!curves.TryGetValue(prefix+channel,out var found)||found.Count==0))
                    throw new InvalidDataException("Fresh Maya animation is missing a rotation curve.");
                var keyTimes=curves[prefix+"rotateX"].Select(pair=>pair.Key).ToArray();
                if(channels.Skip(1).Any(channel=>!curves[prefix+channel].Select(pair=>pair.Key).SequenceEqual(keyTimes)))
                    throw new InvalidDataException("Fresh Maya rotation channel times disagree.");
                if(track.Translation!=null&&new[]{"translateX","translateY","translateZ"}.Any(channel=>
                    !curves.TryGetValue(prefix+channel,out var found)||!found.Select(pair=>pair.Key).SequenceEqual(keyTimes)))
                    throw new InvalidDataException("Fresh Maya translation channel times disagree.");
                var witnesses=new SortedSet<double>(track.Frames.Select(frame=>(double)frame));
                foreach(double time in keyTimes)witnesses.Add(time);
                for(int i=1;i<keyTimes.Length;i++)
                    for(int fraction=1;fraction<16;fraction++)
                        witnesses.Add(keyTimes[i-1]+(keyTimes[i]-keyTimes[i-1])*(fraction/16.0));
                foreach(double time in witnesses)
                {
                    check();
                    checkedSamples++;
                    int hi=Array.BinarySearch(track.Frames,(float)time);
                    if(hi<0)hi=~hi;
                    int lo=Math.Max(0,hi-1);hi=Math.Min(hi,track.Frames.Length-1);
                    float part=hi==lo?0:(float)((time-track.Frames[lo])/(track.Frames[hi]-track.Frames[lo]));
                    Quaternion reference=Quaternion.Slerp(track.Rotation[lo],track.Rotation[hi],part);
                    var exported=new Vector3((float)Evaluate(curves[prefix+"rotateX"],time),
                        (float)Evaluate(curves[prefix+"rotateY"],time),(float)Evaluate(curves[prefix+"rotateZ"],time));
                    maximum=Math.Max(maximum,Angle(Rotation(exported),reference));
                    if(maximum>MaxSampledRotationDegrees+0.000001)
                        throw new InvalidDataException("Fresh Maya animation exceeds the sampled local rotation bound: "+F(maximum)+" degrees.");
                    if(track.Translation!=null)
                    {
                        Vector3 expected=Vector3.Lerp(track.Translation[lo],track.Translation[hi],part);
                        var written=new Vector3((float)Evaluate(curves[prefix+"translateX"],time),
                            (float)Evaluate(curves[prefix+"translateY"],time),
                            (float)Evaluate(curves[prefix+"translateZ"],time));
                        if(Vector3.Distance(expected,written)>0.00001f)
                            throw new InvalidDataException("Fresh Maya animation exceeds the sampled translation bound.");
                    }
                }
            }
            return maximum;
        }
        internal static int Write(string path,PortableRiggedGltf.Clip clip,Action check,
            out double maximumDegrees,out int checkedSamples)
        {
            if(clip==null||clip.Tracks==null||clip.Tracks.Length==0||
                clip.Tracks.Select(t=>t.Bone).Distinct(StringComparer.Ordinal).Count()!=clip.Tracks.Length)
                throw new InvalidDataException("Choose an exact portable clip with distinct native joint tracks.");
            if(Math.Abs(clip.FramesPerSecond-60)>0.0001f)
                throw new InvalidDataException("This Maya .anim writer currently requires a verified 60-frame timebase.");
            var fitted=clip.Tracks.Select(track=>{check();return new{Track=track,Samples=Fit(track,check)};}).ToArray();
            double last=fitted.Max(x=>x.Samples[x.Samples.Count-1].Time);
            using(var writer=new StreamWriter(path,false,new UTF8Encoding(false)))
            {
                writer.WriteLine("animVersion 1.1;");writer.WriteLine("mayaVersion 2015;");
                writer.WriteLine("timeUnit ntscf;");writer.WriteLine("linearUnit cm;");
                writer.WriteLine("angularUnit deg;");writer.WriteLine("startTime 0;");
                writer.WriteLine("endTime "+F(last)+";");
                foreach(var item in fitted)
                {
                    if(item.Track.Bone.Length==0||item.Track.Bone.Any(c=>!char.IsLetterOrDigit(c)&&c!='_'))
                        throw new InvalidDataException("Portable joint name cannot be represented in Maya .anim.");
                    var values=item.Samples;
                    Curve(writer,item.Track.Bone,"rotateX","rotate",0,values,s=>s.Euler.X);
                    Curve(writer,item.Track.Bone,"rotateY","rotate",1,values,s=>s.Euler.Y);
                    Curve(writer,item.Track.Bone,"rotateZ","rotate",2,values,s=>s.Euler.Z);
                    if(item.Track.Translation!=null)
                    {
                        Curve(writer,item.Track.Bone,"translateX","translate",3,values,s=>s.Position.X);
                        Curve(writer,item.Track.Bone,"translateY","translate",4,values,s=>s.Position.Y);
                        Curve(writer,item.Track.Bone,"translateZ","translate",5,values,s=>s.Position.Z);
                    }
                }
            }
            check();
            maximumDegrees=Verify(path,clip,check,out checkedSamples);
            if(maximumDegrees>MaxSampledRotationDegrees+0.000001)
                throw new InvalidDataException("Fresh Maya .anim motion verification failed.");
            return fitted.Sum(item=>item.Samples.Count);
        }
        internal static string[] Publish(string destination,IReadOnlyList<PortableRiggedGltf.Clip> clips,
            Action validate,CancellationToken cancellation)
        {
            if(clips==null||clips.Count==0||clips.Any(c=>c==null||string.IsNullOrWhiteSpace(c.Identity))||
                clips.Select(c=>c.Identity).Distinct(StringComparer.Ordinal).Count()!=clips.Count)
                throw new InvalidDataException("Choose distinct exact portable animations.");
            string[] names=clips.Select(Filename).ToArray();
            if(names.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=names.Length)
                throw new InvalidDataException("Maya animation filenames collide.");
            destination=Path.GetFullPath(destination);
            if(File.Exists(destination)||Directory.Exists(destination))throw new IOException("The animation destination exists.");
            string parent=Path.GetDirectoryName(destination);
            if(!Directory.Exists(parent))throw new DirectoryNotFoundException("The animation export parent is unavailable.");
            string stage=Path.Combine(parent,".fighterport-maya-"+Guid.NewGuid().ToString("N"));
            string parentPrefix=Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            string stageFull=Path.GetFullPath(stage);
            if(!stageFull.StartsWith(parentPrefix,StringComparison.OrdinalIgnoreCase))
                throw new IOException("Invalid Maya animation staging directory.");
            Directory.CreateDirectory(stage);
            try
            {
                var receipt=new List<string>{"Fighterport portable Maya animation export",
                    "Format: Autodesk .anim 1.1; XYZ local Euler; 60 fps; linear curves",
                    "Validation: sampled local orientation <= "+F(MaxSampledRotationDegrees)+" degrees against frozen action",
                    "Model/texture files: separate; gameplay events and loop policy are not represented"};
                for(int i=0;i<names.Length;i++)
                {
                    cancellation.ThrowIfCancellationRequested();validate?.Invoke();
                    int samples=Write(Path.Combine(stage,names[i]),clips[i],
                        ()=>cancellation.ThrowIfCancellationRequested(),out double maximum,out int witnesses);
                    receipt.Add(names[i]+" | identity SHA-256 "+Hash(clips[i].Identity)+
                        " | fitted sample total "+samples+" | checked local samples "+witnesses+
                        " | maximum sampled orientation "+F(maximum)+" degrees | SHA-256 "+HashFile(Path.Combine(stage,names[i])));
                }
                File.WriteAllLines(Path.Combine(stage,"Export-info.txt"),receipt);
                cancellation.ThrowIfCancellationRequested();validate?.Invoke();
                if(File.Exists(destination)||Directory.Exists(destination))throw new IOException("The animation destination appeared during export.");
                Directory.Move(stage,destination);
                return names;
            }
            finally
            {
                if(Directory.Exists(stageFull))
                {
                    if(!stageFull.StartsWith(parentPrefix,StringComparison.OrdinalIgnoreCase)||
                        (File.GetAttributes(stageFull)&FileAttributes.ReparsePoint)!=0)
                        throw new IOException("Maya animation staging directory changed ownership.");
                    Directory.Delete(stageFull,true);
                }
            }
        }
    }
}
