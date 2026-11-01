using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace FighterPort.Adapters.Melee
{
    public sealed class MeleeSurfaceWitnesses
    {
        public sealed class Slot
        {
            public MeleeMesh Mesh {get;internal set;}
            public MeleeVertex[] Vertices {get;internal set;}
        }
        public IReadOnlyList<Slot> Slots {get;}
        public int SourceVertices {get;}
        public int EquationCount {get;}
        public MeleeSurfaceWitnesses(IEnumerable<MeleeMesh> meshes)
        {
            var slots=new List<Slot>();int original=0,unique=0;
            foreach(var mesh in meshes)foreach(var group in mesh.Vertices.GroupBy(v=>v.MatrixIndex))
            {
                var vertices=group.ToArray();original=checked(original+vertices.Length);
                // A matrix slot may be shared only with an identical ordered
                // influence equation. Never merge owners, slots or mesh bindings.
                if(vertices.Any(v=>!v.Weights.SequenceEqual(vertices[0].Weights)))throw new InvalidDataException("Native matrix slot has inconsistent ordered skin influences.");
                var positions=vertices.GroupBy(v=>v.NativePosition).Select(g=>g.First()).ToArray();unique=checked(unique+positions.Length);
                slots.Add(new Slot{Mesh=mesh,Vertices=positions});
            }
            Slots=slots.AsReadOnly();SourceVertices=original;EquationCount=unique;
        }
    }
}
