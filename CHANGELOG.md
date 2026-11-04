# Changelog

Optimal GT Designer is a fork of [GT Plus](https://github.com/Coow/GT-Plus) 0.1.28 by Coow. Every change below was made
on this fork, from the first update onwards. Throughout, titles stay plain vMix GT files: no feature changes how a
`.gtzip` is written or how vMix reads it, and editor-only aids are never saved into the file.

## 0.1.52 — 5 November 2026

The first published release of Optimal GT Designer. It includes every change below.

### Interface
- **Ribbon interface laid out like vMix GT Title Designer** (File, Home, Styles, Format, Effects, Animations, Data,
  plus a View tab), in a dark theme. Groups the selection can't use are greyed out rather than hidden.
- **One Move tool (V)**: click to select, drag to move, drag a handle to resize, Shift-click to add to the selection,
  Cmd/Ctrl-drag to box-select.
- **Option/Alt-drag a resize handle** to resize from the centre; Shift keeps the proportions.
- **Free Transform (Cmd/Ctrl+T)**, as in Photoshop, for one object: a box that turns with the object, eight handles and
  a centre point.
  - Corners scale with the proportions kept (Shift: freely), sides stretch, and Option scales from the centre.
  - Drag outside the box to rotate (Shift: 15° steps); drag inside, or use the arrow keys, to move.
  - Enter or a double-click applies it as one undo step; Esc or Cmd+Z cancels.
- **Photoshop-style cursors**: double arrows along the direction each handle pulls (turned with the object), and a
  curved rotate arrow. Corner handles used to show the four-way move cursor.
- **Grid or solid canvas background**; drag-to-move on the canvas.

### Layers panel
- **Clipping masks like Photoshop**:
  - Option-click the line between two rows, or right-click › Create / Release Clipping Mask.
  - Holding Option shows Photoshop's clip cursor.
  - Clipped rows are indented behind a bent arrow, and their base is underlined.
  - Saved as GT's own `<Mask Object>`.
- **Clipped objects can only be clicked where they're visible.**
- **Double-click a row to rename it**; rows show the normal arrow pointer.
- **Dragging an object into another layer keeps it in place on screen.** The layer's frame grows to cover the object,
  but never past the canvas edge.
- **Cut, Copy and Paste**: Cmd/Ctrl+X/C/V, the Home tab's Clipboard group, and the row's right-click menu. Cut takes
  the object's animations with it, and pasting after a cut brings the object back under its own name.
- **Copy Attributes / Paste Attributes** (right-click): copy one object's Location, Rotate and Dimensions values onto
  the selected objects.
- **Layer groups from the keyboard**: Cmd/Ctrl+D duplicates the selected layer, Delete or Backspace deletes it, and
  the arrow keys move it.
- **Backspace deletes**, as the Mac keyboard's delete key should.
- **A copy of a numbered layer ("2") gets the next free number**, instead of a name like "Element3".

### Format
- **Format › Layer › Fit to visible** shrinks a layer's frame to the pixels its contents actually draw, without moving
  anything on screen. It never makes a frame bigger than the canvas.
- **Layers stay inside vMix's layer viewport.**
  - vMix only draws a canvas-sized area of each layer, measured from the layer's top-left, and the canvas now clips
    layers the same way.
  - Opening or saving a title whose layer reaches past that area shows a ⚠ note in the status bar.
  - This fixes objects that showed in the editor but vanished in GT and vMix.

### Fonts and text
- **Font weights are matched the way vMix matches them**: each face's true weight is read from the font itself, so
  the editor draws the face vMix will. For example, GFF Latin Black no longer shows as ExtraBold on macOS.
- **The weight menu lists the family's real faces**, each with a sample.
- **Home › Fonts › Replace fonts**: swap family, weight and style for every text object that uses a font, in one step.
- **Faux italic as in Photoshop**: a 10° GT Skew.

### Effects and orientation
- **Skew, Reflection and Texture Flip effects.**
- **Masks are drawn from the mask object's own pixels**, as GT's engine draws them.
- **Skew, rotation and shadows match vMix**, checked against vMix with a test title:
  - a positive Skew X leans forward;
  - a positive RotateZ turns counterclockwise;
  - RotateX/RotateY, skew centre offset and shadow offset match too.

### Photoshop import
- **PSD/PSB import** following GT's own import rules: each top-level group becomes a layer cropped to its contents,
  pixel layers become rectangles, and text layers become text.
- **Photoshop clipping masks** become GT masks.
- **Text rotation and slant** become RotateZ and Skew; faux italic becomes a 10° skew.
- **Fonts are matched** by their exact PostScript name.
- **Photoshop's flattened preview** is loaded as the reference image.

### Reference image
- **View › Reference** lays the original design (the PSD preview, or any image) over the title, with opacity and
  difference modes. It is editor-only and never saved.
- **Cmd/Ctrl+Shift+R** turns it on and off.

### Timeline
- **Figma-style timeline**:
  - The ruler and names stay pinned while the track scrolls.
  - Groups collapse.
  - The working area is highlighted.
  - The control bar never scrolls, and its height can be dragged.
  - The timeline can be detached into its own window.
- **Objects are grouped under their layer**, top layer first, as in the Layers panel.
- **Keyframe shapes show the easing**, as in After Effects: diamond for linear, circle for eased, square for bounce.
- **Cmd/Ctrl+scroll zooms** the time axis around the pointer.
- **Space plays and pauses** only when you're working in the timeline.
- **GT's 1 · 2 · 3 transition slots** in the timeline bar. Picking a type fills the slot exactly as GT does: timing is
  kept, and `<None>` placeholders are written where GT writes them.
- **Continuous storyboard plays as vMix plays it**:
  - Delay, Duration, easing and Reverse are ignored, as vMix ignores them, and are greyed out.
  - Blink, FillOffset and StrokeOffset now preview.
  - ImageSequenceLoop is timed by Speed, and the Frames… button sets that Speed.
- **The timeline's show/hide shortcut is now Cmd/Ctrl+Shift+T.**

### Platforms
- **macOS app** with Cmd/Option shortcuts and Mac-style shortcut labels.
- **Windows** (x64, ARM64) and **Linux** (x64, ARM64) builds, all self-contained, so no .NET install is needed.
- **The app checks this repository for updates.**

### Documentation
- A `docs/` folder covering the ribbon, canvas, layers, masks and clipping, effects, fonts, PSD import, the reference
  image, the timeline and installing on each platform.
