# Contributing to Majorsilence.Forms

Contributions are welcome — fork, branch, open a pull request against `main`. AI-assisted changes are
welcome too: whether you wrote the code by hand or with a coding assistant, the bar is the same. Note
in the PR that a diff was AI-assisted if a reviewer would find that useful context; it isn't a
separate approval track.

This file is the long version of the readme's [Contributing](readme.md#contributing) section. It is
mostly about **how to know your change is actually right**, because this is a compatibility layer: the
characteristic failure here is not a crash, it is a member that compiles, runs, returns something
plausible, and silently does not do what WinForms does.

## Build and test

```bash
dotnet build --configuration Release
dotnet test
```

The solution multi-targets `netstandard2.0`, `net8.0` and `net10.0` (a few projects add `net48`), so a
change that compiles for one may not compile for all — always build the solution, not just the project
you edited.

### The four gates

CI runs the suite in four shapes, and a change can pass three and fail the fourth. Run all of them
before opening a PR; the suite takes about five seconds, so there is no reason not to.

```bash
dotnet test -c Debug
dotnet test -c Release
MF_FORCE_CUSTOM_CHROME=1 dotnet test -c Release
MF_FORCE_CUSTOM_CHROME=1 MF_HEADLESS_SCALE=2 dotnet test -c Release -- xunit.parallelizeTestCollections=false
```

- **`MF_FORCE_CUSTOM_CHROME`** — macOS is the only platform that uses the OS's window decorations;
  every other platform draws the library's own `FormTitleBar`, whose caption buttons are *implicit*
  children. Anything touching `ClientSize`, tab order, hit-testing near the top of a form, or
  `Controls` enumeration takes a different path on each side, so a test written on macOS can pass
  vacuously. This variable makes a macOS process take the other path.
- **`MF_HEADLESS_SCALE=2`** — makes the headless backend report a scaled display, exercising the
  logical↔device conversions that are identity at scale 1. It **must** run serially: a test that opens
  a modal dialog picks its owner out of the global `Application.OpenForms`, and in parallel it can pick
  another test's window and wait on it forever.
- The migrator suite takes over ten minutes (it builds generated projects) against seconds for
  everything else, and is OS-agnostic. CI runs it once, on Windows. Exclude it locally with
  `--filter "FullyQualifiedName!~Migrator"`.

**One thing that does not have an environment variable:** the ambient default font measures **11px of
text height on macOS and 13px on Windows and Linux**. Any assertion comparing two controls' heights, or
a height to a constant, can hold on one platform and fail on the other — and all four gates above use
the same font, so they will not catch it. Parameterise the font in the test instead (an
`[InlineData(null)] / [InlineData(24f)]` theory exercises both the "glyph taller than text" and "text
taller than glyph" regimes everywhere).

## Writing the change

### Generate, don't transcribe

Values, defaults, event orders and metrics should come from upstream `dotnet/winforms` or from the code
that consumes them — not from memory. Every time this repo has transcribed a number, it has transcribed
something wrong. Cite the upstream file and line in a comment when the reason for a value is not
obvious from the code.

The same applies to this repo's own planning documents. The `Fix:` notes in
[`docs/behaviour-gap/`](docs/behaviour-gap/) are a starting point, not a specification: several have
turned out to name the wrong hook, the wrong number, or a design upstream does more simply. If one is
wrong, fix the code and correct the note.

### Coordinates

`Bounds` and `MouseEventArgs` are **logical** units; `ClientRectangle`, back buffers and captured
bitmaps are **device** pixels. They are identical at scale 1, so mixing them is invisible until a
scaled display shows up. Convert once, at the boundary — the established pattern for a control that
lays out in device units is to convert the mouse point on the way in (see
`ListBox.GetIndexAtLocation`), not the bounds on the way out.

### The stub policy

An unimplemented member **no-ops or returns a sensible default; it never throws
`NotImplementedException`.** Migrated code should compile *and run*. The cost of that policy is that a
silent no-op is the hardest kind of gap to find, which is why accepting one is a recorded act — see the
baselines below. [`COMPATIBILITY_MATRIX.md`](COMPATIBILITY_MATRIX.md) is the document migrating
developers and coding assistants read to decide whether a control is safe to use, so **a row that
overstates is a defect in its own right**; update it alongside the code it describes.

### Trimming and NativeAOT

`IsAotCompatible` is on for every non-`netstandard2.0` target, which turns the trim, AOT and
single-file analyzers into errors in Release. Two conventions:

- A genuine dynamic-code dependency is annotated `[RequiresDynamicCode(...)]`, not suppressed — there
  are no `IL3050` suppressions in this repo, and adding one claims a guarantee the code cannot make.
- When annotating would propagate onto public API, restructure to avoid the dependency instead. For
  example, `BindingSource` builds a **closed** `BindingList<object?>` and records the element type
  separately, rather than reaching a typed `BindingList<T>` through `MakeGenericType`.

A local `dotnet build` will not show these unless you enable the analyzer:
`dotnet build -c Release -p:IsAotCompatible=true`.

### Style

`.editorconfig` carries the mechanical rules (4-space indent, sorted usings). Two conventions it does
not encode:

- A space before the parameter list — `Method ()`, `new Rectangle (x, y, w, h)`.
- Comments explain **why**, not what, and are worth their length when they record something that cost
  someone an afternoon: which upstream behaviour a line reproduces, what broke before it existed, why
  an obvious-looking alternative does not work. Match the density of the file you are editing.

## Tests

**A test written after the fix is not a test until it has been shown to fail without it.** Before
opening the PR, break your own change — comment out the fix, or short-circuit it — and confirm each new
test goes red. This repeatedly catches tests that pass for the wrong reason, and the failure mode is
always the same shape: an assertion loose enough that the *unfixed* behaviour also satisfies it.

Concrete traps, all found this way:

- **Absolute thresholds.** "The wrapped panel is at least 60px tall" is satisfied by the panel's own
  default height without any wrapping. Assert a *relationship* instead — narrow versus wide, a
  difference rather than a bound, one font size against another.
- **Coincidences.** A `Button` and a `CheckBox` with the same caption happened to be the same height on
  macOS (11px text + 1px border = the 13px glyph). Nothing required that, and it failed on every other
  platform. Assert what the mechanism guarantees, against the thing that owns the number.
- **"Some ink in this region."** Any drawing satisfies it. Pin something only the intended rendering
  can produce — a header band's fill colour, ink past a deliberately wide column with too few items for
  any other layout to reach that far.

A test that *cannot* fail against the old behaviour — a negative or a shape guard — is legitimate, but
say so in a comment so the next reader does not mistake it for proof.

### The baseline gates

Four committed baselines pin how hollow the layer is, and shrinking them is the point. Each fails if an
entry is **added** (you introduced a new stub) and prompts if one is **removed** (you wired something
up — regenerate and commit the smaller file):

| Gate | Records | Regenerate with |
|---|---|---|
| `NoOpStubBaseline.txt` | empty-bodied public `void` methods | `MAJORSILENCE_WRITE_STUB_BASELINE=1` |
| `InertEventBaseline.txt` | events whose accessors are `add { } remove { }` | `MAJORSILENCE_WRITE_INERT_EVENT_BASELINE=1` |
| `UnraisedEventBaseline.txt` | field-backed events nothing raises | `MAJORSILENCE_WRITE_UNRAISED_EVENT_BASELINE=1` |
| `StoredOnlyPropertyBaseline.txt` | settable auto-properties nothing reads | `MAJORSILENCE_WRITE_STORED_ONLY_BASELINE=1` |

```bash
MAJORSILENCE_WRITE_STORED_ONLY_BASELINE=1 dotnet test --filter "FullyQualifiedName~StoredOnly"
```

Absence from a baseline is not a certificate: "read by code that is itself inert" needs transitive
reachability, which the scanners do not do. They are the floor, not the ceiling.

The **API gap gate** is separate: [`tools/Majorsilence.Forms.ApiDiff`](tools/Majorsilence.Forms.ApiDiff)
diffs the public surface against the real reference assemblies by reflection. Both baselines are at
zero and should stay there.

```bash
dotnet run --project tools/Majorsilence.Forms.ApiDiff -- --surface winforms --check
```

### Diagnosing a rendering fault

Render to a PNG on the headless backend rather than launching a GUI: `HeadlessRenderer.Use ()`,
`form.Show ()`, then `HeadlessRenderer.CapturePng (form)`. It avoids the window server entirely and is
reproducible. Two things that repeatedly decide investigations:

- **Sample pixels** to tell a colour bug from a geometry bug — decode and print the colour at chosen
  points. "Inside the control it is F0F0F0, one pixel outside it is 636C87" settles in one step what
  reading palette code does not.
- **Render before dumping the tree.** Strip items are laid out inside `OnPaint`, so a tree dumped before
  the first paint shows every item at 0,0 — which looks exactly like the bug you are hunting.

If you do run a sample app, launch it from its build output directory or with
`dotnet run --project samples/<Name>`: every sample loads icons through a **relative** `Images` path, so
from the repo root every icon silently degrades to a 1×1 placeholder and it reads as a rendering bug in
the framework.

## Larger compatibility work

Behavioural gaps are tracked in [`docs/behaviour-gap-plan.md`](docs/behaviour-gap-plan.md) (phases,
progress, and a "What phase N found" section per landed item) with per-finding detail in
[`docs/behaviour-gap/`](docs/behaviour-gap/), each citing both sides' `file:line`. If you close a
finding, annotate it there — the things discovered *while* fixing have consistently been more valuable
than the original findings. [`BACKLOG.md`](BACKLOG.md) holds what is wanted, deferred, or decided
against, with the reasoning.

## Pull requests

- **Commit messages**: an imperative subject line, then a body explaining *why* — what was broken, what
  the user-visible symptom was, and anything a future reader would otherwise have to rediscover. The
  existing history is the reference; messages end with the last line of real content, with no trailers.
- **`main` is protected** by the "Tests Must Pass" ruleset: the `build` jobs (macOS, Ubuntu, Windows)
  and the `migrator` jobs are required, and branches must be **up to date** before merging. A PR that
  sat while `main` moved shows as `BEHIND` even with every check green — `gh pr update-branch <n>`, or
  merge `main` in, and CI re-runs against the combined tree.
- Keep one logical change per commit where the files allow it. Where a change spans documentation that
  other work also touches, it is better to land one honest commit than to split it into an attribution
  that does not survive a rebase.

## Code of conduct

Participation is governed by the [Code of Conduct](CODE_OF_CONDUCT.md) (Contributor Covenant 2.1).
Conduct concerns go to the maintainers **privately** — a private report on the repository (GitHub →
*Security* → *Advisories* → *Report a vulnerability*, which despite the name is a private channel), or
GitHub's own [report abuse](https://github.com/contact/report-abuse) flow. Not a public issue.

For bugs and feature requests, open an issue.
