# Example smoke harness

Constructs every `Form` in the Krypton Extended Toolkit's `Examples` assembly on the headless backend and
reports which ones throw.

## Why

Clicking through the demo costs a launch, a crash, a fix and a rebuild *per bug*, and the crashes hide each
other — you only ever see the first one. This finds every constructor-time failure in a single pass. It took
the Examples app from 29/35 forms constructing to 34/35 in one sitting, and three of the fixes were found
only because the harness reached forms that a human clicking would not have got to yet.

It is NOT a substitute for running the app. Painting, layout, theming and interaction are all untested here
-- a form that constructs can still render wrongly. Use it to clear the crashes, then look at the app for
everything else. The literal-ampersand bug (`&OK` instead of an underlined O) was invisible to this and
obvious in a screenshot.

## Running

    dotnet run --project tools/Majorsilence.Forms.ExampleSmoke

Copy the Win32 shim dylibs into the output's `runtimes/osx/native/` first, or every form fails on
`user32.dll` at `Krypton.Toolkit.PI.RegisterWindowMessage`:

    cp <any-built-app>/runtimes/osx/native/*.dylib \
       tools/Majorsilence.Forms.ExampleSmoke/bin/Debug/net10.0/runtimes/osx/native/

Exit code is 0 when every form constructs, 1 otherwise.

## Note on rebuilds

Use `--no-incremental` after editing the Krypton sources. The Extended projects write to a shared `Bin/`
path and an ordinary incremental build was observed to leave a stale assembly in place, which reports a
failure that has already been fixed.
