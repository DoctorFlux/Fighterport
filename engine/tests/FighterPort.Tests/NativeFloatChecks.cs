using FighterPort.Core;
using FighterPort.Adapters.Brawl;

internal static class NativeFloatChecks
{
    public static async Task RunAsync(BrawlAdapter adapter,FighterProject native,string output,Action<bool,string> check)
    {
        float value=BitConverter.Int32BitsToSingle(unchecked((int)0xC27E9FFF)),tangent=BitConverter.Int32BitsToSingle(0x3A7E9FFF);string bone=native.Scene.Bones[0].Name;
        foreach(bool constant in new[]{false,true})
        {
            var channels=new Curve?[9];channels[3]=new(constant?[new(0,value)]:[new(0,value,tangent,tangent),new(23,value+.25f,tangent,tangent)]);
            var clip=new AnimationClip(Guid.NewGuid(),"Original finite endian probe",24,new(60),false,[new(bone,channels)]){NativeTarget=native.Clips[0].Source,NativeWrite=NativeWriteMode.NewResource,NativeWriteName="OriginalEndianProbe"};
            string path=Path.Combine(output,"finite-endian-"+(constant?"fixed":"I12")+"-"+Guid.NewGuid().ToString("N")+".chr0");await adapter.ExportAsync(native with{Clips=[clip]},clip,path);
            var reopened=await adapter.ImportAsync(native.Clips[0].Source!.Model!.Path,path);var curve=reopened.Clips[0].Tracks.Single(t=>t.Bone==bone).Channels[3]!;
            check(BitConverter.SingleToInt32Bits(curve.Keys[0].Value)==BitConverter.SingleToInt32Bits(value),$"Finite { (constant?"fixed":"I12") } CHR0 scalar retains exact bits when its byte reversal represents a signaling NaN");
            if(!constant)check(BitConverter.SingleToInt32Bits(curve.Keys[0].OutTangent)==BitConverter.SingleToInt32Bits(tangent)&&Math.Abs(curve.Sample(7.375)-channels[3]!.Sample(7.375))<.000001f,"Finite native tangent bytes and withheld fractional interpolation survive fresh decoder reopening");
            var bytes=await File.ReadAllBytesAsync(path);check(bytes.AsSpan().IndexOf(new byte[]{0xC2,0x7E,0x9F,0xFF})>=0,"Genuine CHR0 contains the independent expected raw big-endian value bytes");
        }
    }
}
