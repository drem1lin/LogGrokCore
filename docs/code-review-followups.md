# Code review follow-ups

Status as of 2026-06-20. A multi-area code review (data layer, app/WPF layer,
diagnostics/bootstrap, test coverage) produced ~40 findings. The high-confidence,
low/medium-risk ones were fixed across these commits:

- `ee663d4` Phase 1 — latent bugs + diagnostics (BOM detection, PooledList.Contains,
  ChunkedList.Clear, Search.Progress TrySetResult, DelegateCommand.CanExecuteChanged,
  Loader stream dispose, DeploymentVersion log header, Dispatcher/UnobservedTask hooks,
  SingleInstanceManager empty-arg filter, ParsedBufferConsumer fault handling)
- `82a6079` Phase 2 — subscription/lifecycle leaks (document/search VM dispose,
  Selection unsubscribe + VirtualList disposes evicted items, MarkedLineViewModels cache)
- `a332726` Phase 3 — render hot-path allocations (GetTextBounds, frozen pens, null pen,
  ElementAt O(n^2))
- `af9bab4` Phase 4 — unit tests for pure logic
- `deaa91e` localized document tab context menu
- `85917fc` activate existing tab instead of reopening
- `7f6821a` Phase 5 — ColorSettings hex guard, [ThreadStatic] guard, MatchSurgery fallback,
  LineIndex polling→event
- `c371a21` Phase 5 #6 — ClippingRectProviderBehavior leak/hashcode collisions

Test count went 89 → 152.

### Phase 9 — data perf/correctness + Y17 dispose (pending commit)
- **Y17 (dispose)** `MarkedLinesViewModel` now `IDisposable`: named handlers replace the ctor
  lambdas and `Dispose()` unsubscribes from the static `TranslationSource`, the documents
  collection, and every per-document `MarkedLinesChanged`. `MainWindowViewModel.Dispose()` disposes
  it. Because it is a disposable transient created via a factory, its DryIoc registration now uses
  `Setup.With(allowDisposableTransient: true)` (the owner disposes it) — without this the container
  throws `RegisteredDisposableTransientWontBeDisposedByContainer` at startup.
- **Y19** `IndexerBase.GetIndexCountForComponent` caches the matching `IndexKeyNum` set per
  (component, value) and rescans only when the key count grows (append-only index); per-key counts
  are always summed live, so counts stay correct while loading.
- **Y20** `LineProvider.Fetch` validates the byte range against `Int32` and throws a clear
  `InvalidOperationException` instead of silently overflowing the `(int)` cast (>2 GB / huge line).
- **Y23** `StringPool` bounds each size bucket (`MaxPooledPerBucket`) so returned buffers past the
  cap are dropped for GC instead of being retained forever (slow leak over long sessions).
- **Y22 (skipped)** the `int[]` in `RegexBasedLineParser.Parse(string)` backs the returned
  `ParseResult` and is read later by `LineViewModel`; it outlives the call, so the suggested
  stackalloc would be a use-after-free. Left as-is.
- Tests: +2 `StringPool` cap, +1 `LineProvider` range guard (188 → 191).
- Validated by running the app on a 2 GB log after fixing the startup crash the Y17 change first
  introduced: document loads/renders, app closes cleanly, no crash events.

### Phase 8 — reliability (committed b138b36)
- **Y12** `SearchDocumentViewModel.SetIsSearching` wrapped in try/catch (async void) and the
  min-show delay clamped via the extracted, unit-tested `RemainingMinShowDelay` (never negative);
  `ListView.ScheduleMandatoryRemeasure` wrapped in try/catch.
- **Y13** `ListView.ScheduleResetColumnsWidth` bounds its ApplicationIdle self-reschedule
  (`MaxResetColumnsRetries`) so an open app with no document no longer idle-spins; the per-column
  `ActualWidth` subscription is now a named handler (no per-column closure, unsubscribable).
- **Y14** `BaseLogListViewItem` subscribes `Items.CurrentChanged` via Loaded/Unloaded instead of
  the ctor (recycled/virtualized containers no longer leak); `UpdateIsCurrentProperty` null-guards
  `Content`.
- **Y6**  `SingleInstanceManager.StartListeningNextInstances` moves try/catch inside the accept
  loop so one bad connection no longer permanently stops "open in existing window"; message parsing
  extracted to unit-tested `ParseInstanceMessage`.
- **Y17 (partial)** `UpdateLinesCollection` index access bounded against `IndexOutOfRange`.
  NOTE: the review premise was wrong — `MarkedLinesViewModel` is registered **transient** (not
  `Reuse.Singleton`, `App.xaml.cs:101`), so its un-unsubscribed static `TranslationSource`
  subscription is a real (not benign) leak if resolved more than once. Left for a follow-up that
  adds a dispose/lifecycle hook.
- Tests: +3 `RemainingMinShowDelay`, +4 `ParseInstanceMessage` (181 → 188).
- Validated by running the app: 2 GB document opens with correct column sizing and row coloring,
  row selection is stable, and idle CPU is 0% with both an empty and a loaded document (confirms
  the Y13 idle-loop fix). UI-lifecycle paths (Y13/Y14) have no unit coverage — validated by running.

### Phase 7 — render hot-path perf (committed beebce7)
- **Y7** `ColorSettings` regexes compiled with `RegexOptions.CultureInvariant`;
  `BaseLogListViewItem.OnContentChanged` short-circuits when there are no color rules.
- **Y8** `SearchPattern.GetRegex` caches the compiled `Regex` by (effective pattern, options)
  instead of recompiling on the UI thread per committed search.
- **Y9** `TextControl` search-highlight geometry uses `regex.EnumerateMatches(span)` — no
  per-line `Text.ToString()` + `Matches().ToList()`.
- **Y10** `CollapsibleRegionsMachine` builds position→region-index maps once in the ctor
  instead of rebuilding two dictionaries on every `Update()`/toggle.
- **Y11** `TextView.MeasureOverride` builds the visible-lines list in a single pass (no chained
  LINQ / double `ToList`); `GuideLinesControl.OnRender` pushes one `GuidelineSet` per render
  instead of allocating one per line.
- Tests: +4 SearchPattern caching, +6 CollapsibleRegionsMachine, +3 ColorSettings (168 → 181).
- Validated by running the app on a 2 GB log: load, rule coloring, text/measure rendering and
  an active search all work; no crash. (Y9 highlight geometry / Y11 rendering have no unit
  coverage — render paths are validated only by running.)

### Phase 6 — safe quick wins (committed 468f0f9)
Addressed the low-risk batch: **Y4** (structured open/duration/throughput logs in
`Loader`), **Y15** (`SearchAutocompleteCache` specific catches + `Trace.TraceWarning`),
**Y16** (`DragnDropBehavior` handler now a static method group so `RemoveHandler` matches),
**Y18** (removed dead `LoaderImpl.GetPatterns` SWAR code), **Y24** (`PooledList` indexer
bounds check), **Y25** (`StringShortenerConverter` → `Binding.DoNothing` on `UnsetValue`),
**Y26** (converters `ConvertBack` → `NotSupportedException`), **Y27** (`FormatText*`
honor `culture`), **Y28** (deleted unused `GlyphRunSurgery`). Build + 152 tests green.

---

## Remaining — RED (high risk, do with care + benchmarks)

### R1. `IndexTree` is mutated on the loader thread while the UI reads it (data race)
- Files: `LogGrokCore.Data/IndexTree/IndexTree.cs`, `Index/Indexer.cs`, leaf types.
- `Indexer.Add` runs on the ParsedBufferConsumer background thread; the UI reads via
  `IndexedLinesProvider` → `IndexTree.GetEnumerableFromIndex/...` during live filtering of a
  still-loading file. `IndexTree.Add` mutates `_count/_head/_currentLeaf` and leaves append to
  a `List<T>` — none synchronized (unlike `LineIndex`, which locks).
- Symptom: intermittent `InvalidOperationException`/torn reads when filtering during load.
- Why risky: a lock on every `Add` (millions of calls) hurts load throughput; the readers use
  lazy `yield` enumeration that can't be held under a lock. Needs a snapshot or lock-free
  design + benchmarks. Do NOT add a naive lock.

### R2. `LineProcessor` silently drops lines that fail to parse
- File: `LogGrokCore.Data/LineProcessor.cs:73-86`.
- When `TryParse` fails and `_currentOffset != 0`, the line is decoded into the buffer but not
  indexed/counted, and `_currentOffset`/`_bufferOffset` bookkeeping only handles the
  `_currentOffset == 0` case → non-conforming lines vanish; possible offset drift.
- Why risky: may be partly intentional (multi-line records); changing it touches parsing/index
  correctness and there are no `LineProcessor` tests. Needs explicit design for unparseable
  lines + tests first.

### R3. `Indexer._currentCount++` + GetOrAdd value-factory race
- File: `LogGrokCore.Data/Index/Indexer.cs:47-62`.
- `_currentCount++` is non-atomic and the "did the factory run?" detection via
  `_currentCount > keyCount` is fragile. Currently safe (single consumer thread), but
  `SubIndexer` shares the same dictionaries. Use `Interlocked` and detect new-key insertion
  robustly before any concurrency is introduced here.

---

## Remaining — YELLOW (medium risk / value)

### Diagnostics
- Y1. First-chance handler still logs *every* exception and the filter scans
  `exception.StackTrace` (forces a full stack walk) per throw. Consider gating the whole
  `FirstChanceException` subscription behind verbose mode and matching on exception *type* /
  `TargetSite` instead of substring-scanning the formatted stack trace.
  (`Diagnostics/ExceptionsLogger.cs`, `FirstChanceExceptionsFilter.cs`. The reentrancy guard is
  already [ThreadStatic].)
- Y2. `LoggerToTraceListenerAdapter` (`Diagnostics/LoggerToTraceListenerAdapter.cs`):
  `_message` StringBuilder is not thread-safe (interleaved Trace.Write from threads);
  `Flush()` is a no-op that can lose buffered Write() content; `new StackFrame(3)` per
  `TraceEvent` is fragile + expensive. `LogLevelFormatter` maps Error and Fatal both to "ERR".
- Y3. `App.OnExit` flushes then `TerminateProcess` immediately — use
  `LogManager.Flush(timeout)` + `Shutdown()` first (`Bootstrap/App.xaml.cs:71-85`).
- Y4. Add structured open/duration logs when opening a file (size, parse time) — highest-value
  diagnostic addition for a log viewer.

### Bootstrap
- Y5. WER elevation (`Bootstrap/EntryPoint.cs`, `Diagnostics/CrashDumpConfiguration.cs`):
  re-prompts UAC on every launch if the user declined; `SettingsChanged` only compares
  `DumpCount` (not DumpType/DumpFolder); `WaitForExit(30000)` blocks startup; only
  `UnauthorizedAccessException` is caught around HKLM access (SecurityException can escape).
- Y6. `SingleInstanceManager.StartListeningNextInstances` exits the accept loop permanently on
  any error (first instance stops accepting "open in existing window"); no abandoned-mutex
  handling. (`Bootstrap/SingleInstanceManager.cs`.)

### App / rendering perf
- Y7. Per-row `newContent.ToString()` + linear scan of N compiled regexes on every container
  realization (`Controls/ListControls/BaseLogListViewItem.cs:133-156`, `Colors/ColorSettings.cs`).
  Use `as string`, short-circuit when Rules empty, RegexOptions.CultureInvariant; consider one
  combined alternation regex.
- Y8. `SearchPattern.GetRegex(Compiled)` recompiles the regex on the UI thread per committed
  search (`Search/SearchDocumentViewModel.cs:159-162`, `Search/SearchPattern.cs:30`). Cache the
  compiled Regex; ideally build off-thread.
- Y9. `TextControl.OnRender` does `regex.Matches(line).ToList()` + `Text.ToString()` per line —
  use `regex.EnumerateMatches(span)` (`Controls/TextRender/TextControl.cs:155-157`).
- Y10. `CollapsibleRegionsMachine.Update()` rebuilds two dictionaries on every toggle though
  region membership never changes (`Controls/TextRender/CollapsibleRegionsMachine.cs:117-118`).
- Y11. `TextView.MeasureOverride` chained LINQ + double ToList per layout
  (`Controls/TextRender/TextView.cs:350-359`). `GuideLinesControl` allocates a GuidelineSet per
  line per render (`GuideLinesControl.cs:90-99`).

### App / reliability
- Y12. `async void` handlers: `SearchDocumentViewModel.SetIsSearching` (can compute negative
  Task.Delay → throws), `ListView.ScheduleMandatoryRemeasure`; clamp delays, convert to async
  Task or guard.
- Y13. `ListView.ScheduleResetColumnsWidth` re-schedules itself at ApplicationIdle until ready
  (potential idle busy-loop) and subscribes a per-column PropertyChanged handler never removed
  (`Controls/ListControls/ListView.cs:219-240`).
- Y14. `BaseLogListViewItem` subscribes `Items.CurrentChanged` in ctor, never unsubscribed;
  `UpdateIsCurrentProperty` can NRE on null `Content` (`:32-35,165-168`).
- Y15. `SearchAutocompleteCache` swallows all exceptions with bare `catch {}` (load/save) —
  catch specific, Trace.TraceWarning (`Search/SearchAutocompleteCache.cs:34-37,48-51`).
- Y16. `DragnDropBehavior.OnDropCommandChanged` calls RemoveHandler with a freshly-created
  delegate, so it never matches the added one (handler leak) (`Controls/DragnDropBehavior.cs:96-101`).
- Y17. `MarkedLinesViewModel` subscribes static `TranslationSource.PropertyChanged` with no
  unsubscribe (benign while app-lifetime singleton); `UpdateLinesCollection` indexed mutation is
  fragile (`MarkedLines/MarkedLinesViewModel.cs`).

### Data / perf & correctness (lower)
- Y18. `LoaderImpl.GetPatterns` computes 4 SWAR patterns that are never used — dead code; remove
  or finish the SWAR newline scan (`LogGrokCore.Data/LoaderImpl.cs:34,164-191`).
- Y19. `IndexerBase.GetIndexCountForComponent` is an O(keys) LINQ scan per call — cache per
  component (`Index/IndexerBase.cs:41-47`).
- Y20. `LineProvider.Fetch` casts the byte span size to `int` — guard/checked for >2GB or huge
  single lines (`LineProvider.cs:30-38`).
- Y21. `CountIndex` allocates a fresh snapshot on every `Counts` read before finish; mark
  `IndexTree._count` volatile if read cross-thread (`Index/CountIndex.cs`).
- Y22. `RegexBasedLineParser.Parse(string)` allocates an `int[]` per call (stackalloc for small
  component counts) — secondary path, hot only if used in a loop.
- Y23. `StringPool` buckets grow unbounded (slow leak over long sessions with varied line sizes).
- Y24. `PooledList` indexer has no bounds check against `_count`.

### Misc converters / fragility
- Y25. `StringShortenerConverter.Convert` throws on `UnsetValue` instead of `Binding.DoNothing`.
- Y26. `IsNullToVisibilityConverter`/`ObjectToTypeConverter` `ConvertBack` throw
  `NotImplementedException` (use NotSupportedException; latent crash if a binding is TwoWay).
- Y27. `FormatTextExtension`/`FormatTextMultiExtension` ignore the supplied `culture`.
- Y28. `GlyphRunSurgery` reflects a private WPF field in a static ctor (same fragility class as
  the now-guarded MatchSurgery; appears unused — verify/remove).

---

## Notes
- GUI-affecting changes (context menu, tab dedupe, LineIndex event signal, ClippingRect) are
  only validated by running the app — no unit coverage for those paths.
- Test gaps still worth filling (easy + valuable): IndexKey equality/hash, SearchLineIndex,
  FilteredCountIndicesProvider, StringTokenizer edge cases, ParsedLineComponents/LineMetaInformation
  size math.
