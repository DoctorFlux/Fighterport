using System.Text.Json;
using System.Text.Json.Serialization;
using FighterPort.Core;

namespace FighterPort.Adapters.Brawl;

// Adapter-scoped wire decoding. The legacy JSON serializer turns -0 into +0;
// integer payloads preserve exact scalar bits without changing Core/project JSON.
internal sealed class NativeKeyJsonConverter:JsonConverter<Key>
{
    public override Key Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options)
    {
        if(reader.TokenType!=JsonTokenType.StartObject)throw new JsonException("Native key must be an object.");
        double frame=0;float value=0,input=0,output=0;int? valueBits=null,inputBits=null,outputBits=null;
        while(reader.Read()&&reader.TokenType!=JsonTokenType.EndObject)
        {
            string property=reader.GetString()!;reader.Read();
            switch(property)
            {
                case "Frame":frame=reader.GetDouble();break;
                case "Value":value=reader.GetSingle();break;
                case "InTangent":input=reader.GetSingle();break;
                case "OutTangent":output=reader.GetSingle();break;
                case "ValueBits":valueBits=reader.GetInt32();break;
                case "InTangentBits":inputBits=reader.GetInt32();break;
                case "OutTangentBits":outputBits=reader.GetInt32();break;
                default:reader.Skip();break;
            }
        }
        return new(frame,valueBits is {} v?BitConverter.Int32BitsToSingle(v):value,inputBits is {} i?BitConverter.Int32BitsToSingle(i):input,outputBits is {} o?BitConverter.Int32BitsToSingle(o):output);
    }
    public override void Write(Utf8JsonWriter writer,Key value,JsonSerializerOptions options)=>throw new NotSupportedException("Native key wire converter is read-only; project storage uses its ordinary format.");
}
