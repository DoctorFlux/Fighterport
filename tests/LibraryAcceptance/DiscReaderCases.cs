using BrawlCrate.Studio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

internal static partial class Program
{
    // Independent procedural encryption/encoding, never a game-data fixture.
    const int SyntheticPartition=0x50000,SyntheticData=0x70000,PayloadSize=0x7c00;
    static readonly byte[] ExpectedPayload=Enumerable.Range(0,70000).Select(i=>(byte)((i*7+19)%251)).ToArray();
    static void BE(byte[] bytes,int at,uint value)
    {bytes[at]=(byte)(value>>24);bytes[at+1]=(byte)(value>>16);bytes[at+2]=(byte)(value>>8);bytes[at+3]=(byte)value;}
    static byte[] EncryptFixture(byte[] key,byte[] iv,byte[] data)
    {using(var aes=Aes.Create()){aes.Key=key;aes.IV=iv;aes.Mode=CipherMode.CBC;aes.Padding=PaddingMode.None;using(var enc=aes.CreateEncryptor())return enc.TransformFinalBlock(data,0,data.Length);}}
    static byte[] DiscFixture(bool hashed,bool encrypted)
    {
        int dataSize=4*PayloadSize;var plain=new byte[dataSize];
        Encoding.ASCII.GetBytes("RSBE01").CopyTo(plain,0);plain[7]=3;BE(plain,0x18,0x5d1c9ea3);
        BE(plain,0x424,0x800/4);BE(plain,0x428,100/4);
        int fst=0x800;BE(plain,fst,0x01000000);BE(plain,fst+8,5);
        BE(plain,fst+12,0x01000000);BE(plain,fst+16,0);BE(plain,fst+20,5);
        BE(plain,fst+24,0x01000008);BE(plain,fst+28,1);BE(plain,fst+32,4);
        BE(plain,fst+36,14);BE(plain,fst+40,0x2000/4);BE(plain,fst+44,(uint)ExpectedPayload.Length);
        BE(plain,fst+48,26);BE(plain,fst+52,0x1f00/4);BE(plain,fst+56,16);
        Encoding.ASCII.GetBytes("fighter\0mario\0payload.bin\0shared.bin\0").CopyTo(plain,fst+60);
        ExpectedPayload.Take(16).ToArray().CopyTo(plain,0x1f00);
        ExpectedPayload.CopyTo(plain,0x2000);
        var image=new byte[SyntheticData+(hashed?4*0x8000:dataSize)];
        Buffer.BlockCopy(plain,0,image,0,0x440);image[0x60]=(byte)(hashed?0:1);image[0x61]=(byte)(encrypted?0:1);
        BE(image,0x40000,1);BE(image,0x40004,0x40020/4);BE(image,0x40020,SyntheticPartition/4);
        BE(image,SyntheticPartition,0x10001);BE(image,SyntheticPartition+0x2b4,0x8000/4);
        BE(image,SyntheticPartition+0x2b8,0x20000/4);BE(image,SyntheticPartition+0x2bc,(uint)(image.Length-SyntheticData)/4);
        if(!hashed){plain.CopyTo(image,SyntheticData);return image;}
        byte[] key=Enumerable.Range(1,16).Select(i=>(byte)(i*3)).ToArray();
        byte[] retail={0xeb,0xe4,0x2a,0x22,0x5e,0x85,0x93,0xe4,0x48,0xd9,0xc5,0x45,0x73,0x81,0xaa,0xf7};
        byte[] titleId={0,1,0,0,0x52,0x53,0x42,0x45};titleId.CopyTo(image,SyntheticPartition+0x1dc);
        var titleIv=new byte[16];titleId.CopyTo(titleIv,0);EncryptFixture(retail,titleIv,key).CopyTo(image,SyntheticPartition+0x1bf);
        var headers=Enumerable.Range(0,4).Select(_=>new byte[0x400]).ToArray();var h1=new byte[160];var h2=new byte[160];
        using(var sha=SHA1.Create())
        {
            for(int block=0;block<4;block++)
            {
                for(int part=0;part<31;part++)sha.ComputeHash(plain,block*PayloadSize+part*1024,1024).CopyTo(headers[block],part*20);
                sha.ComputeHash(headers[block],0,620).CopyTo(h1,block*20);
            }
            sha.ComputeHash(h1).CopyTo(h2,0);sha.ComputeHash(h2).CopyTo(image,SyntheticPartition+0x8000);
        }
        for(int block=0;block<4;block++)
        {
            h1.CopyTo(headers[block],0x280);h2.CopyTo(headers[block],0x340);
            var header=encrypted?EncryptFixture(key,new byte[16],headers[block]):headers[block];
            header.CopyTo(image,SyntheticData+block*0x8000);
            var chunk=new byte[PayloadSize];Buffer.BlockCopy(plain,block*PayloadSize,chunk,0,PayloadSize);
            if(encrypted){var iv=new byte[16];Buffer.BlockCopy(header,0x3d0,iv,0,16);chunk=EncryptFixture(key,iv,chunk);}
            chunk.CopyTo(image,SyntheticData+block*0x8000+0x400);
        }
        return image;
    }
    static byte[] WbfsFixture(byte[] iso)
    {
        const int size=0x80000;var wbfs=new byte[size*3];Encoding.ASCII.GetBytes("WBFS").CopyTo(wbfs,0);
        BE(wbfs,4,(uint)wbfs.Length/512);wbfs[8]=9;wbfs[9]=19;wbfs[12]=1;
        Buffer.BlockCopy(iso,0,wbfs,512,256);
        wbfs[768]=0;wbfs[769]=2;wbfs[770]=0;wbfs[771]=1;
        Buffer.BlockCopy(iso,0,wbfs,size*2,Math.Min(iso.Length,size));
        if(iso.Length>size)Buffer.BlockCopy(iso,size,wbfs,size,iso.Length-size);
        return wbfs;
    }
    static string SaveDiscFixture(string output,string name,byte[] bytes)
    {string path=Path.Combine(output,name);File.WriteAllBytes(path,bytes);return path;}
    static void RejectDiscFixture(string output,string name,byte[] bytes)
    {string path=SaveDiscFixture(output,name,bytes);Refuses(()=>{using(var reader=new BrawlDiscImage(path)){}},name+": malformed disc rejects without UI or an external program");}
    static int DiscReaderCases(string output)
    {
        output=Path.GetFullPath(output);if(Directory.Exists(output))throw new IOException("Choose new output.");Directory.CreateDirectory(output);
        foreach(var mode in new[]{new{Hashed=false,Encrypted=false},new{Hashed=true,Encrypted=false},new{Hashed=true,Encrypted=true}})
        foreach(bool wbfs in new[]{false,true})
        {
            string label=(wbfs?"wbfs":"iso")+"-"+mode.Hashed+"-"+mode.Encrypted;
            byte[] bytes=DiscFixture(mode.Hashed,mode.Encrypted);if(wbfs)bytes=WbfsFixture(bytes);
            string path=SaveDiscFixture(output,label+(wbfs?".wbfs":".iso"),bytes),before=Hash(path);
            using(var image=new BrawlDiscImage(path))
            {
                Check(image.GameId=="RSBE01"&&image.Revision==3&&image.Region=="NTSC-U"&&image.Files.SequenceEqual(new[]{"fighter/mario/payload.bin","fighter/shared.bin"}),label+": independent big-endian header, partition and bounded filesystem decode");
                Check(image.ReadPartition(0x2000,ExpectedPayload.Length).SequenceEqual(ExpectedPayload),label+": exact independent payload across native clusters and permuted WBFS sectors");
                Check(image.ReadPartition(0x2000+PayloadSize-7,37).SequenceEqual(ExpectedPayload.Skip(PayloadSize-7).Take(37)),label+": fractional read boundaries do not leak cluster headers");
                image.ReadPartition(0x2400,100);long verified=image.VerifiedClusters;
                Check(image.ReadPartition(0x2400,100).SequenceEqual(ExpectedPayload.Skip(0x400).Take(100))&&image.VerifiedClusters==verified,label+": repeated same-cluster reads retain valid decoded bytes without another verification");
                string root=Path.Combine(output,label+"-files");image.Extract("mario",root);
                Check(File.ReadAllBytes(Path.Combine(root,"fighter","mario","payload.bin")).SequenceEqual(ExpectedPayload),label+": extraction writes exactly the selected original payload");
                Check(!File.Exists(Path.Combine(root,"fighter","shared.bin")),label+": the scoped component API does not silently include shared data");
                string whole=Path.Combine(output,label+"-whole");image.ExtractAll(whole);
                Check(File.ReadAllBytes(Path.Combine(whole,"fighter","shared.bin")).SequenceEqual(ExpectedPayload.Take(16))&&File.ReadAllBytes(Path.Combine(whole,"fighter","mario","payload.bin")).SequenceEqual(ExpectedPayload),label+": default full extraction includes shared fighter resources and every character file");
                Refuses(()=>image.Extract("../mario",Path.Combine(output,"outside")),label+": traversal selection rejects");
                Refuses(()=>image.ReadPartition(-1,1),label+": negative read rejects");
                Refuses(()=>image.ReadPartition(long.MaxValue,1),label+": overflowing read rejects");
                Refuses(()=>image.Extract("mario",root),label+": existing output files cannot be overwritten");
            }
            Check(Hash(path)==before,label+": all reader operations preserve the exact original image");
        }
        byte[] plain=DiscFixture(false,false),cipher=DiscFixture(true,true),bad;
        bad=WbfsFixture(cipher);bad[12]=0;bad[13]=1;
        // ceil((256 + ceil(9399959552/524288)*2)/512)*512 = 36352.
        Buffer.BlockCopy(bad,512,bad,36864,36352);
        using(var image=new BrawlDiscImage(SaveDiscFixture(output,"single-later-slot.wbfs",bad)))
            Check(image.ReadPartition(0x2000,ExpectedPayload.Length).SequenceEqual(ExpectedPayload),"A single occupied later WBFS slot uses its aligned disc-info record, not its occupancy flag as a pointer");
        bad=(byte[])plain.Clone();bad[0]=(byte)'X';RejectDiscFixture(output,"wrong-game.iso",bad);
        bad=(byte[])plain.Clone();BE(bad,0x40000,257);RejectDiscFixture(output,"partition-count.iso",bad);
        bad=(byte[])plain.Clone();BE(bad,0x40000,2);BE(bad,0x40028,0x60000/4);RejectDiscFixture(output,"two-data.iso",bad);
        bad=(byte[])plain.Clone();BE(bad,SyntheticData+0x428,0x1000000);RejectDiscFixture(output,"huge-fst.iso",bad);
        bad=(byte[])plain.Clone();BE(bad,SyntheticData+0x800+28,0);RejectDiscFixture(output,"bad-parent.iso",bad);
        bad=(byte[])plain.Clone();BE(bad,SyntheticData+0x800+32,2);RejectDiscFixture(output,"bad-subtree.iso",bad);
        bad=(byte[])plain.Clone();bad[SyntheticData+0x800+60+8]=(byte)'.';bad[SyntheticData+0x800+60+9]=(byte)'.';bad[SyntheticData+0x800+60+10]=0;RejectDiscFixture(output,"unsafe-path.iso",bad);
        bad=(byte[])plain.Clone();BE(bad,SyntheticData+0x800+44,0x10000000);RejectDiscFixture(output,"file-range.iso",bad);
        RejectDiscFixture(output,"truncated.iso",plain.Take(plain.Length-4096).ToArray());
        bad=(byte[])cipher.Clone();bad[SyntheticData+0x400+0x1500]^=1;RejectDiscFixture(output,"damaged-payload.iso",bad);
        bad=(byte[])cipher.Clone();bad[SyntheticData+0x290]^=1;RejectDiscFixture(output,"damaged-hashes.iso",bad);
        bad=(byte[])cipher.Clone();bad[SyntheticPartition+0x8000]^=1;RejectDiscFixture(output,"damaged-h3.iso",bad);
        bad=(byte[])cipher.Clone();bad[SyntheticPartition+0x1f1]=1;RejectDiscFixture(output,"unsupported-key.iso",bad);
        bad=WbfsFixture(cipher);bad[9]=32;RejectDiscFixture(output,"bad-shift.wbfs",bad);
        bad=WbfsFixture(cipher);bad[771]=2;RejectDiscFixture(output,"duplicate-map.wbfs",bad);
        bad=WbfsFixture(cipher);bad[769]=255;RejectDiscFixture(output,"outside-map.wbfs",bad);
        bad=WbfsFixture(cipher);bad[13]=1;RejectDiscFixture(output,"two-disc.wbfs",bad);
        bad=WbfsFixture(cipher);bad[771]=0;
        using(var image=new BrawlDiscImage(SaveDiscFixture(output,"sparse-missing.wbfs",bad)))
            Refuses(()=>image.Extract("mario",Path.Combine(output,"missing-map-files")),"Unallocated required WBFS payload rejects instead of copying metadata or inventing zero data");
        using(var cancel=new CancellationTokenSource())
        {
            string path=SaveDiscFixture(output,"cancel.iso",cipher);using(var image=new BrawlDiscImage(path,cancel.Token))
            {cancel.Cancel();Refuses(()=>image.Extract("mario",Path.Combine(output,"cancelled-files")),"Cancelled extraction stops before creating an output");Check(!Directory.Exists(Path.Combine(output,"cancelled-files")),"Cancelled selected extraction leaves no partial output root");}
            Refuses(()=>{using(var image=new BrawlDiscImage(path,cancel.Token)){}},"Pre-cancelled reader performs no native opening");
        }
        using(var cancel=new CancellationTokenSource())
        {
            string path=SaveDiscFixture(output,"prepare-cancel.iso",cipher);var library=new AnimationLibrary(Path.Combine(output,"cancelled-library"));
            var request=new LibraryGameRequest{Path=path,ApproveBuild=true,GameVersion="Procedural encrypted fixture only",Characters=new[]{"mario"}};
            Refuses(()=>BrawlGameSource.Prepare(request,Path.Combine(library.DirectoryPath,"cache"),cancel.Token,(p,m)=>{if(p==5)cancel.Cancel();}),"Cancellation at selected extraction publishes no partial library generation");
            Check(!File.Exists(library.IndexPath),"Cancelled preparation preserves the unregistered catalogue");
        }
        File.WriteAllText(Path.Combine(output,"reader-results.json"),new JavaScriptSerializer{MaxJsonLength=16000000}.Serialize(new{Passed=true,Checks,ProceduralOnly=true,ExternalProcessUsed=false}));
        Console.WriteLine("PASS "+Checks.Count+" independent integrated-reader checks");return 0;
    }
}
