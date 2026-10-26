using System;
using System.Globalization;

namespace BrawlCrate.Studio
{
    internal static class LibraryNativeOrder
    {
        // Resource keys record native child indices. Compare indices numerically,
        // preserving depth-first PAC/BRES traversal (2 precedes 10). Compute once
        // per indexed row; presentation never opens or sorts native resources.
        internal static string Key(string resourceKey)
        {
            if(resourceKey==null)return "1";
            var parts=resourceKey.Split('/');
            if(parts[0]!="$")return "1"+resourceKey;
            var indices=new string[parts.Length-1];
            for(int i=1;i<parts.Length;i++)
            {
                if(!int.TryParse(parts[i],NumberStyles.None,CultureInfo.InvariantCulture,out int index)||index<0||
                    parts[i]!=index.ToString(CultureInfo.InvariantCulture))return "1"+resourceKey;
                indices[i-1]=index.ToString("D10",CultureInfo.InvariantCulture);
            }
            // Retain readable legacy/non-native metadata after native identities.
            return "0"+string.Join("/",indices);
        }
    }
}
