This is a standalone third party editor for vMix GT Title Designer, that adds extended functionality to the editor itself.

The file format is explained in the GTZIP-Format.md file.

Don't automatically commit to git

**Golden rule:** no feature may change how a `.gtzip` is written or how vMix GT Title Designer reads it. Express new
features with structures GT already has (masks, effects, ordinary element attributes); anything editor-only (reference
images, UI aids, view state) stays in memory or preferences and is never written into the file.

---

## Project structure

**Framework:** Avalonia 12.1.2, .NET 10, C# - target `net10.0`.

**Project file:** `src/GtPlus/GtPlus.csproj`

---

## Source layout

### Models - `src/GtPlus/Models/`
- **`GtModels.cs`** - all data model classes
  - `GtDocument` - root; holds `Width`, `Height`, `List<GtLayer>`
  - `GtLayer` - named layer; `Location` (GtPoint), `Dimensions` (GtSize), `InnerWidth/Height`, `List<GtElement>`, `Locked`, `Visible`
  - `GtElement` (abstract) - base: `Name`, `Location`, `Dimensions`, `Visible`, `Opacity`, `Locked`, `DataFlags`
  - `GtTextBlock`, `GtImageElement`, `GtRectangleElement` - concrete elements
  - `GtTickerElement : GtTextBlock` - scrolling ticker; adds `Speed` (px/frame), `Direction`,
    `TickerType`, and the template text (kept in the inherited `Text`). Subclassing means every
    `is GtTextBlock` test also catches a ticker - order type switches ticker-first
  - `GtWebElement` - live web page drawn as a design reference; `Url`, `Interactive`,
    `TransparentBackground`. Saved as a carrier object (`WebPagePart`) so it survives a
    round-trip through this editor, but never drawn into a video export - vMix only ever sees
    the empty rectangle it is stored as
  - `GtEffect` - one entry of GT's generic per-object effect list (`GtElement.Effects`,
    `GtLayer.Effects`); `GtEffectType` × `GtEffectMode`, where Mode is the pipeline *stage*
    not the effect kind, so a shadow is `Type=Shadow, Mode=Shadow`. Defaults are omitted on
    write and re-seeded on read, so an absent `Color` means opaque black
  - `GtBrush` - solid/gradient/bitmap; `GtBrushType`, `GtGradientStop`
  - `GtStoryboard` - animations for one vMix event. Identified by the pair `(Type, DataName)`:
    `DataName` scopes a DataChangeIn/Out storyboard to one data field, empty means any field
  - `GtPoint(X,Y)`, `GtSize(Width,Height)` - immutable records; `Location`/`Dimensions` properties have setters so assign `new GtPoint(x,y)` to mutate
- **`GtShadow.cs`** - the only effect type that renders: GT's 12-preset gallery, `Apply` (GT's
  destructive set-shadow semantics), `Resolve` (collapses the whole shadow stage into one
  `GtResolvedShadow` - chained Gaussians compose exactly), and `BlurRadiusForSigma`, the single
  place the Avalonia-radius ↔ GT-sigma conversion lives

### Services - `src/GtPlus/Services/`
- **`GtZipReader.cs`** - reads `.gtzip` → `(GtDocument, Dictionary<string,byte[]> assets)`. Assets keyed by logical path (forward-slash normalised).
- **`GtZipWriter.cs`** - writes `(GtDocument, assets)` → `.gtzip`. Generates fresh GUIDs for asset blobs. Writes `document.xml` as UTF-8 (no BOM). Atomic write via temp file + rename.
- **`HistoryService.cs`** - undo/redo stack. `IHistoryAction` interface. Concrete actions: `MoveElementsAction`, `ResizeElementsAction`. `HistoryService.Push/Undo/Redo/Clear`, fires `Changed` event.
- **`RecentFilesService.cs`** - persists recent file paths
- **`PreferencesService.cs`** - persists user preferences (outside-canvas opacity, debug panel visibility)
- **`GtDataFieldService.cs`** - the composition's data fields (`Name.Text`, `Name.Source`,
  `Name.Fill.Color`/`.Fill.Bitmap`, ticker template children). Only direct children of a
  top-level layer register one, and the list is reversed, matching GT. These are the scopes a
  DataChangeIn/Out storyboard can be keyed to; `Hidden`/`NoEvents` fields are not offered
- **`ImageSequenceBuilder.cs`** - turns files/folders on disk into a GTZIP image sequence: natural
  (digit-aware) frame ordering, one logical asset folder per sequence, frame 0 as the anchor
- **`TickerLayout.cs`** - ticker text splitting + GT's per-frame scroll walk, replayed as a pure
  function of the frame number (`Simulate`) or laid out flush at rest (`Rest`)
- **`WebPagePart.cs`** - stores `GtWebElement`s in a .gtzip the way `GuidesPart` stores guides: each one is swapped **in place** for a rectangle that draws nothing (transparent fill, no stroke) carrying the page's real geometry in its own attributes and `name|url|interactive|transparent` base64url-encoded into its `Name`. Swapping in place is what makes layer and z-order round-trip without being encoded. `Replace`/`Restore` bracket the write; `ExtractCarriers` runs on load. Animations aimed at a web page are still dropped on save - the file holds it under the carrier's name, not its own
- **`ChromiumService.cs`** - locates an installed Chrome/Edge/Chromium (prefs path -> PATH -> usual install dirs). Nothing is bundled or downloaded
- **`CdpSession.cs`** - one headless browser driven over the Chrome DevTools Protocol: `Page.startScreencast` streams the page as PNG frames (every frame must be answered with `Page.screencastFrameAck` or the stream stops after three), `Input.dispatch*` sends clicks, wheel and keys back. No Avalonia types - bytes and numbers only
- **`WebPreviewService.cs`** - one `CdpSession` per `GtWebElement`, keyed by element reference. Starts/stops sessions from the document (a deleted or hidden element loses its browser), debounces resize, coalesces frames so a slow decode drops frames instead of queueing them
- **`FfmpegService.cs`** - locates the ffmpeg binary (prefs path → app dir → PATH → usual install dirs); can download the official Windows build into `%APPDATA%/GtPlus/ffmpeg`
- **`VideoExportService.cs`** - MP4 export. Drives `GtCanvasControl.AnimationFrame` + `ExportToBitmap()` one frame at a time on the UI thread and pipes raw BGRA into ffmpeg's stdin via a bounded background writer
- **`UpdateService.cs`** - `AppVersion.Current` (from the linked version.json resource) plus the GitHub `releases/latest` check and dotted-numeric version compare. Never downloads anything - a newer release only opens the release page
- **`Models/GtSkew.cs`** - GT's Skew effect: `Get`/`Set` the first `Type=Skew` effect (0,0 removes it) and `Matrix()` for the shear. Sign: positive X = forward/italic lean, as GT/vMix draw it (the opposite of D2D/Avalonia `CreateSkew`, so `Matrix` negates both angles). The canvas applies it in `RenderElementBodySkewed` (inside any mask clip) and to mask geometry in `BuildMaskGeometry`; the Effects tab's Skew group drives it through `WireCrop`. Hit-testing and selection handles still use the unskewed box
- **`Models/GtReplaceEffects.cs`** - `GtTextureFlip` (FlipX = "Horizontal", FlipY = "Vertical"), `GtReflection` (Finish stage, `Depth` = share of height the fade reaches) and `GtReplaceEffects.Matrix()`, which composes flips and skews in list order. The canvas draws through it in `RenderElementBodySkewed` (reflection first, via `RenderReflection`) and reshapes mask geometry with it
- **`Psd/`** - PSD/PSB import, no third-party library. `PsdReader` decodes the file (header, ResolutionInfo, layer records
  with flags/opacity/fill opacity/lock/section dividers, channel data raw/RLE/ZIP, 8/16/32-bit, RGB/grey/CMYK, layer masks;
  layer effects, adjustment layers and blend modes are not evaluated). `PsdTextReader` reads 'TySh' (transform, descriptor,
  EngineData: font, size, leading, faux styles, caps, colour, justification, paragraph box). `PsdImporter` follows vMix GT's own
  PSDImport helper (top-level group -> GT layer named after it and cropped to contents; loose layers share a layer; pixel layer
  -> rectangle with a PNG bitmap fill clipped to the canvas; type layer -> TextBlock; names lose dots, max 15 chars, unique;
  hidden layers skipped) with deliberate improvements: nested groups flatten into their top-level group, group/fill opacity
  multiply in, masks apply, any canvas size is allowed (flagged when vMix would refuse it). Sizes in EngineData are pixels
  (GT multiplies by dpi/72 only because its library reports points). Point text is placed by its first baseline using
  `GtCanvasControl.FirstLineGeometry`, so it lands where Photoshop drew it. `MainWindow.ImportPsdAsync` (File > Import PSD...,
  or drop a .psd/.psb on the canvas) loads the result as a new unsaved title
  - Clipping masks (`PsdLayer.Clipped`): `AssignClipBases` gives each clipped layer the nearest unclipped sibling below it.
    A layer or text base becomes GT's own `<Mask Object="base" />` on the clipped object (same GT layer, so it always is);
    a *group* base has no single object to name, so its combined coverage (`Context.GroupAlpha`) is multiplied into the
    clipped pixels. Hidden base -> clipped layer skipped; text clipped to a group imports unclipped with a warning
- **`PlatformKeys.cs`** - `PlatformKeys.Command` is Cmd (`KeyModifiers.Meta`) on macOS and Ctrl elsewhere; every shortcut and modifier-drag checks it instead of `KeyModifiers.Control` (only the raw modifiers forwarded to web pages stay literal). Option already arrives as `Alt`. `Label()` / `LocalizeToolTips()` rewrite the Windows-style "Ctrl+Shift+N" tooltips as "⇧⌘N" on macOS, run once from `MainWindow.OnOpened`
- **`Logger.cs`** - static logger

### Packaging - `packaging/`
- `build-all.sh [version] [mac|windows|linux...]` builds self-contained single-file packages into `dist/`: mac arm64/x64
  `.app` zips (via `macos/build-mac.sh [version] [rid]`, needs macOS for codesign), windows x64/arm64 zips
  (`Optimal GT Designer.exe`), linux x64/arm64 tar.gz (`optimal-gt-designer` + `.desktop` + png + `install.sh`)
- Icons: `macos/AppIcon.icns` (bundle), `windows/AppIcon.ico` (embedded as ApplicationIcon only when building on
  Windows, NETSDK1074 elsewhere), `src/GtPlus/Assets/App/app-icon.png` (every Window's Icon via a style in App.axaml)

### Controls - `src/GtPlus/Controls/`
- **`GtCanvasControl.cs`** - main canvas; renders document, handles selection + move + resize
  - Enums: `CanvasTool { Select, Edit, TextBox, Rectangle, Ticker }`, `ResizeHandle { None, NW, N, NE, E, SE, S, SW, W }`
  - Styled props: `Zoom`, `OutsideCanvasOpacity`, `ActiveTool`
  - Plain prop: `HistoryService? History` - push actions here
  - Selection: `SelectedElements`, `SetSelection`, `ToggleSelection`, `ClearSelection`, `SelectionChanged` event
  - Select tool: click to select, drag an element to move it (not while previewing), Shift+click to toggle, Ctrl+drag for rubber-band box
  - Move tool (`CanvasTool.Edit`): click to select, drag to move, handle drag to resize, Cmd/Ctrl-drag to box-select;
    Alt on a resize handle resizes from the centre (`ApplyResizeDelta(fromCenter)`, Alt read live on every move; snapping
    uses the centred `ResizedBounds`)
  - Resize handles: 8 per selected element (individual bounds, not group bounds); all selected elements resize by same delta
  - Locked elements / locked layers cannot be moved or resized
  - `ElementAbsBounds(layer, el)` → absolute doc-space Rect (layer.Location + el.Location)
  - Web elements: `WebPreviews` (a `WebPreviewService`) supplies frames; `RenderWeb` skips
    everything in export mode. When an element is `Interactive` the canvas forwards pointer,
    wheel and key events to its page instead of selecting/moving - Alt bypasses, resize handles
    still win, Escape returns the keyboard (`WebInputFocused`)
  - Shadows: `RenderShape` wraps `RenderShapeCore` in `ctx.PushEffect` so crop, mask and opacity
    all act on element-plus-shadow as GT's compose does. Avalonia folds an ambient opacity into
    both the content and the effect, double-fading the shadow, so any opacity that may sit over
    one goes through `PushComposedOpacity` (an opacity *mask*, which forces a real layer) instead
    of `PushOpacity`
  - Ticker clock: `PlayTicker`/`PauseTicker`/`StopTicker`/`ToggleTickerPlayback`, `TickerPlaying`,
    `TickerFrame` - a 60 fps DispatcherTimer counting frames. A storyboard preview takes the
    clock over while `AnimationFrame` is set; frame 0 draws tickers at rest
- **`LayersPanelControl.axaml/.cs`** - layers panel; populated via `Populate(GtDocument)`, syncs selection highlight via `Canvas` property
- **`TimelineTrackControl.cs`** - the storyboard track, custom-drawn in Figma's motion-timeline layout: animated objects
  are grouped under their GT layer (`RowKind.Layer`, from `Document`; top layer first; a layer's own animations sit
  directly under it; `Row.Depth` drives the indent; `IsGroup` = Layer or Object), then per object a
  collapsible group row with a grey summary bar over all its animations, then one row per animation drawn as a start
  and end keyframe joined by a line (shape by easing: diamond linear, circle ease, square bounce; `KeyShapes`) (type colour on the diamonds, direction arrow beside the name, hollow
  start diamond = Reverse). Drag a diamond to retime that end, the line to move, a group bar to move every animation
  of that object; the gutter chevron folds a group (`IsCollapsed`/`SetCollapsed`), a group's eye mutes all of it.
  The ruler and name gutter are pinned: `TimelinePanelControl` feeds `TrackScroller.Offset` into `ScrollOffset`, and
  the track draws/hit-tests them at that offset, so all geometry stays in content coordinates. `LeadIn` keeps a
  keyframe at 0s clear of the gutter
  - Working area: `WorkingArea()` merges the spans of every unmuted animation (segment offsets applied); the ruler
    gets a blue bar and tint over each span and the rows a faint wash, gaps where nothing animates stay unmarked
- **Timeline bar** (`TimelinePanelControl.axaml`, `BarHost`): never scrolls. Zoom/detach/close are docked right; the
  rest are items of `BarWrap` (picker+transport, storyboard commands, the clip grid, the direction pad) that wrap onto
  more lines as the panel narrows. `BarGrip` under it pulls the bar's height (38px up to what leaves `MinTrackHeight`
  for the track); double-click resets it to fit its contents. `OnSizeChanged` caps `BarHost.MaxHeight` the same way
- **Transition slots** (`TimelinePanelControl`, `SlotPanel`): GT's 1/2/3 slot toggles + `SlotTypeCombo`. Slot N = the
  object's Nth row on `TargetStoryboard`, placeholders included (`SlotRows`, ordinal name match like GT).
  `SetSlot` mirrors GT's `GraphicsStoryboard.SetAnimationBuilderByIndex`: swap in place copying Delay/Duration/Reverse/
  Interpolation/Speed (`GraphicsAnimationBuilder.CopyTo`), pad with `<None Object/>` placeholders to reach a later
  slot, None writes a placeholder. Targets: objects of the selected clips, else the canvas selection. One undo step
  (whole Animations list snapshot). `SlotsChanged` → `RefreshSlotUi`
- **Continuous storyboard preview** (`GtAnimationEvaluator`): per vMix's builders, the 5 continuous types ignore Delay, Duration,
  easing and Reverse (`elapsed = time`; `EarliestStart` skips them). Blink hides when frac(Speed·t) ≥ 0.5. Fill/StrokeOffset use
  GT's OffsetTransition vector table → `GtAnimOverride.Fill/StrokeOffsetX/Y` (fraction of the box) → canvas `Slid()` translates
  the brush (picture tiles; Pad gradients repeat). ImageSequenceLoop: `LoopPosition` = (frame mod (⌊frames/Speed⌋+1))·Speed/frames
  at `OutputFps` 60, Stretched mode (the only one GT uses); the Frames… helper sets Speed = n/(n−1), not Duration.
- **Timeline zoom**: `TimelinePanelControl.OnTrackWheel` (tunnel on `TrackScroller`) handles Cmd/Ctrl+wheel through
  `ZoomTimeline(factor, viewportX)`, which sets `ZoomSlider.Value` (→ `Track.PixelsPerSecond`) and shifts the scroll
  offset so the time under the pointer stays put (a pointer over the pinned gutter anchors at its edge)
- **Space = play/pause** only when the timeline is the active surface: `_timelineIsActiveSurface` (a window-level
  tunnel press sets it false, the timeline's own tunnel press sets it true), `TimelinePanel.IsPointerOver`, or the
  detached timeline window is active. Text inputs (`IsTextInputSource`) never trigger it
- **Detached timeline** (`MainWindow.DetachTimeline`/`DockTimeline`): the panel's detach button raises
  `DetachRequested`; MainWindow moves the *same* `TimelinePanel` out of its DockPanel into an unowned `Window`
  (unowned so macOS does not tie it to the main window), remembering host/index/height to put it back. Closing that
  window docks it; `SetTimelineVisible(false)` while detached docks then hides. Its KeyDown/KeyUp forward to
  `MainWindow.OnKeyDown`/`OnKeyUp` so shortcuts work there. Position/size persist as `TimelineWindow*` prefs and are
  only reused while that point is on a connected screen
- **`HistoryPanelControl.axaml/.cs`** - history list; set `History` property to wire up; auto-refreshes on `HistoryService.Changed`

### Views - `src/GtPlus/Views/`
- **`MainWindow.axaml/.cs`** - main window
  - Fields: `_currentPath` (open file path), `_currentAssets` (asset dict), `_history`, `_reader`, `_writer`
  - **Ribbon** (top): `PropertiesPanelControl` is laid out as vMix GT's ribbon - File tab, then Home / Styles / Format / Effects / Animations / Data, plus a GT Plus-only View tab. Each group is up to three 22px rows over a caption; groups the selection cannot use are disabled (greyed), never hidden
    - Window-level commands live in MainWindow.axaml and are dropped into the panel's slot properties (`FileContent`, `QuickAccessContent`, `HomeStartContent`, `HomeEndContent`, `AnimationsContent`, `DataContent`, `ViewContent`), so their named controls stay in the window's name scope
    - The old Edit / Layer / View menus are gone: undo, clipboard and layer commands are Home tab buttons; guides, rulers, snap and preferences are View tab toggles. The `*MenuItem` names were kept on those buttons/toggles so the code-behind is unchanged apart from `IsChecked == true` reads
    - Shared ribbon styles (tab, `rb lg` / `rb sm` / `rb row` buttons, `Path.ic` line icons, palette) are in `Styles/Ribbon.axaml`
  - File menu built in code via `RebuildFileMenu()` - includes Open, Save (Ctrl+S), Save As (Ctrl+Shift+S), recent files, Exit
  - Toolbar (left): Move (V), Text Box (T), Rectangle (R), Ticker (K), Web Page (W), Image (I). Move is `CanvasTool.Edit`
    behind `EditToolButton`; `SelectToolButton` is hidden and `SetTool(CanvasTool.Select)` maps to Edit, so there is one
    tool that selects, moves, resizes and (Cmd/Ctrl-drag) box-selects. `CanvasTool.Select` survives only as the
    preview-mode fallback inside the canvas
  - Right panel: tab strip (Layers / History) → `LayersPanel` / `HistoryPanel`; Debug panel below (toggled in Preferences)
  - Bottom bar: status text, Fit button, zoom slider + label
  - `OnKeyDown`: S=Select, E=Edit, Ctrl+Z=Undo, Ctrl+Y=Redo, Ctrl+S=Save, Ctrl+Shift+S=SaveAs
  - Middle-click drag = pan canvas; Ctrl+scroll = zoom
- **`ExportVideoWindow.axaml/.cs`** - MP4 export dialog: storyboard (single, or the TransitionIn+TransitionOut pair), hold seconds, fps, quality, background, output path
- **`SequenceLengthWindow.axaml/.cs`** - sets an ImageSequence clip's Duration from the sequence's
  frame count and a target frame rate (default 60 fps); opened by the timeline strip's "Frames..." button,
  and straight away by the "+ Sequence" button / layers-panel "Add Image Sequence Animation..." entry, which
  create the clip on the storyboard the timeline is showing (both grey out when no storyboard is selected)
- **`PreferencesWindow.axaml/.cs`** - preferences dialog (outside canvas opacity, debug panel toggle, update check on startup)
- **`UpdateWindow.axaml/.cs`** - update check result: version numbers, release notes, "Open Download Page" / "Skip This Version". Opened from File > Check for Updates, and at startup only when a newer, non-skipped release exists

---

## Key patterns

- **Coordinate spaces:** Element `Location`/`Dimensions` are layer-local. Canvas renders with `layer.Location` as transform offset. `ElementAbsBounds` converts to absolute doc space. Zoom is a scale transform on the whole canvas control.
- **Mutation:** Model objects mutate in place (properties have setters). Canvas calls `InvalidateVisual()` after mutations. History actions store before/after snapshots.
- **History:** After any user gesture that changes the model, construct the appropriate `IHistoryAction` and call `GtCanvas.History.Push(action)`. `HistoryService.Changed` fires → `MainWindow.OnHistoryChanged` enables/disables menu items.
- **Assets:** `Dictionary<string,byte[]>` with forward-slash logical paths as keys. Stored in `MainWindow._currentAssets`. Writer regenerates GUIDs on save.
- **Shortcuts:** write new key handlers with `PlatformKeys.Command`, never `KeyModifiers.Control`, and write tooltips Windows-style ("Ctrl+X"); macOS relabels them on open.
- **Masks (canvas):** as GT's engine does it (`GraphicsObject.OnCompose` in GraphicsEngine.dll), the mask object is
  composed in full and its *pixels* are the mask - bitmap alpha, gradients, crop, effects, opacity and its own animation.
  `RenderElement` records the mask object into a `DrawingGroup` (`BuildMaskBrush`) and pushes it as an opacity mask; a
  non-ticker TextBlock mask uses its exact glyph outline (`BuildMaskGeometry`), which is also the fallback when a mask
  cannot be recorded (perspective rotation uses a custom draw op). A mask object with `Visible=false` masks nothing.
  Only rendering changed - the file still stores just `<Mask Object="Name" />`
- **Clipping (Layers panel):** `LayersPanelControl.ToggleClip` sets/clears `MaskObject` to the object below
  (`ClipTargetFor`: the one below, or its base when that is clipped itself). Option-click on the line between two rows
  (`ClipLineTarget`: within `ClipLineReach` of a row's bottom = this row, of its top = the row above) and the row context
  menu call it; with Option held over such a line the row shows Photoshop's clip cursor (`MakeClipCursor`, crossed out
  for release), refreshed on Option key down/up via top-level key handlers. `ClipBaseOf` = the mask object when it sits
  below in the same layer; such rows get the bent-arrow indent (`ClipArrowPath`) and their base an underline. Canvas `HitTest` skips a masked object where
  `IsDrawnAt` (renders it into a 3×3 bitmap around the point) finds no alpha
- **Copy/Paste Attributes** (Layers row menu, `LayersPanelControl.CopyAttributes`/`PasteAttributes`): static
  `_copiedAttributes` = anchor X/Y, Z, RotateX/Y/Z, Width/Height/Depth (the Format tab's Location/Rotate/Dimensions).
  Paste sets size first then `SetAnchorX/Y` on every unlocked selected element; one undo step restoring raw Location
- **Reference image:** `GtCanvasControl.ReferenceImage/Visible/Opacity/Difference`, drawn by `RenderReference` above the
  layers and skipped in `_exportMode`; MainWindow's View › Reference group drives it (`SetReference`). After a PSD import
  it is `PsdImportResult.ReferencePng`, from `PsdFile.Composite` (the image data section, read by
  `PsdReader.ReadComposite`). Never written to the file
- **Font faces:** `Services/FontFaces.cs` lists a family's faces with their *true* weight (OS/2 usWeightClass, what
  DirectWrite/vMix matches) and the platform's reported weight (CoreText on macOS gives e.g. 947 for a Black). `Pick`
  applies the CSS/DirectWrite matching rule on true weights; `FontFaces.Typeface(...)` is what every text draw uses, so
  the face drawn is the one vMix would pick. The Font group's weight menu (`RebuildWeights`) lists real faces with a
  sample; `ByPostScriptName` gives the PSD importer exact matches
- **Fonts:** `ReplaceFontsWindow` (Home › Fonts) groups text by (family, weight, style) and rewrites those three
  attributes, one undo step. The Font group's faux italic button sets Skew X = `FauxItalicAngle` (+10°, Photoshop's
  measured slant) with `CenterOffset` on the first baseline
- **Rotation sign:** vMix turns a positive `RotateZ` counterclockwise on screen (its 3D world is y-up). Every canvas
  rotation goes through `GtCanvasControl.ScreenRz` (= −RotateZ); never call `Matrix.CreateRotation(rotateZ)` directly.
  Verified in vMix (2026-11-04) with an 8-sample test title: RotateZ +20, Skew X +15, Skew Y +15, Skew X with
  CenterOffset 0,+90, RotateX +40, RotateY +40, Shadow offset +20,+20 and an untouched reference all match the canvas
- **PSD text transforms:** `TextTransform.Decompose` QR-splits the 2×2 into rotation (→ `RotateZ`), X shear (→ Skew) and
  scale (→ font size); faux italic adds −tan10°·sx/sy to the shear. The box is placed so `L = O − size/2 − T(a − size/2)`
  with T = R·S about the centre. The canvas applies a Replace-stage reshape *before* Z rotation, as GT does
  (`RenderElementBodySkewed` conjugates it by the rotation)
- **Fit to visible (Format › Layer):** `GtCanvasControl.VisibleContentBounds(layer)` renders the layer's visible objects
  (export mode, at rest) into a frame-sized bitmap and returns the alpha bounding box in doc space;
  `MainWindow.FitLayerToVisible` floors/ceils it to whole pixels, moves/resizes the layer (keeping any Inner/Dimensions
  ratio) and shifts its elements the opposite way, one undo step. Verified pixel-identical on the Waiting Lobby import
- **Canvas background:** View tab › Background switches `CanvasBackground` between Grid and Solid (Black also resets `CanvasSolidColor` to black); Preferences edits the colours and grid size.
- **No XAML bindings for file menu** - built entirely in `RebuildFileMenu()` C# code because recent files are dynamic.
