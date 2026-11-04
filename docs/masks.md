# Masks

In GT Title Designer any object can be masked by another object in the same layer (**Effects › Mask** picks the
mask object). The file stores only the mask object's name:

```xml
<Rectangle.Mask>
  <Mask Object="Map Shape" />
</Rectangle.Mask>
```

## How the canvas draws a mask

The canvas follows GT's own engine. In `GraphicsObject.OnCompose` in GraphicsEngine.dll, GT draws the mask object
in full first, then lets the masked object show only where the mask object drew something. So the mask is the mask
object's **pixels**, not its bounding box:

- an image used as a mask cuts by its transparency
- a gradient fading to transparent fades the masked object with it
- the mask object's crop, opacity and effects count
- the mask object's own animation (fly, zoom, reveal, fade) moves and changes the mask
- a text mask cuts by the exact letter shapes
- a mask object that is **hidden** masks nothing, and the masked object draws whole

Earlier versions cut every non-text mask to the mask object's rectangle.

Only the drawing changed. Files are read and written exactly as before.

## Clipping masks in the Layers panel

You can clip objects the way you would in Photoshop. The Effects › Mask picker still works as before; both set the
same GT mask.

- **Hold Option** (Alt on Windows) and point at the line between two rows: the pointer turns into Photoshop's clipping
  cursor, a bent arrow beside a white square. **Click** to clip the upper object to the one below it.
- On the line above an object that is already clipped, the cursor is crossed out; clicking there **releases** it.
- Option-clicking the middle of a row is an ordinary click, as in Photoshop.
- **Right-click › Create Clipping Mask** clips the object to the one below it. On a clipped object the item reads
  **Release Clipping Mask**.
- As in Photoshop, clipping onto an object that is itself clipped uses that object's base, so a stack of clipped
  objects shares one base.
- A clipped row is indented behind Photoshop's bent down-arrow, pointing at its base, and the base's name is
  underlined.
- Each change is one undo step.

In the file a clipped object simply carries `<Mask Object="base" />`. An object counts as clipped when its mask
object sits below it in the same layer.

## Clicking clipped objects

A masked or clipped object can only be clicked where it actually shows on the canvas. A click on the part its mask
cuts away goes through to whatever is underneath.

## Notes

- The mask object is drawn as itself as well. To show only the cut-out, place the mask object below the masked
  object, or give it a fill the masked object covers.
- A mask object with a 3D (X or Y) rotation falls back to cutting by its outline.
