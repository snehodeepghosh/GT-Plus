# Canvas

## The Move tool (V)

One **Move** tool, shortcut **V**, does everything the separate Select and Edit tools used to:

- **Click** an object to select it, **drag** to move it.
- **Shift-click** adds an object to the selection or removes it.
- **Cmd-drag** (Ctrl-drag on Windows) draws a selection box, wherever it starts.
- **Drag a handle** to resize. Add **Shift** to keep the proportions, and **Option** to resize from the centre.
- **Click empty canvas** to clear the selection.
- With a layer selected, dragging inside its frame moves the layer, and its handles resize it.
- Locked objects and objects on locked layers don't move or resize.
- While a storyboard preview is showing, clicks only select; nothing moves.

## Resizing from the centre

With the **Move** tool, hold **Option** (Alt on Windows) while dragging a resize handle to resize from the centre: the
opposite side moves out, or in, by the same amount, so the object stays centred where it was.

- **Option** on a side handle widens or narrows the object on both sides, or makes it taller or shorter at top and
  bottom.
- **Option** on a corner handle resizes both ways about the centre.
- **Option+Shift** on a corner also keeps the proportions.
- Press or release Option during the drag to switch modes as you go.
- With several objects selected, each resizes about its own centre.

## Background

**View › Background** switches what shows behind the title:

- **Grid**: the transparency checkerboard.
- **Black**: solid black, the way the title will key over black in vMix.

The grid's colours and size and the solid colour can be changed in Preferences.

## Fit a layer to its visible area

**Format › Layer › Fit to visible** shrinks a layer's frame to exactly what its contents draw:

- **What counts:** only drawn pixels. Transparent parts of images, and anything a mask or clipping cuts away, are
  left out. Shadows and glows are included.
- **Nothing moves:** the frame moves and resizes, and the contents move the other way, so nothing shifts on screen.
- **Which layers:** the selected layer, or every layer holding a selected object.
- **Locked layers** are skipped. The whole fit is one undo step.

Only content inside the current frame counts, since that is all the layer shows. It only changes the layer's
position and size and its objects' positions, all ordinary GT values. Animations on the layer itself (Fly, Zoom,
Reveal) are measured against its frame, so they now play relative to the tighter frame.

## Layers panel

- **Double-click** a layer or object row to rename it in place. **Enter** keeps the new name, **Esc** cancels, and
  clicking elsewhere keeps it. Right-click › Rename still works.
- Rows show the normal arrow pointer. Drag a row up or down to reorder it.
- Dragging an object into **another layer** keeps it where it is on screen: its position is converted to the new
  layer's frame. If the object reaches outside that frame, the frame **grows** just enough to cover it, so nothing is
  cut off. The layer's other objects don't move on screen, and undo puts both the object and the frame back.
- Holding **Option** over the line between two rows shows the clipping cursor; see [Masks](masks.md).
- **Right-click › Copy Attributes** remembers an object's Format values: Location X/Y/Z, Rotate X/Y/Z and
  Width/Height/Depth. **Right-click › Paste Attributes** gives those values to every selected, unlocked object, as
  one undo step. X/Y are the numbers the Format tab shows, so they are measured at each object's own anchor point;
  objects with different anchors get the same numbers, not the same box. The copy is kept until you copy something
  else, even across titles. Only ordinary object attributes change, so the file stays plain GT.
