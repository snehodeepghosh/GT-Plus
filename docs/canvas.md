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

## Free Transform (Cmd+T)

As in Photoshop, **Cmd+T** (Ctrl+T on Windows) puts one selected object into Free Transform. A box with eight handles
and a centre reference point appears, turned with the object.

- **Drag a corner** to scale with the proportions kept; hold **Shift** to scale freely. **Drag a side** to stretch one
  way. Hold **Option** (Alt) to scale from the centre.
- **Drag anywhere outside the box** to rotate about the centre; hold **Shift** to snap to 15° steps.
- **Drag inside the box** to move it; the arrow keys nudge it.
- **Enter**, or a double-click inside the box, applies the transform. **Esc** (or Cmd+Z) puts the object back as it
  was. The whole transform is one undo step.
- Selecting something else, switching tools or using another command applies it first.
- The pointer shows Photoshop's cursors: a double arrow along the direction a handle pulls (turned with the box), and
  the curved rotate arrow outside the box. The ordinary Move tool handles use the same arrows.
- It works on one unlocked object at a time. It changes the object's Location, Width/Height and Rotate Z, which are
  plain GT attributes, so the file stays plain GT.

The storyboard timeline's show/hide shortcut moved to **Cmd+Shift+T** to make room.

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

## Layers must stay on the canvas

vMix draws a layer's contents through a window the size of the canvas, measured from the layer's own top-left corner.
If a layer's frame starts left of or above the canvas, or runs past it, the objects that end up beyond the canvas width
or height in that layer's coordinates are **not drawn at all** in GT or vMix.

- The canvas shows a layer the same way, so anything vMix would lose disappears here too.
- Opening or saving a title with such a layer shows a ⚠ note in the status bar naming it.
- To repair it, select the layer and use **Format › Layer › Fit to visible**. Fit to visible never makes a frame larger
  than the canvas.

## Layers panel

- **Double-click** a layer or object row to rename it in place. **Enter** keeps the new name, **Esc** cancels, and
  clicking elsewhere keeps it. Right-click › Rename still works.
- Rows show the normal arrow pointer. Drag a row up or down to reorder it.
- Dragging an object into **another layer** keeps it where it is on screen: its position is converted to the new
  layer's frame. If the object reaches outside that frame, the frame **grows** just enough to cover it, so nothing is
  cut off. The frame never grows past the edge of the canvas (see below). The layer's other objects don't move on screen, and undo puts both the object and the frame back.
- Holding **Option** over the line between two rows shows the clipping cursor; see [Masks](masks.md).
- With a **layer** (group) selected, **Cmd+D** duplicates it and **Delete** or **Backspace** deletes it, the same as
  the Home tab's Duplicate and Delete buttons. With objects selected they act on the objects. A copy of a layer with a
  number for a name ("2") gets the next free number ("4").
- **Cut / Copy / Paste** are on an object row's right-click menu, on the Home tab's Clipboard group, and on
  Cmd+X / Cmd+C / Cmd+V (Ctrl on Windows). Cut copies the selected objects, with the animations aimed at them, and
  deletes them in one undo step. It is refused when any selected object or its layer is locked. Pasting after a cut
  brings the objects back under their own names, so their animations still match; pasting a copy while the original
  is still there names it like a duplicate.
- **Right-click › Copy Attributes** remembers an object's Format values: Location X/Y/Z, Rotate X/Y/Z and
  Width/Height/Depth. **Right-click › Paste Attributes** gives those values to every selected, unlocked object, as
  one undo step. X/Y are the numbers the Format tab shows, so they are measured at each object's own anchor point;
  objects with different anchors get the same numbers, not the same box. The copy is kept until you copy something
  else, even across titles. Only ordinary object attributes change, so the file stays plain GT.
