# Version families and structural screening

`build-semantic.ps1 -AdapterId weixin-4.1.15.56-x64` selects a baseline profile. Each build emits `qttestability.dll`, `beautify-engine.dll` and a copy of its manifest as `adapter.json` in `research/<BuildFolder>/`. Profiles marked `pending` cannot build. A `verified` status records the baseline's limited local tests (4.1.15.56 during 0.5, 4.2.0.13 during 0.6); it does not certify every same-family version or all UI behavior. `-SkipTests` compiles test programs without running them and prints a warning. Packaging defers execution to a fresh extracted validation directory, then requires both native test programs for every bundled adapter and the controller self-test to pass before producing a checksum.

The loader checks the file version's first three components and x64 PE machine. The baseline SHA-256 distinguishes the tested artifact; an unknown SHA-256 within that family may proceed to the engine's read-only structural screening. Historical pixel-probe builds retain exact-hash admission. There is no cross-family fallback.

Before any hook write or private drawing function call, the semantic engine checks:

- The loaded image's x64 PE headers and section table.
- Every directly called private function RVA lies in a readable executable image section.
- Each known object table's MSVC Complete Object Locator has a coherent primary-interface layout and an executable first function.
- Key object getters and Qt metaobject names match the independently observed baseline metadata.
- Every paint-interface table has the expected `0xc0` displacement, the same type descriptor as its primary table, and the expected callback slot.
- The session-row table has the expected displacement, type descriptor and callback slot.

Failure writes `theme.receipt` with the current request revision, process ID, `state=incompatible` and a reason. Successful hook installation and GUI timer creation immediately write `state=ready`; later configuration and actual draw callbacks produce `pending`/`applied`.

These checks are structural heuristics. They do not compare complete function bodies or prove that every private field still has the same meaning. A same-family update can pass and still draw incorrectly or crash. Quit the themed process and use the ordinary WeChat entry to restore normal behavior. No installation file is patched. The controller must label unfamiliar hashes as untested.

`test-semantic.cpp` covers layout policy. `test-preflight.cpp` constructs synthetic PE metadata in its own memory, accepts a coherent fixture, and rejects altered machine, metadata names, executable ranges, object tables and callback slots; it does not load WeChat or test actual rendering.

## 4.2.0.13 investigation

The installed x64 PE has SHA-256 `afe3d02c58bada3c35009c0cd58ee1f57b2a00fe53237d0dce649107fddb9a20`. Independent PE/Capstone scanning recovered 2,042 primary-table/Qt-metaobject name mappings, including inherited names. This is metadata coverage, not a complete type reconstruction.

Selected observed RVAs (file image base `0x180000000`):

| Metadata name | Primary table | `0xc0` paint table | Second callback |
| --- | --- | --- | --- |
| `mmui::XTextView` | `0x94b99a8` | `0x94b9bd8` | `0x1edf780` |
| `mmui::MainView` | `0x98debb8` | `0x98dedd8` | `0x4194760` |
| `mmui::ChatSessionList` | `0x9c8a7d8` | `0x9c8a9f8` | `0x1eeb1f0` |
| `mmui::ChatSessionCell` | `0x9c8c5c8` | `0x9c8c858` | `0x21d3f30` |

The cell also has a secondary interface at `0x120`, table `0x9c8c888`, with second callback `0x61603f0`. These callback/table addresses differ from 4.1.15.56. Follow-up independent disassembly confirmed the following call sites and field access patterns, enabling an **experimental 4.2.0.X** profile; there is no fallback to 4.2.1.X or other families.

| Role | 4.2.0.13 RVA | Independently checked behavior |
| --- | --- | --- |
| QImage filename constructor / destructor | `0x506b60` / `0x5074d0` | Constructor initializes image and forwards filename/format to `0x506ba0`; destructor releases its shared storage. |
| Image wrapper | `0x70b60` | Copies the QImage into retained storage; `0x53c790` and `0x1172b90` construct the drawing resource. Same RVA as baseline, but independently disassembled. |
| Image resource base | `0x562cc0` | Reference count at `+8`; width/height pair at `+0x20`. |
| Paint constructor / ARGB setter / destructor | `0x53c8e0` / `0x53d290` / `0x53cbc0` | Paint storage and retained-resource ownership. |
| Canvas rectangle / image | `0x546610` / `0x549340` | Forward to virtual slots `+0xb8` / `+0x118`; image routine consumes width/height at resource `+0x20`. |
| Widget update | `0x5c09c0` | Reads widget geometry through `+0x28` and sends invalidation through private data `+8`. |
| Text setter / cache invalidation | `0x1ed8ca0` / `0x1ed8580` | Text color `+0x234`, cache block `+0x188`, paragraph holder `+0x3e0`. |
| Paragraph color virtual / invalidation | `0x45bbcb0` / `0x6377580` | Paragraph virtual `+0x78`; invalidation with notification disabled avoids the observed baseline repaint loop. |
| Session-row selected state | `0x6160c00` | Tests cell byte `+0x210`. |

Background and row callbacks independently show interface adjustments `-0xc0` / `-0x120`, widget-data `+0x28`, and rectangle bounds `+0x14` through `+0x20`. The session-cell constructor `0x615b4e0` constructs a derived text view at `0x488f590` (table `0x99a8d58`, stored in cell `+0x268`) and a base text view at `0x1ed6770` (table `0x94b99a8`, stored in cell `+0x270`). This confirms ownership of the two label types; their visible role and exact runtime parent chain remain to be observed. The strict row-text parent-chain filter may skip coloring when it does not match.

Four primary tables expose the `mmui::MainWindow` metaobject. The 4.2 profile accepts each as a root only after its getter/metaobject and object-layout checks; it still requires an enclosing MainView. The 4.1 profile's root check is unchanged.

`research/check-4.2-profile.py` checks the actual PE without loading it: 100 paint tables, 2,042 object tables, 14 direct-function executable ranges, 12 key getter/metaobject-name guards and the row slot matched. `build-semantic.ps1 -AdapterId weixin-4.2.0.13-x64 -BuildFolder independent-4-2-experimental -SkipTests` compiled both DLLs and test sources; **no test executable or WeChat process was run**. Visual background placement, font coloring, refresh, restore and runtime stability remain unverified. Experimental status must remain visible in the client and must not be described as a stable release.

Local read-only reproduction: `research/inspect-version-adapter.py`, `disasm-version-function.py`, `generate-4.2-adapter.py` and `check-4.2-profile.py`; complete metadata map: `research/weixin-4.2.0.13-abi-map.json`. Saved disassembly includes `disasm-4.2-*.txt` and `weixin-42-text-setters.txt`, `weixin-42-text-cache-update.txt`, `weixin-42-paragraph-verified.txt`, `weixin-42-labels.txt`, `weixin-42-label-calls.txt`. REA identified the target PE/hash/architecture, while these deeper findings are independent static PE/Capstone observations, not REA runtime/decompiler evidence. No WeChat process was started, stopped or modified for this investigation.

### Subsequent live validation

After the static investigation above, the user authorized a live launch of 4.2.0.13. The controller ran at Medium integrity. The main window and File Transfer Assistant showed the custom background, native text, themed session selection and input-region overlay. Restore produced a matching disabled receipt and a visibly native appearance; reapply produced a new matching applied receipt and restored the theme. The user also confirmed the version in WeChat's About page. The profile is now `verified` for this limited baseline check, not for every same-family file or all UI behavior. See `../RESEARCH.md` for the receipt observations and remaining coverage.
