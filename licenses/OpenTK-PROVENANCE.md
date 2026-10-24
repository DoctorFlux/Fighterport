# OpenTK dependency provenance

FighterPort Studio V2 uses the app-local OpenTK binary supplied by the pinned
BrawlCrate foundation, without rebuilding or modifying it. The binary is excluded
from source control and is restored explicitly with `scripts/Restore-Dependencies.ps1`.
There is no system installation or automatic download during application startup.

- Binary source: <https://github.com/soopercool101/BrawlCrate/blob/d56cbac89dc2cd0e5c3f923e1d03041b16902add/BrawlLib/OpenTK.dll>
- Binary SHA-256: `931E38B47B824C316A55B5A54D55650C978CA90DDE1DAEA02299EDDD94E719C0`
- Windows file/product version: `1.0.278.44921`
- Binary copyright metadata: `Copyright © 2006 - 2010 the Open Toolkit Library`
- Official historical license source: <https://github.com/opentk/opentk/blob/bca3751e8b38355e61b94afd5ce63fb89f7e669e/Documentation/License.txt>
- Retained license: `OpenTK-LICENSE.txt`, copied verbatim including the Mono,
  Tao Framework and OpenEXR notices.
- License file SHA-256: `D05DDD089170A043CC9FD221DFE09D113C0D9A734C2354E41C972FC07C6DBDB1`

The official historical license has the same copyright period as the binary's
metadata. The exact OpenTK source revision used to produce the upstream binary
has not been established; V2 pins the actual BrawlCrate-provided binary by hash.
The historical OpenTK license is permissive, while the BrawlCrate/BrawlLib
foundation retains its own license and notices separately.

For offline restore, pass `-Offline -UpstreamCheckout <local-pristine-checkout>`.
The script reads only `BrawlLib/OpenTK.dll` from that checkout, verifies its hash,
and stages a copy under V2 before replacing the ignored V2 dependency.
